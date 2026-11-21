using Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using static Configuration.ListExtensions;

namespace Controller
{
    public class Logging
    {
        private readonly static ManualResetEvent loggingStopped;
        private readonly static ManualResetEvent taskStopped;
        private readonly static System.Timers.Timer timer;
        private readonly static object timerLock = new object();
        private static bool tickMissed;
        private static DateTime taskStarted;
        public const int MaxIntervalSeconds = 86400;

        private static int measurements;
        private static Controller.LogFile logFile;
        private static bool writeFailed;
        private static string waitingFor;
        public static Data.MeasurePoint lastMeasurePoint;
        public static int Measurements { get { return measurements; } }

        public static string CurrentFile
        {
            get { return logFile?.CurrentFile; }
        }

        private readonly static List<Port> portList;
        private readonly static List<Configuration.Device> deviceList;

        static void ClosePort()
        {
            foreach (Port port in portList)
            {
                Adapter.Disconnect(port);
            }
            portList.Clear();
        }

        static List<Port> OpenPorts()
        {
            if (portList.Count > 0)
            {
                ClosePort();
            }
            deviceList.Clear();

            List<Port> missing = new List<Port>();
            foreach (Configuration.Device device in Configuration.Communication.GetDevices(typeof(owDevice.DS18B20), true))
            {
                if (portList.AppendIfNot(device.port) && !Adapter.Connect(device.port))
                {
                    missing.Add(device.port);
                }
                device.failing = missing.Contains(device.port);
                deviceList.Add(device);
            }
            return missing;
        }

        static Logging()
        {
            loggingStopped = new ManualResetEvent(true);
            taskStopped = new ManualResetEvent(true);

            portList = new List<Port>();
            deviceList = new List<Configuration.Device>();

            timer = new System.Timers.Timer(2000) { AutoReset = false };
            timer.Elapsed += OnTimer;
        }

        private static void OnTimer(object sender, ElapsedEventArgs e)
        {
            TimerTask();
        }

        private static void Repeat()
        {
            /* Poglej zahtevo za zaustavitev. */
            lock (timerLock)
            {
                if (!loggingStopped.WaitOne(0))
                {
                    timer.Start();
                }
            }
        }

        private static void TimerTask()
        {
            if (loggingStopped.WaitOne(0))
            {
                return;
            }
            lock (timerLock)
            {
                if (loggingStopped.WaitOne(0))
                {
                    return;
                }
                if (!taskStopped.WaitOne(0))
                {
                    if (!tickMissed)
                    {
                        Utils.Log.Warn("Measurement skipped: previous one still running after {0:F1} s, longer than the interval", (DateTime.Now - taskStarted).TotalSeconds);
                    }
                    tickMissed = true;
                    return;
                }
                taskStopped.Reset();
                taskStarted = DateTime.Now;
            }
            Repeat();

            int number = measurements + 1;
            try
            {
                Data.MeasurePoint currentMeasurePoint = new Data.MeasurePoint();
                List<Configuration.Device> readDevices = new List<Configuration.Device>();
                try
                {
                    PortRecovery.Run(portList, deviceList);
                    readDevices = Controller.Devices.ReadDevices(deviceList);
                    foreach (Configuration.Device device in readDevices)
                    {
                        currentMeasurePoint.Add(device);
                        Alarms.Check(device, currentMeasurePoint.Time);
                        device.stats.Add(device.Value, currentMeasurePoint.Time);
                    }
                }
                catch (Exception e)
                {
                    Utils.Log.Error("Reading sensors failed (measurement {0}): {1}", number, e);
                }
                foreach (Configuration.Device device in deviceList)
                {
                    if (!readDevices.Contains(device))
                    {
                        Alarms.ReadFailed(device, currentMeasurePoint.Time);
                    }
                }

                lastMeasurePoint = currentMeasurePoint;
                Controller.ChartHistory.Add(currentMeasurePoint);
                measurements++;
                WriteToFile(currentMeasurePoint);

                Display.Event.RaiseDeviceUpdated();
                if (measurements == 1 && waitingFor != null)
                {
                    Display.Event.RaiseMessageChanged(string.Format("Logging started, waiting for {0}", waitingFor));
                }
            }
            catch (Exception e)
            {
                Utils.Log.Error("Measurement {0} failed: {1}", number, e);
            }
            finally
            {
                lock (timerLock)
                {
                    taskStopped.Set();
                    if (tickMissed)
                    {
                        tickMissed = false;
                        Repeat();
                    }
                }
            }
        }

