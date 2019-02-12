using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Investbot.Dialogs.Portfolio;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Api = LissovWebsite.Interface.Model.Api;

namespace Investbot.BusinessLogic
{
    public class PortfolioPushService
    {
        private bool started = false;
        private InvestDataService investService;
        private MicrosoftAppCredentials botCredentials;
        private IStorage dataStorage;

        public PortfolioPushService(InvestDataService investService, MicrosoftAppCredentials botCredentials, IStorage dataStorage)
        {
            this.investService = investService;
            this.botCredentials = botCredentials;
            this.dataStorage = dataStorage;
        }

        private static List<ExecutionData> queue = new List<ExecutionData>();
        public static int QueueSize => queue?.Count ?? 0;
        public static string Queue => string.Join(", ",
            queue.Select(q => (q.IsContinuation ? "C" : " ") + q.ExecutionTimex.Value.Value.ToString("yyyy-MM-dd HH:mm")));
        public async Task PeriodicCheck()
        {
            Console.WriteLine("Fired! " + DateTime.Now.Minute + " Queue: " + Queue);
            if (queue.Count == 0)
            {
                await LoadQueue();
            }
            var toExecute = queue.Where(q => q.ExecutionTimex.Value <= DateTime.UtcNow).ToList();
            foreach (var job in toExecute)
            {
                if (job.ExecutionTimex.Interval != null
                    && (job.ExecutionTimex.End == null 
                         || job.ExecutionTimex.End + job.ExecutionTimex.Interval > DateTime.UtcNow)
                    )
                    job.ExecutionTimex.Value += job.ExecutionTimex.Interval.Value;
                else
                    queue.Remove(job);
                PerformUpdate(job); // Fire and forget
                Console.WriteLine("Storing");
                await StoreQueue();
                Console.WriteLine("Stored");
            }
        }

        private async Task StoreQueue()
        {
            var data = await dataStorage.ReadAsync(new [] {"bot_queue"});
            data["bot_queue"] = queue;
            await dataStorage.WriteAsync(data);
        }

        private async Task LoadQueue()
        {
            var data = await dataStorage.ReadAsync(new[] { "bot_queue" });
            if (data.TryGetValue("bot_queue", out var obj))
                queue = obj as List<ExecutionData> ?? queue;
        }

        private async Task PerformUpdate(ExecutionData job)
        {
            Console.WriteLine("Queue item processing");
            //Console.WriteLine($"Executing Push for: {job.UserInfo.ChannelId}:{job.UserInfo.User.Id}");
            if (!job.IsContinuation)
            {
                await SendTyping(job);
                job.Portfolio = new PortfolioState();
                await investService.LoadPortfolio(job.UserInfo, job.Portfolio);
            }

            if (job.Portfolio?.Status == Api.StockStatus.Success)
            {
                await SendTyping(job);
                if (!job.IsContinuation)
                    job.Portfolio.Prices = new Dictionary<int, Api.Price>();

                var missing = await investService.LoadPrices(job.Portfolio);
                if (missing.Any())
                {
                    //Console.WriteLine($"Not all prices loaded yet. Waiting for [{string.Join(", ", missing)}]");
                    var jobContinue = new ExecutionData
                    {
                        ExecutionTimex = new Timex.Timex { Value = DateTime.UtcNow.AddMinutes(1) },
                        IsContinuation = true,
                        UserInfo = job.UserInfo,
                        Detailed = job.Detailed,
                        Portfolio = job.Portfolio // use the same instance
                    };
                    queue.Add(jobContinue);
                    StoreQueue();
                    return;
                }
                else
                {
                    PrintSuggestions(job);
                }
            }
            else
            {
                PrintTexts(job, "Portfolio not loaded with status: " + job.Portfolio?.Status);
            }
        }

