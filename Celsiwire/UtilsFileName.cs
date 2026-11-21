using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Utils
{
    public static class FileName
    {
        public static readonly string[] Presets = {
            Consts.App.NAME + "_%Y%m%d_%H%M%S.csv",
            Consts.App.NAME + "_%F_%T.csv",
            Consts.App.NAME + "_%F_%n.csv",
            "%F_%H%M.csv",
        };

        private static readonly string[,] Tokens = {
            { "%Y", "year (2026)" },
            { "%y", "year, 2 digits (26)" },
            { "%m", "month (01-12)" },
            { "%d", "day of month (01-31)" },
            { "%j", "day of year (001-366)" },
            { "%H", "hour (00-23)" },
            { "%I", "hour (01-12)" },
            { "%p", "AM or PM" },
            { "%M", "minute (00-59)" },
            { "%S", "second (00-59)" },
            { "%F", "date, same as %Y-%m-%d" },
            { "%T", "time, same as %H-%M-%S" },
            { "%u", "weekday number (1 = Monday)" },
            { "%s", "seconds since 1970-01-01 UTC" },
            { "%n", "file number (001, 002, ...)" },
            { "%%", "a literal %" },
        };

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static string Format(string pattern, DateTime time, int fileNumber)
        {
            if (pattern == null)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (c == '%' && i + 1 < pattern.Length)
                {
                    string value = Expand(pattern[i + 1], time, fileNumber);
                    if (value != null)
                    {
                        sb.Append(value);
                        i++;
                        continue;
                    }
                }
                sb.Append(c);
            }
            return Sanitize(sb.ToString());
        }

        public static bool IsValid(string pattern)
        {
            string name = Format(pattern, DateTime.Now, 1);
            return name.Length > 0 && name != "." && name != ".." && !IsReservedName(name);
        }

        private static readonly string[] ReservedNames = { "CON", "PRN", "AUX", "NUL" };

        private static bool IsReservedName(string name)
        {
            int dot = name.IndexOf('.');
            string stem = (dot >= 0 ? name.Substring(0, dot) : name).TrimEnd(' ').ToUpperInvariant();
            if (Array.IndexOf(ReservedNames, stem) >= 0)
            {
                return true;
            }
            return stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                && stem[3] >= '1' && stem[3] <= '9';
        }

        public static string Help()
        {
            StringBuilder sb = new StringBuilder("File name pattern tokens:");
            for (int i = 0; i < Tokens.GetLength(0); i++)
            {
                sb.AppendFormat("\n{0}   {1}", Tokens[i, 0], Tokens[i, 1]);
            }
            return sb.ToString();
        }

        private static string Expand(char code, DateTime t, int fileNumber)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            switch (code)
            {
                case 'Y': return t.ToString("yyyy", ci);
                case 'y': return t.ToString("yy", ci);
                case 'm': return t.ToString("MM", ci);
                case 'd': return t.ToString("dd", ci);
                case 'j': return t.DayOfYear.ToString("000", ci);
                case 'H': return t.ToString("HH", ci);
                case 'I': return t.ToString("hh", ci);
                case 'p': return t.ToString("tt", ci);
                case 'M': return t.ToString("mm", ci);
                case 'S': return t.ToString("ss", ci);
                case 'F': return t.ToString("yyyy-MM-dd", ci);
                case 'T': return t.ToString("HH-mm-ss", ci);
                case 'u': return (((int)t.DayOfWeek + 6) % 7 + 1).ToString(ci);
                case 's': return ((long)(t.ToUniversalTime() - UnixEpoch).TotalSeconds).ToString(ci);
                case 'n': return fileNumber.ToString("000", ci);
                case '%': return "%";
                default: return null;
            }
        }

        private static string Sanitize(string name)
        {
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '-' : c);
            }
            return sb.ToString().Trim().TrimEnd('.');
        }
    }
}
