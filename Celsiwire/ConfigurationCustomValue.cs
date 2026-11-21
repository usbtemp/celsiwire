using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Configuration
{
    public class CustomValue
    {
        public int value;
        public string format;

        public override string ToString()
        {
            return string.Format(format, value);
        }

        public override int GetHashCode()
        {
            return value;
        }

        public override bool Equals(object obj)
        {
            return obj != null && GetType().Equals(obj.GetType()) && value == ((CustomValue)obj).value;
        }

        /* Only a whole number, so "-5", "1.5" or "abc30" are rejected instead of read as 5, 1 or 30. */
        public virtual bool TryParse(string text, out int result)
        {
            Match match = Regex.Match(text, @"^\s*(\d{1,9})\s*$");
            result = 0;
            return match.Success && int.TryParse(match.Groups[1].Value, out result);
        }

        public CustomValue()
        {
            value = 0;
            format = "{0}";
        }
    }

    public class TimeInterval : CustomValue
    {
        private static readonly Regex Pattern = new Regex(@"^\s*(\d{1,9})\s*(s|secs?|seconds?|m|mins?|minutes?|h|hours?)?\s*$", RegexOptions.IgnoreCase);

        /* A number with an optional unit: "90", "90 s", "5 min", "2 hours". The value is in seconds. */
        public override bool TryParse(string text, out int result)
        {
            result = 0;
            Match match = Pattern.Match(text);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int number))
            {
                return false;
            }
            string unit = match.Groups[2].Value.ToLowerInvariant();
            int factor = unit.StartsWith("h", System.StringComparison.Ordinal) ? 3600 : unit.StartsWith("m", System.StringComparison.Ordinal) ? 60 : 1;
            if (number > int.MaxValue / factor)
            {
                return false;
            }
            result = number * factor;
            return true;
        }

        public TimeInterval() : base()
        {
            format = "{0} secs";
        }

        public override string ToString()
        {
            if (value > 0 && value % 3600 == 0)
            {
                return Utils.Formats.Plural(value / 3600, "hour");
            }
            if (value > 0 && value % 60 == 0)
            {
                return Utils.Formats.Plural(value / 60, "min");
            }
            return Utils.Formats.Plural(value, "sec");
        }
    }

    class DefaultAndCustom<T> where T : CustomValue, new()
    {
        public T currentValue, customValue;
        public List<T> CustomValues { get; }

        public int Value { get { return (currentValue ?? CustomValues[0]).value; } }

        public DefaultAndCustom(int[] startUpValues, int index)
        {
            CustomValues = new List<T>();
            foreach (int n in startUpValues)
            {
                T currentValue = new T() { value = n };
                CustomValues.Add(currentValue);
            }

            currentValue = index < 0 || index >= startUpValues.Length ? CustomValues[0] : CustomValues[index];
            customValue = null;
        }

        public void SetValue(int value)
        {
            T match = null;
            foreach (T candidate in CustomValues)
            {
                if (candidate.value == value)
                {
                    match = candidate;
                    break;
                }
            }
            if (match == null)
            {
                customValue = new T() { value = value };
                match = customValue;
            }
            currentValue = match;
        }
    }
}
