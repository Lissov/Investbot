using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Investbot.BusinessLogic;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace Investbot.Dialogs.Privacy
{
    public class PrivacyDialog : ComponentDialog
    {
        public string PrivacyQuestDialog = "privacyDialog";
        public string PrivacyAcceptancePrompt = "privacyPrompt";

        private IStatePropertyAccessor<UserInfo> userInfoAccessor;
        public PrivacyDialog(IStatePropertyAccessor<UserInfo> accessor) : base(nameof(PrivacyDialog))
        {
            this.userInfoAccessor = accessor;

            var waterfallSteps = new WaterfallStep[]
            {
                InitializeStateStepAsync,
                ShowPrivacyAndTermsLinks,
                PromptForAcceptanceStepAsync,
                RecordAcceptanceStateStepAsync,
            };

            AddDialog(new WaterfallDialog(PrivacyQuestDialog, waterfallSteps));
            AddDialog(new TextPrompt(PrivacyAcceptancePrompt));
        }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            var userInfo = await userInfoAccessor.GetAsync(stepContext.Context, () => null);
            if (userInfo == null)
            {
                await stepContext.Context.SendActivityAsync("Sorry, bot was not initialized properly and seem to be not working :(");
                return new DialogTurnResult(DialogTurnStatus.Cancelled);
            }

            await PrintAcceptanceStatus(stepContext, userInfo);

            return await stepContext.NextAsync();
        }

        private async Task PrintAcceptanceStatus(WaterfallStepContext stepContext, UserInfo userInfo)
        {
            userInfo = userInfo ?? await userInfoAccessor.GetAsync(stepContext.Context, () => null);
            if (AreTermsAccepted(userInfo))
            {
                await stepContext.Context.SendActivityAsync(
                    $"You are identified as {userInfo.User.Name} with ID {userInfo.User.Id} on channel {userInfo.ChannelId}. "
                  + $"You accepted Privacy Policy and User Agreement on {userInfo.TermsAcceptedDate.ToString()}");
            }
        }

        private bool AreTermsAccepted(UserInfo userInfo)
        {
            return userInfo.TermsAcceptedDate != null
                   && userInfo.TermsAcceptedDate >= new DateTime(2018, 12, 27);
        }

        private async Task<DialogTurnResult> ShowPrivacyAndTermsLinks(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            await stepContext.Context.SendActivityAsync(
                "By continuing using the service, you are agreeing with User Agreement and Privacy Policy. Please read and review them carefully."
                + "\n" +
                "To allow us to collect information that enables extended services, we need to record your explicit agreement.");

            await stepContext.Context.SendActivityAsync(
                "Privacy Policy: http://lissov.kiev.ua/Investbot/Privacy");
            await stepContext.Context.SendActivityAsync(
                "User Agreement: http://lissov.kiev.ua/Investbot/UserAgreement");

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> PromptForAcceptanceStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            var userInfo = await userInfoAccessor.GetAsync(stepContext.Context, () => null);

            var message = "";
            if (AreTermsAccepted(userInfo))
            {
                message = "Type 'Revoke' to revoke your agreement or anything else otherwize.";
            }
            else
            {
                message = "Type 'Yes' to confirm you have read and accept them.";
            }

            var opts = new PromptOptions
            {
                Prompt = new Activity { Type = ActivityTypes.Message, Text = message }
            };
            return await stepContext.PromptAsync(PrivacyAcceptancePrompt, opts);
        }

        private async Task<DialogTurnResult> RecordAcceptanceStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationtoken)
        {
            var userInfo = await userInfoAccessor.GetAsync(stepContext.Context, () => null);

            var option = stepContext.Result as string;
            switch (option.ToLower())
            {
                case "yes":
                    userInfo.TermsAcceptedDate = DateTime.Now;
                    await userInfoAccessor.SetAsync(stepContext.Context, userInfo);
                    await stepContext.Context.SendActivityAsync("Thanks for accepting! Your agreement is recorded. You can revoke it any time by checking this dialog again.");
                    await PrintAcceptanceStatus(stepContext, userInfo);
                    break;
                case "revoke":
                    userInfo.TermsAcceptedDate = null;
                    await userInfoAccessor.SetAsync(stepContext.Context, userInfo);
                    await stepContext.Context.SendActivityAsync("Your cancellation of agreement is recorded. If you change your mind, please use the same dialog (e.g. type 'Privacy').");
                    break;
                default:
                    await stepContext.Context.SendActivityAsync(
                        "Your answer is not recognized. You can review Privacy Policy and User Agreement by asking me again about Privacy.");
                    break;
            }

            return await stepContext.EndDialogAsync();
        }
    }
}
