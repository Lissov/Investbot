using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Timex.Tests
{
    [TestClass]
    public class TimexParserTests
    {
        private Timex ParseAndParse(string json)
        {
            dynamic obj = JsonParser.Parse(json);
            if (!TimexParser.TryParse(obj, out Timex timex))
                throw new Exception("Not parsed");
            return timex;
        }

        [TestMethod]
        public void DateExtended()
        {
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""datetime"", ""timex"": [ ""2018-04-05"" ] } ]}");
            Assert.AreEqual(new DateTime(2018, 04, 05), result.Value);
        }

        [TestMethod]
        public void DateBasic()
        {
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""datetime"", ""timex"": [ ""20190405"" ] } ]}");
            Assert.AreEqual(new DateTime(2019, 04, 05), result.Value);
        }

        [TestMethod]
        public void DateWithOmitting()
        {
            Assert.Inconclusive();
        }

        [TestMethod]
        public void WeekDate()
        {
            Assert.Inconclusive();
        }

        [TestMethod]
        public void DateOrdinal()
        {
            Assert.Inconclusive("Not implemented");
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""datetime"", ""timex"": [ ""1981-095"" ] } ]}");
            Assert.AreEqual(new DateTime(1981, 04, 05), result.Value);
        }

        [TestMethod]
        public void DateOrdinalBasic()
        {
            Assert.Inconclusive("Not implemented");
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""datetime"", ""timex"": [ ""1981095"" ] } ]}");
            Assert.AreEqual(new DateTime(1981, 04, 05), result.Value);
        }

        [TestMethod]
        public void TimeExtended()
        {
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""time"", ""timex"": [ ""13:47:30"" ] } ]}");
            Assert.AreEqual(DateTime.Today + new TimeSpan(0, 13, 47, 30), result.Value);
        }

        [TestMethod]
        public void ParseDateTime()
        {
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""datetime"", ""timex"": [ ""2019-01-05T18:30"" ] } ]}");
            Assert.AreEqual(new DateTime(2019, 01, 05, 18, 30, 00), result.Value);
        }

        [TestMethod]
        public void ParseDateTimeZ()
        {
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""datetime"", ""timex"": [ ""2019-02-11T23:20:57Z"" ] } ]}");
            Assert.AreEqual(DateTime.SpecifyKind(new DateTime(2019, 02, 11, 23, 20, 57), DateTimeKind.Utc), result.Value);
        }

        [TestMethod]
        public void ParseDateTimeBasic()
        {
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""datetime"", ""timex"": [ ""20190506T134730"" ] } ]}");
            Assert.AreEqual(new DateTime(2019, 05, 06, 13, 47, 30), result.Value);
        }

        [TestMethod]
        public void Midnight()
        {
            Assert.Inconclusive();
        }

        [TestMethod]
        public void TimeZone()
        {
            Assert.Inconclusive();
        }

        [TestMethod]
        public void Period()
        {
            Assert.Inconclusive("Parsing period in years not implemented");
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""set"", ""timex"": [ ""P3Y6M4DT12H30M5S"" ] } ]}");
            //Assert.AreEqual(new TimeSpan(3, 6, 4, 12, 47, 30), result.Interval);
        }

        [TestMethod]
        public void PeriodSimple()
        {
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""set"", ""timex"": [ ""P4DT12H30M5S"" ] } ]}");
            Assert.AreEqual(new TimeSpan(4, 12, 30, 5), result.Interval);
        }

        [TestMethod]
        public void TimeInterval()
        {
            Assert.Inconclusive();
        }

        [TestMethod]
        public void RepeatingInterval()
        {
            Assert.Inconclusive();
        }

        [TestMethod]
        public void DateRange()
        {
            var result = ParseAndParse(
                @"{""datetime"": [ {""type"": ""daterange"", ""timex"": [ ""(2019-01-22,2019-01-29,P6.0757884607662D)"" ] } ]}");
            Assert.AreEqual(new DateTime(2019, 01, 22), result.Start);
            Assert.AreEqual(new DateTime(2019, 01, 29), result.End);
        }

        [TestMethod]
        public void ParseComplete()
        {
            var result = ParseAndParse(@"
{""datetime"": [
  {
    ""type"": ""set"",
    ""timex"": [
      ""P1D""
    ]
  },
  {
    ""type"": ""daterange"",
    ""timex"": [
      ""(2019-01-22,XXXX-01-29,P6.0757884607662D)""
    ]
  },
  {
    ""type"": ""time"",
    ""timex"": [
      ""T16""
    ]
  }
]}");
            Assert.AreEqual(new DateTime(2019, 01, 22, 16, 00, 00), result.Start);
            Assert.AreEqual(new DateTime(2020, 01, 29, 16, 00, 00), result.End);
            Assert.AreEqual(new TimeSpan(1, 0, 0, 0), result.Interval); // 1 day
        }

        [TestMethod]
        public void RealCase1()
        {
            var result = ParseAndParse(@"{""datetime"": [  {    ""type"": ""set"",    ""timex"": [      ""PT5M""    ]  }]}");
            Assert.AreEqual(new TimeSpan(0, 0, 5, 0), result.Interval); // 5 minutes
        }
    }
}
