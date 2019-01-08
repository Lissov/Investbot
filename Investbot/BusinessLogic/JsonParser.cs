using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace Investbot.BusinessLogic
{
    public class JsonParser
    {
        public static dynamic Parse(string json)
        {
            /*json = json
                .Replace("\r", "")
                .Replace("\n", " ")
                .Replace("\n", " ")*/
            json = json.Trim();
            if (json[0] != '{' || json[json.Length-1] != '}')
            {
                throw new ArgumentException("Unparseable JSON");
            }

            return ParseFrom(json, 1).Item1;
        }

        private static char[] quote = { '"' };
        private static char[] delimiters = {':'};
        private static char[] spaces = { '\t', '\r', '\n', ' ' };
        private static char[] ending = { ',', '}' };
        private static char[] non_identifier = delimiters
                                                .Union(spaces)
                                                .Union(ending)
                                            .ToArray();
        private static Tuple<dynamic, int> ParseFrom(string json, int index)
        {
            DynObject result = new DynObject();

            SkipSpaces(json, ref index);
            while (json[index] != '}')
            {
                if (json[index] == ',') index++;
                SkipSpaces(json, ref index);

                var prop = ParseValue(json, ref index);
                result.SetMember(prop.Item1, prop.Item2);
                SkipSpaces(json, ref index);
            }

            return new Tuple<dynamic, int>(result, index);
        }

        private static void SkipSpaces(string json, ref int index)
        {
            while (spaces.Contains(json[index])) index++;
        }

        private static Tuple<string, object> ParseValue(string json, ref int index)
        {
            string name;
            if (json[index] == '"')
            {
                index = ParseTill(json, index + 1, quote, out name) + 1;
            }
            else
            {
                index = ParseTill(json, index, non_identifier, out name);
            }

            while (spaces.Contains(json[index])) index++;
            if (json[index] == ':') index++;
            while (spaces.Contains(json[index])) index++;
            object value;

            switch (json[index])
            {
                case '{':
                    var vObj = ParseFrom(json, index + 1);
                    value = vObj.Item1;
                    index = vObj.Item2 + 1;
                    SkipTillEnding(json, ref index);
                    break;
                case '[':
                    var dynArray = new DynObject();
                    dynArray.SetArray();
                    var braces = new[] {'{', ']'};
                    int arrayIndex = 0;
                    do
                    {
                        while (!braces.Contains(json[index])) index++;
                        if (json[index] == '{')
                        {
                            var arrayVal = ParseFrom(json, index + 1);
                            var arrayObj = arrayVal.Item1;
                            index = arrayVal.Item2 + 1;
                            dynArray.PushArrayValue(arrayObj);
                        }
                    } while (json[index] != ']');
                    index++;
                    value = dynArray;
                    break;
                case '"':
                    index = ParseTill(json, index + 1, quote, out var v) + 1;
                    value = v;  // no parse in quotes
                    SkipTillEnding(json, ref index);
                    break;
                default:
                    index = ParseTill(json, index, non_identifier, out var v1);
                    value = ParseValue(v1);
                    SkipTillEnding(json, ref index);
                    break;
            }

            return new Tuple<string, object>(name, value);
        }

        private static void SkipTillEnding(string json, ref int index)
        {
            while (!ending.Contains(json[index])) index++;
        }

        private static int ParseTill(string json, int i, char[] separators, out string name)
        {
            var end = i + 1;
            while (!separators.Contains(json[end])) end++;
            name = json.Substring(i, end - i);
            return end;
        }

        private static object ParseValue(string value)
        {
            if (Boolean.TryParse(value, out var valBool))
                return valBool;
            if (int.TryParse(value, out var valInt))
                return valInt;
            if (double.TryParse(value, out var valDouble))
                return valDouble;
            if (DateTime.TryParse(value, out var valDate))
                return valDate;
            return value;
        }

        public class DynObject : DynamicObject
        {
            private Dictionary<string, object> values = new Dictionary<string, object>();
            private List<object> arrayValues = null;

            public override bool TryGetMember(GetMemberBinder binder, out object result)
            {
                return values.TryGetValue(binder.Name, out result);
            }

            public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
            {
                result = arrayValues[(int)indexes[0]];
                return true;
            }

            public void SetMember(string name, object value)
            {
                values[name] = value;
            }

            public void SetArray()
            {
                arrayValues = new List<object>();
                values["Length"] = 0;
            }

            public void PushArrayValue(object value)
            {
                arrayValues.Add(value);
                values["Length"] = arrayValues.Count;
            }
        }
    }
}
