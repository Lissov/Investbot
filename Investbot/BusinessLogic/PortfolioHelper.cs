﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Investbot.Dialogs.Portfolio;
using Api = LissovWebsite.Interface.Model.Api;

namespace Investbot.BusinessLogic
{
    public class PortfolioHelper
    {
        public List<string> GetRecommendations(PortfolioState portfolio)
        {
            var recommendation = portfolio.Stocks
                .SelectMany(s => s.PriceMarks.Select(pm => new { Stock = s, Mark = pm }))
                .Where(spm =>
                    (spm.Mark.Type.Trim() == "buy" && spm.Mark.Price >= portfolio.Prices[spm.Stock.Id].LastPrice)
                    || (spm.Mark.Type.Trim() == "sell" && spm.Mark.Price <= portfolio.Prices[spm.Stock.Id].LastPrice));

            return recommendation.Select(mark =>
            {
                var relative = mark.Mark.Type.Trim() == "buy" ? "below" : "above";
                return
                    $"{mark.Stock.Code} {portfolio.Prices[mark.Stock.Id].LastPrice} {relative} {mark.Mark.Price} [{mark.Mark.Comment}]";
            }).ToList();
        }

        public string GetMarksStatus(PortfolioState portfolio)
        {
            var missingMarks = portfolio.Stocks
                .Where(s => !s.PriceMarks.Any(pm => pm.Type.Trim() == "buy")
                            || !s.PriceMarks.Any(pm => pm.Type.Trim() == "sell"))
                .Select(s => s.Code);
            if (missingMarks.Any())
                return "Missing marks on: " + string.Join(", ", missingMarks);

            var width = portfolio.Stocks.Sum(s =>
            {
                var lowMark = s.PriceMarks.Where(pm => pm.Type.Trim() == "buy").Max(pm => pm.Price);
                var highMark = s.PriceMarks.Where(pm => pm.Type.Trim() == "sell").Min(pm => pm.Price);
                return (highMark - lowMark) * s.Trades.Sum(t => t.Quantity) * portfolio.RatesToEur[s.Currency];
            });
            return "Marks width is EUR " + Math.Round(width, 2);
        }

        public string GetSummary(PortfolioState portfolio)
        {
            try
            {
                var stockSummary =
                    portfolio.Stocks
                        .GroupBy(s => s.Currency)
                        .Select(g =>
                        {
                            var amounts = g.Select(s =>
                            {
                                var amt = s.Trades.Select(t => t.Quantity).Sum()
                                          * (portfolio.Prices[s.Id].LastPrice ?? -1);
                                if (amt < 0) throw new Exception("Negative amount on: " + s.Code);
                                return amt;
                            });
                            return new {Currency = g.Key, Amount = amounts.Sum()};
                        })
                        .ToList();

                stockSummary.Add(new
                {
                    Currency = "TotalEUR",
                    Amount = stockSummary.Select(s => s.Amount * portfolio.RatesToEur[s.Currency]).Sum()
                });

                return string.Join("; ", stockSummary.Select(s => $"{s.Currency}: {Math.Round(s.Amount, 2)}"));
            } catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public IEnumerable<string> GetAllRecommendations(PortfolioState portfolio)
        {
            var recommendations = GetRecommendations(portfolio);
            if (recommendations.Any())
            {
                yield return "Recommendations for today:";
                foreach (var r in recommendations)
                {
                    yield return r;
                }
            }
            else
            {
                yield return "Now all in the defined range.";
            }

            var marks = GetMarksStatus(portfolio);
            if (!string.IsNullOrEmpty(marks))
            {
                yield return marks;
            }

            yield return "TOTALS: " + GetSummary(portfolio);
        }


        public string GetRatesMessage(PortfolioState portfolio)
        {
            var rates = portfolio.RatesToEur.Keys
                .Where(k => k != "EUR")
                .Select(k => $"{k}: {Math.Round(portfolio.RatesToEur[k], 2)}");
            return $"FxRates: {string.Join(", ", rates)}.";
        }
    }
}
