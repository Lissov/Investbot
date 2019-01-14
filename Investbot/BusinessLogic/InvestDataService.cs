using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LissovWebsite.Interface.Model.Api;
using Microsoft.Bot.Builder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Investbot.BusinessLogic
{
    public class InvestDataService
    {
        private string url;
        private string username;
        private string password;
        private CookieContainer cookieContainer;

        private bool loggedIn = false;

        public InvestDataService(string url, string username, string password)
        {
            this.url = url;
            this.username = username;
            this.password = password;
            this.cookieContainer = new CookieContainer();
        }

        public async Task<bool> Login()
        {
            using (var client = new WebClient())
            {
                client.Headers.Add(HttpRequestHeader.ContentType, "application/json");

                var data = $"{{'login': '{username}', 'password':'{password}', 'remember':'true'}}";
                var res = await client.UploadStringTaskAsync(new Uri(this.url + "login"), null, data);

                dynamic content = JsonParser.Parse(res);
                if (content.authorized)
                {
                    cookieContainer.SetCookies(new Uri(this.url), client.ResponseHeaders["Set-Cookie"]);
                    return true;
                }

                return false;
            }
        }

        public async Task<StockList> GetStocks(string channelName, string channelId)
        {
            await CheckLogin();
            
            using (var client = new WebClient())
            {
                client.Headers.Add(HttpRequestHeader.Cookie, cookieContainer.GetCookieHeader(new Uri(this.url)));

                var request = $"stock/getStocksByChannel?channelName={channelName}&channelId={channelId}";
                var res = await client.DownloadStringTaskAsync(new Uri(this.url + request));
                //var parsed = JsonParser.Parse(res);
                var parsed = JsonConvert.DeserializeObject<StockList>(res);
                if (parsed.Stocks != null)
                {
                    parsed.Stocks = parsed.Stocks
                        .Where(s => s.Trades.Sum(t => t.Quantity) > 0)
                        .ToArray();
                }

                return parsed;
            }
        }

        private async Task CheckLogin()
        {
            if (!loggedIn)
            {
                loggedIn = await Login();
            }

            if (!loggedIn)
                throw new Exception("Can't login");
        }

        public async Task<Price> GetPrice(string code, string exchange)
        {
            await CheckLogin();

            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.Cookie, cookieContainer.GetCookieHeader(new Uri(this.url)));

                    var request = $"price/{exchange}/{code}";
                    var res = await client.DownloadStringTaskAsync(new Uri(this.url + request));
                    var parsed = JsonConvert.DeserializeObject<Price>(res);

                    return parsed;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<Dictionary<string, decimal>> GetRatesToEur(IEnumerable<string> currencies)
        {
            var res = new Dictionary<string, decimal>();
            foreach (var c in currencies)
            {
                var r = await GetFxRate(c, "EUR");
                res[c] = r ?? -1;
            }

            return res;
        }

        private async Task<decimal?> GetFxRate(string fromCcy, string toCcy)
        {
            await CheckLogin();

            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add(HttpRequestHeader.Cookie, cookieContainer.GetCookieHeader(new Uri(this.url)));

                    var request = $"fx-rate?fromCcy={fromCcy}&toCcy={toCcy}";
                    var res = await client.DownloadStringTaskAsync(new Uri(this.url + request));
                    dynamic parsed = JsonParser.Parse(res);

                    return (decimal?)parsed.fxRates[0]?.rate;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
