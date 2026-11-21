using System;

namespace Utils
{
    static class Log
    {
        private const long MAX_FILE_SIZE = 1024 * 1024;
        private static readonly object fileLock = new object();

        public static string FilePath
        {
            get { return System.IO.Path.Combine(Path.AppDataDirectory, Consts.App.NAME.ToLowerInvariant() + ".log"); }
        }

        public static void Debug(string format, params object[] args)
        {
            Write("DEBUG", format, args);
        }

        public static void Info(string format, params object[] args)
        {
            Write("INFO", format, args);
        }

        public static void Warn(string format, params object[] args)
        {
            Write("WARN", format, args);
        }

        public static void Error(string format, params object[] args)
        {
            Write("ERROR", format, args);
        }

        private static void Write(string level, string format, object[] args)
        {
            string message = args == null || args.Length == 0 ? format : string.Format(format, args);
            string time = Formats.Timestamp(DateTime.Now);
            string line = string.Format("{0} {1,-5} {2}", time, level, message);
            Console.WriteLine(line);
            WriteToFile(line);
        }

        private static void WriteToFile(string line)
        {
            lock (fileLock)
            {
                try
                {
                    string path = FilePath;
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    System.IO.FileInfo info = new System.IO.FileInfo(path);
                    if (info.Exists && info.Length > MAX_FILE_SIZE)
                    {
                        string previous = System.IO.Path.ChangeExtension(path, ".1.log");
                        if (System.IO.File.Exists(previous))
                        {
                            System.IO.File.Delete(previous);
                        }
                        System.IO.File.Move(path, previous);
                    }
                    System.IO.File.AppendAllText(path, line + Environment.NewLine, System.Text.Encoding.UTF8);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