        private static void WriteToFile(Data.MeasurePoint mp)
        {
            if (logFile == null)
            {
                return;
            }
            string previousFile = logFile.CurrentFile;
            try
            {
                logFile.Write(mp);
            }
            catch (Exception e)
            {
                Utils.Log.Error(e.ToString());
                writeFailed = true;
                Display.Event.RaiseMessageChanged(string.Format("Cannot write {0}, {1} lines waiting: {2}", System.IO.Path.GetFileName(logFile.CurrentFile), logFile.Pending, e.Message),
                    string.Format("Cannot write {0}, {1} lines waiting: {2}", logFile.CurrentFile, logFile.Pending, e.Message));
                return;
            }

            string movedTo = logFile.TakeMoved(out int movedLines);
            if (movedTo != null)
            {
                Utils.Log.Warn("Cannot write {0}, {1} lines saved to {2}", logFile.CurrentFile, movedLines, movedTo);
                writeFailed = true;
                Display.Event.RaiseMessageChanged(string.Format("Cannot write {0}, {1} lines saved to {2}", System.IO.Path.GetFileName(logFile.CurrentFile), movedLines, movedTo),
                    string.Format("Cannot write {0}, {1} lines saved to {2}", logFile.CurrentFile, movedLines, movedTo));
                return;
            }

            if (writeFailed)
            {
                if (logFile.Pending > 0)
                {
                    return;
                }
                writeFailed = false;
                Display.Event.RaiseMessageChanged(string.Format("Writing to {0} resumed.", System.IO.Path.GetFileName(logFile.CurrentFile)),
                    string.Format("Writing to {0} resumed.", logFile.CurrentFile));
            }
            else if (logFile.CurrentFile != previousFile)
            {
                Display.Event.RaiseMessageChanged(string.Format("Logging to {0}", System.IO.Path.GetFileName(logFile.CurrentFile)),
                    string.Format("Logging to {0}", logFile.CurrentFile));
            }
        }

        private static List<owDevice.owDevice> GetOwDevices()
        {
            List<owDevice.owDevice> devices = new List<owDevice.owDevice>();
            foreach (Configuration.Device device in deviceList)
            {
                devices.Add(device.device);
            }
            return devices;
        }

        public static bool Check()
        {
            return Configuration.Communication.GetDevices(typeof(owDevice.DS18B20), true).GetEnumerator().MoveNext();
        }

