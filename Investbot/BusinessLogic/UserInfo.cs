using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;

namespace Investbot.BusinessLogic
{
    public class UserInfo
    {
        public ChannelData User { get; set; }
        public ChannelData Recipient { get; set; }
        public string ChannelId { get; set; }
        public DateTime? TermsAcceptedDate { get; set; }
        public string ServiceUrl { get; set; }
    }

    public class ChannelData
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public ChannelData()
        {
        }

        public ChannelData(ChannelAccount account)
        {
            Id = account.Id;
            Name = account.Name;
        }
    }
}
