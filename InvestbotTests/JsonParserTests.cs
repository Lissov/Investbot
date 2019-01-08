using Investbot.BusinessLogic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InvestbotTests
{
    [TestClass]
    public class JsonParserTests
    {
        [TestMethod]
        public void TestSimple()
        {
            dynamic res = JsonParser.Parse("{name:value}");
            Assert.IsNotNull(res);
            Assert.AreEqual("value", res.name);
        }

        [TestMethod]
        public void TestSimple2()
        {
            dynamic res = JsonParser.Parse(@"
            {
                ""authorized"": true
            }
            ");
            Assert.IsNotNull(res);
            Assert.IsTrue(res.authorized);
        }

        [TestMethod]
        public void TestSeveralParams()
        {
            dynamic res = JsonParser.Parse(@"
            {
                name1: 1,
                name2: ""2""
            }
            ");
            Assert.IsNotNull(res);
            Assert.AreEqual(1, res.name1);
            Assert.AreEqual("2", res.name2);
        }

        [TestMethod]
        public void Nested()
        {
            dynamic res = JsonParser.Parse(@"
            {
                name1: 1,
                subObject: {
                    so1: ""value of so1"",
                    so2: false
                }
            }
            ");
            Assert.IsNotNull(res);
            Assert.AreEqual(1, res.name1);
            Assert.IsNotNull(res.subObject);
            Assert.AreEqual("value of so1", res.subObject.so1);
            Assert.IsFalse(res.subObject.so2);
        }

        [TestMethod]
        public void FxRate()
        {
            dynamic res = JsonParser.Parse("{ \"fxRates\": [ { \"id\": 0, \"lastUpdated\": \"2019-01-07T21:03:42.4937006+01:00\", \"fromCcy\": \"USD\", \"toCcy\": \"EUR\", \"rate\": 0.8737439930100480559196155526 } ] }");
            Assert.IsNotNull(res);
            Assert.IsNotNull(res.fxRates);
            Assert.AreEqual(0.8737439930100480559196155526, res.fxRates[0].rate);
        }
    }
}