        private async void PrintSuggestions(ExecutionData job)
        {
            var helper = new PortfolioHelper();
            var texts = new List<string>();
            texts.Add("Update for " + DateTime.Now);
            var recs = helper.GetRecommendations(job.Portfolio);
            if (job.Detailed)
            {
                texts.Add(helper.GetRatesMessage(job.Portfolio));
                texts.AddRange(recs);
                texts.Add(helper.GetSummary(job.Portfolio));
            }
            else
            {
                if (recs.Count == 0 && job.ExecutionTimex.Interval != null) return; // skip for recurring
                texts.AddRange(recs);
            }
            await PrintTexts(job, texts.ToArray());
        }

        private async Task PrintTexts(ExecutionData job, params string[] texts)
        {
            var acc = "";
            try
            {
                var convData = await InitConversation(job);
                foreach (var text in texts)
                {
                    await SendMessage(convData, text);
                }
            }
            catch (Exception ex)
            {
                Diagnostic.Log += $"\r\n\r\nPush error while [{acc}]: " + ex.Message;
            }
        }

        private async Task SendTyping(ExecutionData job)
        {
            var acc = "";
            try
            {
                var convData = await InitConversation(job);
                await SendMessage(convData, "", isTyping: true);
            }
            catch (Exception ex)
            {
                Diagnostic.Log += $"\r\n\r\nPush error while [{acc}]: " + ex.Message;
            }
        }

        private static async Task SendMessage(ConversationData convData, string messageText, bool isTyping = false)
        {
            var message = isTyping
                ? (Activity)Activity.CreateTypingActivity()
                : (Activity)Activity.CreateMessageActivity();
            message.From = convData.BotAccount;
            message.Recipient = convData.UserAccount;
            message.Conversation = new ConversationAccount(id: convData.ConversationId, isGroup: false);
            message.Text = messageText;
            await convData.Connector.Conversations.SendToConversationAsync(message);
        }

        private async Task<ConversationData> InitConversation(ExecutionData job)
        {
            var convData = new ConversationData();
            convData.BotAccount = new ChannelAccount {Id = job.UserInfo.Recipient.Id, Name = job.UserInfo.Recipient.Name};
            convData.UserAccount = new ChannelAccount {Id = job.UserInfo.User.Id, Name = job.UserInfo.User.Name};
            var url = job.UserInfo.ServiceUrl;
            MicrosoftAppCredentials.TrustServiceUrl(url, DateTime.Now.AddDays(1));
            convData.Connector = new ConnectorClient(new Uri(url), botCredentials);
            var conversation = await convData.Connector.Conversations
                .CreateDirectConversationAsync(convData.BotAccount, convData.UserAccount);
            convData.ConversationId = conversation.Id;
            return convData;
        }

        public async Task SetUpdateForAUser(UserInfo userInfo, Timex.Timex executionTimex, bool detailed)
        {
            if (!started)
            {
                if (queue.Count == 0) await LoadQueue();
                SetupJob();
                started = true;
            }

            queue.Add(new ExecutionData
            {
                ExecutionTimex = executionTimex,
                UserInfo = userInfo,
                IsContinuation = false,
                Detailed = detailed
            });
            await StoreQueue();
        }

        public void SetupJob()
        {
            RecurringJob.AddOrUpdate(() => PeriodicCheck(), Cron.Minutely);
        }

        private class ExecutionData
        {
            public Timex.Timex ExecutionTimex { get; set; }
            public UserInfo UserInfo { get; set; }
            public PortfolioState Portfolio { get; set; }
            public bool IsContinuation { get; set; }
            public bool Detailed { get; set; }
        }

        private class ConversationData
        {
            public string ConversationId { get; set; }
            public ChannelAccount BotAccount { get; set; }
            public ChannelAccount UserAccount { get; set; }
            public ConnectorClient Connector { get; set; }
        }

        public async Task<int> ClearForUser(UserInfo userInfo)
        {
            if (queue.Count == 0)
                await LoadQueue();
            var toClear = queue.Where(j =>
                j.UserInfo.User.Id == userInfo.User.Id && j.UserInfo.ChannelId == userInfo.ChannelId);
            foreach (var job in toClear)
            {
                queue.Remove(job);
            }

            await StoreQueue();
            return toClear.Count();
        }
    }
}
