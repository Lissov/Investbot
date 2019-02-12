using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Investbot.BusinessLogic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
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
            var userInfo = await userInfoAccessor.GetAsync(stepContext.Context, () => null);
            if (userInfo?.TermsAcceptedDate == null)
            {
                await stepContext.Context.SendActivityAsync("Please review and accept our Privacy Policy and User Agreement before using the portfolio service.");
                return await stepContext.EndDialogAsync();
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> LoadPortfolioStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            var portfolio = await portfolioStateAccessor.GetAsync(stepContext.Context);

            if (portfolio?.LoadedAt == null || portfolio.LoadedAt < DateTime.Now.AddDays(-1))
            {
                try
                {
                    portfolio = portfolio ?? new PortfolioState();
                    await stepContext.Context.SendActivityAsync($"Loading portfolio ...");

                    var reply = stepContext.Context.Activity.CreateReply();
                    reply.Text = null;
                    reply.Type = ActivityTypes.Typing;
                    await stepContext.Context.SendActivityAsync(reply); //show typing pause

                    var userInfo = await userInfoAccessor.GetAsync(stepContext.Context);
                    await investService.LoadPortfolio(userInfo, portfolio);
                    if (portfolio.Status != Api.StockStatus.Success)
                    {
                        await stepContext.Context.SendActivityAsync(GetStockLoadErrorMessage(portfolio.Status));
                        return await stepContext.EndDialogAsync();
                    }
                }
                catch (Exception ex)
                {
                    await stepContext.Context.SendActivityAsync("Error: " + ex.Message);
                }
            }

            if (portfolio?.LoadedAt != null)
            {
                //await stepContext.Context.SendActivityAsync($"Portfolio with {portfolio.Stocks.Count()} positions last loaded at {portfolio.LoadedAt}.");
                var ratesMsg = new PortfolioHelper().GetRatesMessage(portfolio);
                await stepContext.Context.SendActivityAsync(ratesMsg);
                return await stepContext.NextAsync();
            }

            return await stepContext.EndDialogAsync();
        }

        private string GetStockLoadErrorMessage(string status)
        {
            switch (status)
            {
                case Api.StockStatus.ChannelNotRegistered:
                    return "Channel is not registered. Please contact the bot administrator to register a channel.";
                default:
                    return "Unknown error!";
            }
        }

        private async Task<DialogTurnResult> LoadPricesStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            var portfolio = await portfolioStateAccessor.GetAsync(stepContext.Context);

            var missing = await investService.LoadPrices(portfolio);
            if (missing.Any())
            {
                await stepContext.Context.SendActivityAsync(
                    $"Not all prices loaded yet. Waiting for [{string.Join(", ", missing)}]");
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
