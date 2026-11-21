using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Controller
{
    public class LogFile
    {
        private static readonly TimeSpan FLUSH_INTERVAL = TimeSpan.FromMinutes(1);
        private const int FLUSH_RECORDS = 20;

        private readonly string directory;
        private readonly string pattern;
        private readonly int recordsPerFile;
        private readonly List<owDevice.owDevice> columns;
        private readonly NumberFormatInfo numberFormat;
        private readonly string separator;
        private readonly Encoding encoding;

        private readonly List<KeyValuePair<string, string>> pending;
        private readonly Dictionary<string, string> fallbackFiles = new Dictionary<string, string>();
        private string movedTo;
        private int movedLines;

        private int recordsInFile;
        private int fileNumber;
        private const int MAX_FILE_NUMBER = 99999;
        private DateTime lastFlush;
        private int recordsSinceFlush;
        private readonly bool newFilePerDay;
        /* toAppData: lines that cannot be written go to the local AppData folder when more than maxPending wait, and at the end. */
        private readonly bool toAppData;
        private readonly int maxPending;
        private DateTime fileDate;

        public string CurrentFile { get; private set; }
        public int Pending { get { lock (sync) { return pending.Count; } } }

        private readonly object sync = new object();

        public LogFile(string directory, string pattern, int recordsPerFile, bool newFilePerDay, bool toAppData, int maxPending, IEnumerable<owDevice.owDevice> devices)
        {
            this.newFilePerDay = newFilePerDay;
            this.toAppData = toAppData;
            this.maxPending = maxPending;
            if (directory == null || directory.Trim().Length == 0)
            {
                throw new ArgumentException("No folder for log files is set.");
            }
            if (!Utils.FileName.IsValid(pattern))
            {
                throw new ArgumentException("Invalid file name pattern.");
            }
            if (recordsPerFile < 1)
            {
                throw new ArgumentException("Records per file must be at least 1.");
            }

            this.directory = directory.Trim();
            Directory.CreateDirectory(this.directory);

            this.pattern = pattern;
            this.recordsPerFile = recordsPerFile;
            columns = new List<owDevice.owDevice>(devices);

            CultureInfo culture = CultureInfo.CurrentCulture;
            separator = culture.TextInfo.ListSeparator;
            if (separator == culture.NumberFormat.NumberDecimalSeparator)
            {
                separator = ";";
            }
            numberFormat = Utils.Formats.ValueFormat();
            encoding = new UTF8Encoding(false);

            pending = new List<KeyValuePair<string, string>>();
            recordsInFile = 0;
            fileNumber = 0;
            CurrentFile = null;
            lastFlush = DateTime.MinValue;
        }

        public void Write(Data.MeasurePoint mp)
        {
            lock (sync)
            {
                if (CurrentFile == null || (newFilePerDay ? mp.Time.Date != fileDate : recordsInFile >= recordsPerFile))
                {
                    NextFile(mp.Time);
                }
                pending.Add(new KeyValuePair<string, string>(CurrentFile, FormatRow(mp)));
                recordsInFile++;
                recordsSinceFlush++;

                DateTime now = DateTime.UtcNow;
                if (now - lastFlush >= FLUSH_INTERVAL || recordsSinceFlush >= FLUSH_RECORDS)
                {
                    lastFlush = now;
                    recordsSinceFlush = 0;
                    Flush(false);
                }
            }
        }

        /* final: logging is ending, so lines that cannot be written go to the local AppData folder however few they are. */
        public void Flush(bool final)
        {
            lock (sync)
            {
                bool moveIfFailing = toAppData && (final || pending.Count > maxPending);
                while (pending.Count > 0)
                {
                    string path = pending[0].Key;
                    StringBuilder sb = new StringBuilder();
                    int n = 0;
                    while (n < pending.Count && pending[n].Key == path)
                    {
                        sb.Append(pending[n].Value).Append("\r\n");
                        n++;
                    }
                    try
                    {
                        Directory.CreateDirectory(directory);
                        File.AppendAllText(path, sb.ToString(), encoding);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)
                    {
                        if (!moveIfFailing)
                        {
                            throw;
                        }
                        WriteFallback(path, sb.ToString(), pending[0].Value.StartsWith("Time" + separator, StringComparison.Ordinal));
                        movedTo = fallbackFiles[path];
                        movedLines += n;
                    }
                    pending.RemoveRange(0, n);
                }
            }
        }

        /* The lines of one log file go to a file of the same name in the local AppData folder, with the header first. */
        private void WriteFallback(string path, string text, bool hasHeader)
        {
            if (!fallbackFiles.TryGetValue(path, out string fallback))
            {
                string folder = Utils.Path.LocalAppDataDirectory;
                Directory.CreateDirectory(folder);
                string name = Path.GetFileName(path);
                fallback = Path.Combine(folder, name);
                for (int k = 2; File.Exists(fallback); k++)
                {
                    fallback = Path.Combine(folder, string.Format("{0}_{1}{2}", Path.GetFileNameWithoutExtension(name), k, Path.GetExtension(name)));
                }
                if (!hasHeader)
                {
                    text = FormatHeader() + "\r\n" + text;
                }
                fallbackFiles[path] = fallback;
            }
            File.AppendAllText(fallback, text, encoding);
        }

        /* The file that waiting lines were last moved to and how many, once; null when none were moved since the last call. */
        public string TakeMoved(out int lines)
        {
            lock (sync)
            {
                string file = movedTo;
                lines = movedLines;
                movedTo = null;
                movedLines = 0;
                return file;
            }
        }

        /* %n starts at 001 every day and skips numbers whose file already exists (e.g. after a restart). */
        private void NextFile(DateTime time)
        {
            if (time.Date != fileDate)
            {
                fileNumber = 0;
            }
            fileDate = time.Date;
            recordsInFile = 0;
            bool numbered = Utils.FileName.Format(pattern, time, 1) != Utils.FileName.Format(pattern, time, 2);
            string path;
            do
            {
                fileNumber++;
                path = Path.Combine(directory, Utils.FileName.Format(pattern, time, fileNumber));
            } while (numbered && IsTaken(path) && fileNumber < MAX_FILE_NUMBER);
            CurrentFile = UniquePath(Utils.FileName.Format(pattern, time, fileNumber));
            pending.Add(new KeyValuePair<string, string>(CurrentFile, FormatHeader()));
        }

        private bool IsTaken(string path)
        {
            if (File.Exists(path))
            {
                return true;
            }
            foreach (KeyValuePair<string, string> entry in pending)
            {
                if (entry.Key == path)
                {
                    return true;
                }
            }
            return false;
        }

        private string UniquePath(string name)
        {
            string path = Path.Combine(directory, name);
            string stem = Path.GetFileNameWithoutExtension(name);
            string extension = Path.GetExtension(name);
            for (int k = 2; IsTaken(path); k++)
            {
                path = Path.Combine(directory, string.Format("{0}_{1}{2}", stem, k, extension));
            }
            return path;
        }

        private string FormatHeader()
        {
            List<string> cells = new List<string>() { "Time" };
            foreach (owDevice.owDevice device in columns)
            {
                string rom = owAlgorithms.StringByte.ToHex(device.rom.id);
                string name = Configuration.Communication.GetDisplayName(device);
                cells.Add(Quote(name == rom ? rom : string.Format("{0} ({1})", name, rom)));
            }
            return string.Join(separator, cells.ToArray());
        }

        private string Quote(string cell)
        {
            if (cell.Contains(separator) || cell.Contains("\"") || cell.Contains("\n") || cell.Contains("\r"))
            {
                return "\"" + cell.Replace("\"", "\"\"") + "\"";
            }
            return cell;
        }

        private string FormatRow(Data.MeasurePoint mp)
        {
            List<string> cells = new List<string>() { Utils.Formats.Timestamp(mp.Time) };
            foreach (owDevice.owDevice device in columns)
            {
                cells.Add(mp.Data.TryGetValue(device, out float value) ? Utils.Formats.Value(value, numberFormat) : string.Empty);
            }
            return string.Join(separator, cells.ToArray());
        }
    }
}
