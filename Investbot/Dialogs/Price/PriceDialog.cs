using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Investbot.BusinessLogic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace Investbot.Dialogs.Price
{
    public class PriceDialog : ComponentDialog
    {
        private IStatePropertyAccessor<PriceState> priceStateAccessor;
        private const string PriceQuestDialog = "priceDialog";
        // Prompts names
        private const string StockNamePrompt = "stockNamePrompt";
        private const string StockCodePrompt = "stockCodePrompt";

        public PriceDialog(IStatePropertyAccessor<PriceState> accessor) : base(nameof(PriceDialog))
        {
            this.priceStateAccessor = accessor;

            var waterfallSteps = new WaterfallStep[]
            {
                InitializeStateStepAsync,
                PromptForStockNameStepAsync,
                PromptForStockCodeStepAsync,
                DisplayPriceStateStepAsync,
            };

            AddDialog(new WaterfallDialog(PriceQuestDialog, waterfallSteps));
            AddDialog(new TextPrompt(StockNamePrompt));
            AddDialog(new TextPrompt(StockCodePrompt));
        }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            var priceState = await priceStateAccessor.GetAsync(stepContext.Context, () => null);
            if (priceState == null)
            {
                var priceStateOpt = stepContext.Options as PriceState;
                if (priceStateOpt != null)
                {
                    await priceStateAccessor.SetAsync(stepContext.Context, priceState);
                }
                else
                {
                    await priceStateAccessor.SetAsync(stepContext.Context, new PriceState());
                }
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> PromptForStockNameStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var priceState = await priceStateAccessor.GetAsync(stepContext.Context);

            if (!string.IsNullOrWhiteSpace(priceState?.StockCode))
            {
                return await ShowPrice(stepContext);
            }

            if (string.IsNullOrWhiteSpace(priceState?.StockName))
            {
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = "What is the stock?",
                    },
                };
                return await stepContext.PromptAsync(StockNamePrompt, opts);
            }
            else
            {
                await SetNameFromCode(stepContext.Context, priceState);

                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> PromptForStockCodeStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var priceState = await priceStateAccessor.GetAsync(stepContext.Context);

            // name comes from user:
            var name = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(priceState.StockName) && name != null)
            {
                priceState.StockName = name;
                await priceStateAccessor.SetAsync(stepContext.Context, priceState);
                await SetNameFromCode(stepContext.Context, priceState);
            }

            if (string.IsNullOrWhiteSpace(priceState.StockCode))
            {
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"What is the code for {priceState.StockName}?",
                    },
                };
                return await stepContext.PromptAsync(StockCodePrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> DisplayPriceStateStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var priceState = await priceStateAccessor.GetAsync(stepContext.Context);

            // code coming from user
            var stockCode = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(priceState.StockCode) &&
                !string.IsNullOrWhiteSpace(stockCode))
            {
                priceState.StockCode = stockCode;
                await priceStateAccessor.SetAsync(stepContext.Context, priceState);
            }

            return await ShowPrice(stepContext);
        }

        private async Task<DialogTurnResult> ShowPrice(WaterfallStepContext stepContext)
        {
            var context = stepContext.Context;
            var priceState = await priceStateAccessor.GetAsync(context);

            var provider = new PriceProvider();
            var value = await provider.GetPrice(priceState.StockCode);

            var code = priceState.StockCode != priceState.StockName ? $" ({priceState.StockCode})" : "";
            if (value.HasValue)
            {
                if (priceState.StockCode != priceState.StockName)
                {
#pragma warning disable 4014
                    /*await - fire and forget*/ provider.AddMap(priceState.StockName, priceState.StockCode);
#pragma warning restore 4014
                }

                await context.SendActivityAsync($"Price of {priceState.StockName}{code} is {value}");
            }
            else
            {
                await context.SendActivityAsync($"Sorry, I can't find the price of {priceState.StockName} ({priceState.StockCode}).");
            }

            //cleanup so that next dialog starts fresh
            priceState.StockName = null;
            priceState.StockCode = null;
            await priceStateAccessor.SetAsync(context, priceState);

            return await stepContext.EndDialogAsync();
        }

        private async Task SetNameFromCode(ITurnContext context, PriceState priceState)
        {
            var code = await TryExtractCode(priceState.StockName);
            if (!string.IsNullOrEmpty(code))
            {
                priceState.StockCode = code;
                await priceStateAccessor.SetAsync(context, priceState);
            }
        }

        private async Task<string> TryExtractCode(string name)
        {
            var pp = new PriceProvider();
            var c = pp.GetCode(name.ToLower());
            if (!string.IsNullOrEmpty(c) && await pp.IsValidAsCode(c))
            {
                return c;
            }

            if (await pp.IsValidAsCode(name))
            {
                return name;
            }

            return null;
        }

    }
}
