using System;

namespace Timex
{
    public class Timex
    {
        public DateTime? Value { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public TimeSpan? Interval { get; set; }
    }
}
