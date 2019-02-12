using System;
using System.Collections;
using System.Globalization;

namespace Timex
{
    public class TimexParser
    {
        public static bool TryParse(dynamic value, out Timex result)
        {
            try
            {
                result = null;
                TimeSpan? timeOffset = null;
                DateTimeEx dateValue = null;
                DateTimeEx dateStart = null;
                DateTimeEx dateEnd = null;
                var array = (value is IEnumerable)
                    ? value
                    : value.datetime;
                foreach (var part in array)
                {
                    switch (part.type.ToString())
                    {
                        case "datetime":
                            var timex = part.timex[0].ToString();
                            if (!TryParseExactDate(timex, out dateValue)) return false;
                            break;
                        case "daterange":
                            var timexRange = part.timex[0];
                            var parts = timexRange.ToString()
                                .Split(new[] {'(', ',', ')'});
                            if (parts.Length < 4) return false;
                            var timexFrom = parts[1];
                            var timexTo = parts[2];
                            var p1 = TryParseExactDate(timexFrom, out dateStart);
                            var p2 = TryParseExactDate(timexTo, out dateEnd);
                            if (!p1 || !p2) return false;
                            break;
                        case "set":
                            var timexSet = part.timex[0];
                            if (!TryParsePeriod(timexSet.ToString(), out TimeSpan period)) return false;
                            result = result ?? new Timex();
                            result.Interval = period;
                            break;
                        case "time":
                            var timexTime = part.timex[0].ToString();
                            if (timexTime[0] == 'T') timexTime = timexTime.Substring(1);
                            if (!DateTime.TryParseExact(timexTime, new[] {"HH", "HHmm", "HH:mm", "HHmmSS", "HH:mm:SS" },
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                            {
                                return false;
                            }

                            timeOffset = new TimeSpan(parsedTime.Hour, parsedTime.Minute, parsedTime.Second);
                            break;
                    }
                }

                if (dateValue != null)
                {
                    result = result ?? new Timex();
                    result.Value = dateValue.Value;
                }
                if (dateStart != null)
                {
                    result = result ?? new Timex();
                    result.Start = dateStart.Value;
                }
                if (dateEnd != null)
                {
                    result = result ?? new Timex();
                    result.End = dateEnd.Value;
                }
                if (timeOffset != null)
                {
                    result = result ?? new Timex();
                    if (result.Start == null && result.End == null)
                    {
                        result.Value = result.Value ?? DateTime.Today;
                    }
                    if (result.Value != null)
                        result.Value += timeOffset;
                    if (result.Start != null)
                        result.Start += timeOffset;
                    if (result.End != null)
                        result.End += timeOffset;
                }

                var r = result;
                AdjustDateToAfterToday(() => r.Value, (v) => r.Value = v, dateValue);
                AdjustDateToAfterToday(() => r.Start, (v) => r.Start = v, dateStart);
                AdjustDateToAfterToday(() => r.End, (v) => r.End = v, dateEnd);

                return result != null;
            }
            catch (Exception ex)
            {
                result = null;
                return false;
            }
        }

        private static void AdjustDateToAfterToday(Func<DateTime?> getter, Action<DateTime> setter, DateTimeEx dateValue)
        {
            if (getter() != null && dateValue?.IsPattern == true)
            {
                while (getter() < DateTime.Now)
                    setter(AdjustDate(getter().Value, dateValue));
            }
        }

        private static DateTime AdjustDate(DateTime dateTime, DateTimeEx patterns)
        {
            if (patterns.IsDayPattern)
            {
                var res = dateTime.AddDays(1);
                if (res.Month != dateTime.Month && !patterns.IsMonthPattern && patterns.IsYearPattern)
                {
                    res = dateTime.AddYears(1);
                }

                return res;
            }

            if (patterns.IsMonthPattern)
                return dateTime.AddMonths(1);

            if (patterns.IsYearPattern)
                return dateTime.AddYears(1);

            throw new ArgumentException("Adjust impossible");
        }

        private static bool TryParseExactDate(string timex, out DateTimeEx parsed)
        {
            parsed = new DateTimeEx();
            var basic = timex.Replace("-", "").Replace(":", "");
            if (basic.Substring(0, 4).ToUpper() == "XXXX")
            {
                basic = DateTime.Today.Year.ToString() + basic.Substring(4);
                parsed.IsYearPattern = true;
            }
            if (basic.Substring(4, 2).ToUpper() == "XX")
            {
                var m = DateTime.Today.Month.ToString();
                if (m.Length == 1) m = "0" + m;
                basic = basic.Substring(0, 4) + m + basic.Substring(6);
                parsed.IsMonthPattern = true;
            }
            if (basic.Substring(6, 2).ToUpper() == "XX")
            {
                var d = DateTime.Today.Day.ToString();
                if (d.Length == 1) d = "0" + d;
                basic = basic.Substring(0, 6) + d;
                parsed.IsDayPattern = true;
            }

            if (!DateTime.TryParseExact(basic, new[]
                { "yyyyMMdd", "yyyyMMddTHHmm", "yyyyMMddTHHmmss",
                    "yyyyMMdd HHmm", "yyyyMMdd HHmmss" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                return false;
            };

            parsed.Value = parsedDate;
            return true;
        }

        private static bool TryParsePeriod(string timex, out TimeSpan parsed)
        {
            var timexPending = timex.ToUpper();

            parsed = new TimeSpan();
            var date = true;
            while (!string.IsNullOrEmpty(timexPending))
            {
                var i1 = timexPending.IndexOfAny(new[] { 'P', 'Y', 'M', 'D', 'T', 'H', 'M', 'S' });
                if (i1 == -1) return false;
                var v = i1 == 0 ? 0 : int.Parse(timexPending.Substring(0, i1));
                switch (timexPending[i1])
                {
                    case 'P': break;
                    case 'Y':
                        return false;
                    case 'M':
                        if (date)
                        {
                            return false;
                        }
                        else
                        {
                            parsed += new TimeSpan(0, 0, v, 0);
                        }
                        break;
                    case 'D':
                        parsed += new TimeSpan(v, 0, 0, 0);
                        break;
                    case 'T':
                        date = false;
                        break;
                    case 'H':
                        parsed += new TimeSpan(0, v, 0, 0);
                        break;
                    case 'S':
                        parsed += new TimeSpan(0, 0, 0, v);
                        break;
                }

                timexPending = timexPending.Substring(i1 + 1);
            }

            return true;
        }

        private class DateTimeEx
        {
            public DateTime Value { get; set; }
            public bool IsYearPattern = false;
            public bool IsMonthPattern = false;
            public bool IsDayPattern = false;

            public bool IsPattern => IsYearPattern || IsMonthPattern || IsDayPattern;
        }
    }
}
