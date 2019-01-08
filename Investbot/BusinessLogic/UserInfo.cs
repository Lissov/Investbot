using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Investbot.BusinessLogic
{
    public class UserInfo
    {
        public string User { get; set; }
        public string Id { get; set; }
        public string ChannelId { get; set; }
        public DateTime? TermsAcceptedDate { get; set; }
    }
}