        /* waitForPorts: start even if no adapter can be opened yet; port recovery opens them when they appear. */
        public static void Start(bool waitForPorts)
        {
            lastMeasurePoint = null;
            measurements = 0;

            double newInterval = 1000.0 * Math.Min(Configuration.Logging.Interval.Value, MaxIntervalSeconds);
            if (timer.Interval != newInterval)
            {
                timer.Enabled = true; /* Grd trik iz "https://docs.microsoft.com/en-us/dotnet/api/system.timers.timer.interval" */
                timer.Interval = newInterval;
                timer.Enabled = false;
                /* Pogoj se preverja, da je teh primerov čim manj. */
            }

            List<Port> missing = OpenPorts();

            if (deviceList.Count == 0 || (missing.Count == portList.Count && !waitForPorts))
            {
                ClosePort();
                deviceList.Clear();
                throw new InvalidOperationException("Could not open the port of any selected sensor.");
            }

            try
            {
                logFile = Configuration.Logging.LogToFile
                    ? new Controller.LogFile(Configuration.Logging.LogDirectory, Configuration.Logging.FileNamePattern, Configuration.Logging.RecordsPerFile.Value, Configuration.Logging.NewFilePerDay,
                        Configuration.Logging.WaitingToAppData, Configuration.Logging.MaxWaitingLines, GetOwDevices())
                    : null;
            }
            catch
            {
                ClosePort();
                throw;
            }
            writeFailed = false;
            PortRecovery.Start();
            List<string> missingNames = new List<string>();
            foreach (Port port in missing)
            {
                Utils.Log.Warn("{0}: not available at start, waiting for it", port);
                missingNames.Add(port.ToString());
            }
            waitingFor = missingNames.Count > 0 ? string.Join(", ", missingNames.ToArray()) : null;
            Alarms.Reset(deviceList);
            foreach (Configuration.Device device in deviceList)
            {
                device.stats.Reset();
            }

            if (logFile != null)
            {
                Utils.Log.Info("Logging started: interval {0} s, {1} sensor(s) on {2} port(s), folder {3}, file name pattern {4}, {5}", Configuration.Logging.Interval.Value, deviceList.Count, portList.Count, Configuration.Logging.LogDirectory, Configuration.Logging.FileNamePattern,
                    Configuration.Logging.NewFilePerDay ? "new file every day" : Configuration.Logging.RecordsPerFile.Value + " records per file");
            }
            else
            {
                Utils.Log.Info("Logging started: interval {0} s, {1} sensor(s) on {2} port(s), not logging to file", Configuration.Logging.Interval.Value, deviceList.Count, portList.Count);
            }
            Display.Event.RaiseLoggingStarted();

            tickMissed = false;
            loggingStopped.Reset();
            taskStopped.Set();

            TimerTask();

            /* Počakaj na zaustavitev. */
            loggingStopped.WaitOne();

            /* Počakaj na spostitev. */
            taskStopped.WaitOne();

            ClosePort();
            Utils.Log.Info("Logging stopped: {0} measurement(s)", measurements);

            if (logFile == null)
            {
                return;
            }
            try
            {
                logFile.Flush(true);
            }
            catch (Exception e)
            {
                throw new System.IO.IOException(string.Format("{0} lines could not be written to {1}: {2}", logFile.Pending, logFile.CurrentFile, e.Message), e);
            }
            string savedTo = logFile.TakeMoved(out int savedLines);
            if (savedTo != null)
            {
                Utils.Log.Warn("Cannot write {0}, {1} lines saved to {2}", logFile.CurrentFile, savedLines, savedTo);
                throw new System.IO.IOException(string.Format("Cannot write {0}, {1} lines saved to {2}", logFile.CurrentFile, savedLines, savedTo));
            }
        }

        public static void FlushPending()
        {
            Controller.LogFile file = logFile;
            if (file == null || file.Pending == 0)
            {
                return;
            }
            try
            {
                int lines = file.Pending;
                file.Flush(true);
                string savedTo = file.TakeMoved(out int savedLines);
                if (savedTo != null)
                {
                    Utils.Log.Warn("Cannot write {0}, {1} lines saved to {2}", file.CurrentFile, savedLines, savedTo);
                    return;
                }
                Utils.Log.Info("{0} waiting line(s) written to {1}", lines, file.CurrentFile);
            }
            catch (Exception e)
            {
                Utils.Log.Error("Cannot write {0} waiting line(s) to {1}: {2}", file.Pending, file.CurrentFile, e.Message);
            }
        }

        public static void Stop()
        {
            lock (timerLock)
            {
                timer.Stop();
                loggingStopped.Set(); /* Podaj zahtevo za ustavitev. */
            }
            Utils.Log.Info("Logging stop requested");
        }

        public static bool WaitForMeasurement(int milliseconds)
        {
            return taskStopped.WaitOne(milliseconds);
        }

        public static bool IsRunning()
        {
            return !loggingStopped.WaitOne(0);
        }
    }
}
