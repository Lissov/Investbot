using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Investbot.BusinessLogic
{
    public class PriceProvider
    {
        private static Dictionary<string, string> _mapping;

        public PriceProvider()
        {
            if (_mapping == null)
            {
                _mapping = new Dictionary<string, string>();
                _mapping.Add("microsoft", "msft");
                _mapping.Add("general electric", "ge");
            }
        }

        public string GetCode(string name)
        {
            if (_mapping.ContainsKey(name))
            {
                return _mapping[name];
            }

            if (_mapping.ContainsValue(name))
            {
                return name;
            }

            return null;
        }

        public async Task<bool> IsValidAsCode(string code)
        {
            var pr = await GetPrice(code);
            return pr.HasValue;
        }

        public async Task<bool> AddMap(string name, string code)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("NAME and CODE are mandatory.");
            }

            if (_mapping.ContainsKey(name) && _mapping[name] == code)
            {
                return true;
            }

            var valid = await IsValidAsCode(code);
            if (valid)
            {
                _mapping[name] = code;
            }

            return valid;
        }

        public async Task<decimal?> GetPrice(string code)
        {
            try
            {
                var url =
                    $@"https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol={code}&interval=5min&apikey=0RWDI9DFEV10BVM8";
                var client = new HttpClient();
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    dynamic data = await response.Content.ReadAsStringAsync();
                    Dictionary<string, JObject> parsed =
                        JsonConvert.DeserializeObject<Dictionary<string, JObject>>(data);
                    var priceKey = parsed.Keys.SingleOrDefault(p => p.StartsWith("Time Series"));
                    if (string.IsNullOrEmpty(priceKey)) return null;
                    var prices = parsed[priceKey];
                    if (prices.Count > 0)
                    {
                        JProperty price = prices.First as JProperty;
                        var time = price.Name;
                        foreach (JProperty pr in price.Value)
                        {
                            if (pr.Name.Contains("close"))
                            {
                                var v = pr.Value;
                                return decimal.Parse(v.ToString());
                            }
                        }
                    }

                    return 2.4m;
                }
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
                // and return null below
            }

            return null;
        }
    }
}
