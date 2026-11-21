using System;
using System.Globalization;

namespace Utils
{
    /* Text formats shared by the CSV file, the application log, the display and the clipboard. */
    static class Formats
    {
        public const string TIMESTAMP = "yyyy-MM-dd HH:mm:ss";
        private const string VALUE = "0.0###";

        public static string Timestamp(DateTime time)
        {
            return time.ToString(TIMESTAMP, CultureInfo.InvariantCulture);
        }

        /* Regional decimal sign, but always a plain minus (some cultures use U+2212, which Excel does not read). */
        public static NumberFormatInfo ValueFormat()
        {
            NumberFormatInfo format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
            format.NegativeSign = "-";
            return format;
        }

        /* A temperature with up to 4 decimals, as in the CSV file. */
        public static string Value(float value, NumberFormatInfo format)
        {
            return value.ToString(VALUE, format);
        }

        public static string Value(float value)
        {
            return Value(value, ValueFormat());
        }

        /* Time of day in status messages. */
        public static string Clock(DateTime time)
        {
            return time.ToString("HH:mm:ss");
        }

        /* Day, date and time of day, e.g. "Mon 3.11. 14:05", with English day names. */
        public static string DayClock(DateTime time)
        {
            return time.ToString("ddd d.M. HH:mm", CultureInfo.InvariantCulture);
        }

        /* Date and time in the Windows short date and long time format. */
        public static string DateAndTime(DateTime time)
        {
            return time.ToShortDateString() + " " + time.ToLongTimeString();
        }

        public static string Plural(int count, string word)
        {
            return count == 1 ? "1 " + word : count + " " + word + "s";
        }
    }
}
