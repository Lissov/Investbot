using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Investbot.BusinessLogic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace Investbot.Dialogs.Updater
{
    public class UpdaterDialog : ComponentDialog
    {
        public string UpdaterQuestDialog = "updatrrDialog";
        private IStatePropertyAccessor<UserInfo> userInfoAccessor;
        private IStatePropertyAccessor<UpdaterState> updaterStateAccessor;
        private PortfolioPushService pushService;

        public UpdaterDialog(IStatePropertyAccessor<UserInfo> userInfoAccessor, IStatePropertyAccessor<UpdaterState> updaterStateAccessor, PortfolioPushService pushService)
            : base(nameof(UpdaterDialog))
        {
            this.userInfoAccessor = userInfoAccessor;
            this.updaterStateAccessor = updaterStateAccessor;
            this.pushService = pushService;

            var waterfallSteps = new WaterfallStep[]
            {
                InitializeStateStepAsync,
                DefinePeriodStepAsync,
                SetUpdaterStepAsync
            };

            AddDialog(new WaterfallDialog(UpdaterQuestDialog, waterfallSteps));
        }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
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

        private async Task<DialogTurnResult> DefinePeriodStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> SetUpdaterStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            try
            {
                var userInfo = await userInfoAccessor.GetAsync(stepContext.Context);
                var updaterState = await this.updaterStateAccessor.GetAsync(stepContext.Context);

                if (updaterState.Action == "clear")
                {
                    await stepContext.Context.SendActivityAsync(Activity.CreateTypingActivity());
                    var cleared = await pushService.ClearForUser(userInfo);
                    await stepContext.Context.SendActivityAsync($"Cleared {cleared} your tasks.");
                }
                if (string.IsNullOrEmpty(updaterState.Action) || updaterState.Action == "add") {
                    var next = updaterState.UpdateDateTime;
                    if (next.Value == null) next.Value = next.Start ?? DateTime.UtcNow;

                    var detailed = updaterState.Detalization == "full";
                    await pushService.SetUpdateForAUser(userInfo, next, detailed);
                    var status = $"{(detailed ? "Detailed" : "Brief")} update is scheduled at {next.Value} UTC";
                    if (next.Interval != null) status += $" and then every {next.Interval.Value}";
                    if (next.End != null) status += $" till {next.End.Value} UTC";
                    status += ".";
                    await stepContext.Context.SendActivityAsync(status);
                }
                updaterState.Action = "";
                updaterState.UpdateDateTime = null;
                await this.updaterStateAccessor.SetAsync(stepContext.Context, updaterState);
            }
            catch (Exception ex)
            {
                await stepContext.Context.SendActivityAsync("Error in updater: " + ex.Message);
            }


            return await stepContext.EndDialogAsync();
        }
    }
}
