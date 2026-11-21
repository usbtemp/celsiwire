namespace Configuration
{
    class Logging
    {
        public static readonly DefaultAndCustom<TimeInterval> Interval;
        public static readonly DefaultAndCustom<CustomValue> RecordsPerFile;
        public static string FileNamePattern;
        public static string LogDirectory;
        public static bool NewFilePerDay;
        public static bool LogToFile = true;
        /* Lines that cannot be written to the log folder go to the local AppData folder when more than MaxWaitingLines wait, and when logging stops. */
        public static bool WaitingToAppData = true;
        public static int MaxWaitingLines = 1000;
        public const int MinWaitingLinesLimit = 10;
        public const int MaxWaitingLinesLimit = 1000000;

        static Logging()
        {
            Interval = new DefaultAndCustom<TimeInterval>(Consts.Default.Interval, System.Array.IndexOf(Consts.Default.Interval, 5));
            RecordsPerFile = new DefaultAndCustom<CustomValue>(Consts.Default.RecordsPerFile, 2);
            FileNamePattern = Utils.FileName.Presets[0];
            LogDirectory = Utils.Path.DefaultLogDirectory;
        }
    }
}
