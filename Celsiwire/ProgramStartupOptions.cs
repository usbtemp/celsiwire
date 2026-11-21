using System;

namespace Program
{
    static class StartupOptions
    {
        public static bool AutoStart;
        public static string ConfigPath;
        private static readonly char[] Separators = { '=', ':' };

        public static void Parse(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Length < 2 || (arg[0] != '/' && arg[0] != '-'))
                {
                    Utils.Log.Warn("Unknown command-line argument: {0}", arg);
                    continue;
                }
                string name = arg.Substring(1);
                string value = null;
                int separator = name.IndexOfAny(Separators);
                if (separator > 0)
                {
                    value = name.Substring(separator + 1);
                    name = name.Substring(0, separator);
                }
                if (string.Equals(name, "autostart", StringComparison.OrdinalIgnoreCase))
                {
                    AutoStart = true;
                }
                else if (string.Equals(name, "config", StringComparison.OrdinalIgnoreCase))
                {
                    if (value == null && i + 1 < args.Length)
                    {
                        value = args[++i];
                    }
                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            ConfigPath = System.IO.Path.GetFullPath(value);
                        }
                        catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is System.IO.PathTooLongException || e is System.Security.SecurityException)
                        {
                            Utils.Log.Error("Invalid configuration path \"{0}\": {1}", value, e.Message);
                        }
                    }
                }
                else
                {
                    Utils.Log.Warn("Unknown command-line option: {0}", arg);
                }
            }
        }
    }
}
