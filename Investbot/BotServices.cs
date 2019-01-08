using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Configuration;

namespace Investbot
{
    public class BotServices
    {
        public BotServices(BotConfiguration botConfiguration)
        {
            foreach (var service in botConfiguration.Services)
            {
                switch (service.Type)
                {
                    case ServiceTypes.Luis:
                    {
                        var luis = (LuisService)service;
                        /*if (luis == null)
                        {
                            throw new InvalidOperationException("The LUIS service is not configured correctly in your '.bot' file.");
                        }*/

                        var endpoint = (luis.Region?.StartsWith("https://") ?? false) ? luis.Region : luis.GetEndpoint();
                        var app = new LuisApplication(luis.AppId, luis.AuthoringKey, endpoint);
                        var recognizer = new LuisRecognizer(app);
                        this.LuisServices.Add(luis.Name, recognizer);
                        break;
                    }
                }
            }
        }

        public Dictionary<string, LuisRecognizer> LuisServices { get; } = new Dictionary<string, LuisRecognizer>();
    }
}
