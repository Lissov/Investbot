// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Investbot.BusinessLogic;
using Investbot.Dialogs.Portfolio;
using Investbot.Dialogs.Price;
using Investbot.Dialogs.Privacy;
using Investbot.Dialogs.Updater;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using Timex;

namespace Investbot
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service. Transient lifetime services are created
    /// each time they're requested. Objects that are expensive to construct, or have a lifetime
    /// beyond a single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class InvestbotBot : IBot
    {
        private readonly BotServices services;
        private readonly ConversationState conversationState;
        private readonly UserState userState;
        private readonly IStatePropertyAccessor<PriceState> priceStateAccessor;
        private readonly IStatePropertyAccessor<PortfolioState> portfolioStateAccessor;
        private readonly IStatePropertyAccessor<DialogState> dialogStateAccessor;
        private readonly IStatePropertyAccessor<UserInfo> userStateAccessor;
        private readonly IStatePropertyAccessor<UpdaterState> updaterStateAccessor;
        public static readonly string LuisConfiguration = "InvestBotLuisApplication";

        // Supported LUIS Intents
        public const string GreetingIntent = "Greeting";
        public const string PriceIntent = "Price";
        public const string PortfolioIntent = "Portfolio";
        public const string TermsIntent = "Terms";
        public const string CancelIntent = "Cancel";
        public const string HelpIntent = "Help";
        public const string UpdaterIntent = "PushInfo";
        public const string NoneIntent = "None";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>                        
        public InvestbotBot(BotServices services, UserState userState,
            InvestDataService investService, PortfolioPushService pushService,
            ConversationState conversationState)
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            this.userState = userState ?? throw new ArgumentNullException(nameof(userState));
            this.conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));

            this.priceStateAccessor = userState.CreateProperty<PriceState>(nameof(PriceState));
            this.portfolioStateAccessor = userState.CreateProperty<PortfolioState>(nameof(PortfolioState));
            this.dialogStateAccessor = conversationState.CreateProperty<DialogState>(nameof(DialogState));
            this.userStateAccessor = conversationState.CreateProperty<UserInfo>(nameof(UserInfo));
            this.updaterStateAccessor = conversationState.CreateProperty<UpdaterState>(nameof(UpdaterState));

            // Verify LUIS configuration.
            if (!services.LuisServices.ContainsKey(LuisConfiguration))
            {
                throw new InvalidOperationException($"The bot configuration does not contain a service type of `luis` with the id `{LuisConfiguration}`.");
            }

            Dialogs = new DialogSet(dialogStateAccessor);
            Dialogs.Add(new PriceDialog(priceStateAccessor));
            Dialogs.Add(new PrivacyDialog(userStateAccessor));
            Dialogs.Add(new PortfolioDialog(investService, userStateAccessor, portfolioStateAccessor));
            Dialogs.Add(new UpdaterDialog(userStateAccessor, updaterStateAccessor, pushService));
        }

        private DialogSet Dialogs { get; set; }

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            var activity = turnContext.Activity;

            await UpdateUserState(turnContext);

            // Create a dialog context
            var dc = await Dialogs.CreateContextAsync(turnContext);

            if (activity.Type == ActivityTypes.Message)
            {
                var luisResults = await services.LuisServices[LuisConfiguration]
                    .RecognizeAsync(dc.Context, cancellationToken);

                var topScoringIntent = luisResults?.GetTopScoringIntent();

                var topIntent = topScoringIntent.Value.intent;

                await UpdatePriceState(luisResults, dc.Context);
                await UpdateUpdaterState(luisResults, dc.Context);

                var interrupted = await IsTurnInterruptedAsync(dc, topIntent);
                if (interrupted)
                {
                    // Bypass the dialog. Save state before the next turn.
                    await conversationState.SaveChangesAsync(turnContext);
                    await userState.SaveChangesAsync(turnContext);
                    return;
                }

                // Continue the current dialog
                var dialogResult = await dc.ContinueDialogAsync();

                // if no one has responded,
                if (!dc.Context.Responded)
                {
                    // examine results from active dialog
                    switch (dialogResult.Status)
                    {
                        case DialogTurnStatus.Empty:
                            switch (topIntent)
                            {
                                case PriceIntent:
                                    await dc.BeginDialogAsync(nameof(PriceDialog));
                                    break;
                                case TermsIntent:
                                    await dc.BeginDialogAsync(nameof(PrivacyDialog));
                                    break;
                                case PortfolioIntent:
                                    await dc.BeginDialogAsync(nameof(PortfolioDialog));
                                    break;
                                case UpdaterIntent:

                                    await dc.BeginDialogAsync(nameof(UpdaterDialog));
                                    break;

                                case NoneIntent:
                                default:
                                    // Help or no intent identified, either way, let's provide some help.
                                    // to the user
                                    await dc.Context.SendActivityAsync("I didn't understand what you just said to me.");
                                    break;
                            }
                            break;

                        case DialogTurnStatus.Waiting:
                            break;      // The active dialog is waiting for a response from the user, so do nothing.

                        case DialogTurnStatus.Complete:
                            await dc.EndDialogAsync();
                            break;

                        default:
                            await dc.CancelAllDialogsAsync();
                            break;
                    }
                }
            }
            else if (activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (activity.MembersAdded != null)
                {
                    foreach (var member in activity.MembersAdded)
                    {
                        if (member.Id != activity.Recipient.Id)
                        {
                            await GreetNewUser(dc);
                        }
                    }
                }
            }

            await conversationState.SaveChangesAsync(turnContext);
            await userState.SaveChangesAsync(turnContext);
        }
        
        private async Task GreetNewUser(DialogContext dc)
        {
            var userInfo = await userStateAccessor.GetAsync(dc.Context);
            var message = (!string.IsNullOrEmpty(userInfo?.User?.Name))
                ? $"Hi {userInfo.User.Name}, I am InvestmentBot. Glad to see you here! You are identified with ID [{userInfo.User.Id}] on channel [{userInfo.ChannelId}]"
                : "Hi, I am Investment Bot! I was not able to identify you, so some of my features will be not accessible.";

            await dc.Context.SendActivityAsync(message);
        }

        private async Task<bool> IsTurnInterruptedAsync(DialogContext dc, string topIntent)
        {
            // See if there are any conversation interrupts we need to handle.
            if (topIntent.Equals(CancelIntent))
            {
                if (dc.ActiveDialog != null)
                {
                    await dc.CancelAllDialogsAsync();
                    await dc.Context.SendActivityAsync("Ok. I've canceled our last activity.");
                }
                else
                {
                    await dc.Context.SendActivityAsync("I don't have anything to cancel.");
                }
                return true;        // Handled the interrupt.
            }

            if (topIntent.Equals(HelpIntent))
            {
                await dc.Context.SendActivityAsync("Let me try to provide some help.");
                await dc.Context.SendActivityAsync("I can tell price of stocks, understand being asked for help, or being asked to cancel what I am doing. Try typing 'What is the price of Microsoft?'");
                await dc.Context.SendActivityAsync("Jobs in queue: " + PortfolioPushService.QueueSize + ": " + PortfolioPushService.Queue);

                if (dc.ActiveDialog != null)
                {
                    await dc.RepromptDialogAsync();
                }
                return true;        // Handled the interrupt.
            }

            return false;           // Did not handle the interrupt.
        }
        
        private async Task UpdatePriceState(RecognizerResult luisResult, ITurnContext turnContext)
        {
            if (luisResult.Entities != null && luisResult.Entities.HasValues)
            {
                var priceState = await priceStateAccessor.GetAsync(turnContext, () => new PriceState());
                var entities = luisResult.Entities;

                string[] stockNameEntities = { "stock", "stock_patternAny" };
                string[] stockCodeEntities = { "code", "code_patternAny" };

                string newName = GetEntityValue(entities, stockNameEntities);
                string newCode = GetEntityValue(entities, stockCodeEntities);

                if (!string.IsNullOrEmpty(newName) && priceState.StockName != newName)
                {
                    priceState.StockCode = null;
                    priceState.StockName = newName;
                }

                if (!string.IsNullOrEmpty(newCode))
                {
                    priceState.StockCode = newCode;
                }

                // Set the new values into state.
                await priceStateAccessor.SetAsync(turnContext, priceState);
            }
        }

        private async Task UpdateUpdaterState(RecognizerResult luisResult, ITurnContext turnContext)
        {
            try
            {
                if (luisResult.Entities != null && luisResult.Entities.HasValues)
                {
                    var updaterState = await updaterStateAccessor.GetAsync(turnContext, () => new UpdaterState());
                    var entities = luisResult.Entities;
                    if (entities["push_action"] != null)
                    {
                        updaterState.Action = entities["push_action"][0][0].ToString();
                    }
                    if (entities["push_detalization"] != null)
                    {
                        updaterState.Detalization = entities["push_detalization"][0][0].ToString();
                    }
                    if (entities["datetime"] != null)
                    {
                        dynamic dt = entities["datetime"];
                        if (TimexParser.TryParse(dt, out Timex.Timex timex))
                            updaterState.UpdateDateTime = timex;
                        else
                            await turnContext.SendActivityAsync($"Can't parse timex expression: {dt}");
                    }
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync("Error: " + ex.Message);
            }
        }

        private static string GetEntityValue(JObject entities, string[] stockNameEntities)
        {
            foreach (var key in stockNameEntities)
            {
                if (entities[key] != null)
                {
                    return entities[key][0].ToString();
                }
            }

            return null;
        }

        private async Task UpdateUserState(ITurnContext turnContext)
        {
            try
            {
                var userInfo = await userStateAccessor.GetAsync(turnContext, () => new UserInfo());

                userInfo.User = new ChannelData(turnContext.Activity.From);
                userInfo.Recipient = new ChannelData(turnContext.Activity.Recipient);
                userInfo.ChannelId = turnContext.Activity.ChannelId;
                userInfo.ServiceUrl = turnContext.Activity.ServiceUrl;

                await userStateAccessor.SetAsync(turnContext, userInfo);
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync("Error: " + ex.Message);
            }
        }
    }
}
