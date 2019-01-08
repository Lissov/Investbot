using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Investbot.BusinessLogic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Api = LissovWebsite.Interface.Model.Api;

namespace Investbot.Dialogs.Portfolio
{
    public class PortfolioDialog : ComponentDialog
    {
        private InvestDataService investService;
        private IStatePropertyAccessor<UserInfo> userInfoAccessor;
        private IStatePropertyAccessor<PortfolioState> portfolioStateAccessor;
        public string PortfolioQuestDialog = "portfolioDialog";

        public PortfolioDialog(InvestDataService investService,
            IStatePropertyAccessor<UserInfo> userInfoAccessor,
            IStatePropertyAccessor<PortfolioState> portfolioStateAccessor) 
            : base(nameof(PortfolioDialog))
        {
            this.investService = investService;
            this.userInfoAccessor = userInfoAccessor;
            this.portfolioStateAccessor = portfolioStateAccessor;

            var waterfallSteps = new WaterfallStep[]
            {
                InitializeStateStepAsync,
                LoadPortfolioStepAsync,
                LoadPricesStepAsync
            };

            AddDialog(new WaterfallDialog(PortfolioQuestDialog, waterfallSteps));
        }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            var portfolio = await portfolioStateAccessor.GetAsync(stepContext.Context, () => new PortfolioState { LoadedAt = null });
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> LoadPortfolioStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            var portfolio = await portfolioStateAccessor.GetAsync(stepContext.Context);

            if (portfolio?.LoadedAt == null || portfolio.LoadedAt < DateTime.Now.AddDays(-1))
            {
                try
                {
                    await stepContext.Context.SendActivityAsync($"Loading portfolio ...");
                    var userInfo = await userInfoAccessor.GetAsync(stepContext.Context, null);
                    var channelName = userInfo.ChannelId == "emulator" ? "facebook" : userInfo.ChannelId;
                    var channelId = userInfo.ChannelId == "emulator" ? "<add here>" : userInfo.Id;
                    var r = await this.investService.GetStocks(channelName, channelId);
                    portfolio.Stocks = r.ToArray();
                    portfolio.LoadedAt = DateTime.Now;
                    portfolio.Prices = new Dictionary<int, Api.Price>();
                    //await stepContext.Context.SendActivityAsync($"You have {r.Count()} positions.");
                    var currencies = portfolio.Stocks.Select(s => s.Currency).Distinct();
                    portfolio.RatesToEur = await this.investService.GetRatesToEur(currencies);
                }
                catch (Exception ex)
                {
                    await stepContext.Context.SendActivityAsync("Error: " + ex.Message);
                }
            }

            if (portfolio?.LoadedAt != null)
            {
                await stepContext.Context.SendActivityAsync($"Portfolio with {portfolio.Stocks.Count()} positions last loaded at {portfolio.LoadedAt}.");
                var rates = portfolio.RatesToEur.Keys
                    .Where(k => k != "EUR")
                    .Select(k => $"{k}: {Math.Round(portfolio.RatesToEur[k], 2)}");
                await stepContext.Context.SendActivityAsync($"FxRates: {string.Join(", ", rates)}.");
                return await stepContext.NextAsync();
            }

            return await stepContext.EndDialogAsync();
        }

        private async Task<DialogTurnResult> LoadPricesStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            var portfolio = await portfolioStateAccessor.GetAsync(stepContext.Context);

            foreach (var portfolioStock in portfolio.Stocks)
            {
                if (!portfolio.Prices.ContainsKey(portfolioStock.Id))
                {
                    var p = await investService.GetPrice(portfolioStock.Code, portfolioStock.Exchange);
                    if (p != null)
                        portfolio.Prices[portfolioStock.Id] = p;
                }
            }
            var missing = portfolio.Stocks.Where(s => !portfolio.Prices.ContainsKey(s.Id) || portfolio.Prices[s.Id].LastPrice <= 0).ToList();
            if (missing.Any())
            {
                await stepContext.Context.SendActivityAsync(
                    $"Not all prices loaded yet. Waiting for [{string.Join(", ", missing.Select(m => m.Code))}]");
            }
            else
            {
                var helper = new PortfolioHelper();
                //Recommendations are
                var recommendations = helper.GetRecommendations(portfolio);                
                if (recommendations.Any())
                {
                    await stepContext.Context.SendActivityAsync("Recommendations for today: ");
                    foreach (var r in recommendations)
                    {
                        await stepContext.Context.SendActivityAsync(r);
                    }
                }
                else
                {
                    await stepContext.Context.SendActivityAsync("Now all in the defined range.");
                }

                var marks = helper.GetMarksStatus(portfolio);
                if (!string.IsNullOrEmpty(marks))
                {
                    await stepContext.Context.SendActivityAsync(marks);
                }

                await stepContext.Context.SendActivityAsync("TOTALS: " + helper.GetSummary(portfolio));
            }

            return await stepContext.EndDialogAsync();
        }
    }
}
