using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Api = LissovWebsite.Interface.Model.Api;

namespace Investbot.Dialogs.Portfolio
{
    public class PortfolioState
    {
        public Api.Stock[] Stocks { get; set; }
        public Dictionary<int, Api.Price> Prices { get; set; }
        public Dictionary<string, decimal> RatesToEur { get; set; }
        public DateTime? LoadedAt { get; set; }
    }
}
