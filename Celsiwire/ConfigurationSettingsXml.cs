using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Configuration
{
    class SettingsXml
    {
        public static bool canSave = true;

        private const int VERSION = 1;

        private class PortEntry
        {
            public string name;
            public bool enabled;
        }

        private class DeviceEntry
        {
            public owDevice.owROM rom;
            public string port;
            public string name;
            public bool enabled;
            public float? low;
            public float? high;
            public float offset;
        }

        private class AlarmOptions
        {
            public float hysteresis;
            public bool sound;
            public bool trayNotice;
        }

        private static void WriteAlarmOptions(XmlWriter writer)
        {
            writer.WriteStartElement("Alarms");
            writer.WriteAttributeString("hysteresis", XmlConvert.ToString(Alarms.Hysteresis));
            writer.WriteAttributeString("sound", XmlConvert.ToString(Alarms.PlaySound));
            writer.WriteAttributeString("trayNotice", XmlConvert.ToString(Alarms.TrayNotice));
            writer.WriteEndElement();
        }

        private static void WriteChart(XmlWriter writer)
        {
            writer.WriteStartElement("Chart");
            writer.WriteAttributeString("showAlarmLimits", XmlConvert.ToString(Chart.ShowAlarmLimits));
            writer.WriteEndElement();
        }

        /* Only a true/false value, so nothing can fail; a file without it keeps the current value. */
        private static bool ReadChartLimits(XmlElement root)
        {
            return ReadBool(root["Chart"], "showAlarmLimits", Chart.ShowAlarmLimits);
        }

        private static AlarmOptions ReadAlarmOptions(XmlElement root)
        {
            XmlElement element = root["Alarms"];
            AlarmOptions options = new AlarmOptions()
            {
                hysteresis = ReadFloat(element, "hysteresis") ?? Alarms.Hysteresis,
                sound = ReadBool(element, "sound", Alarms.PlaySound),
                trayNotice = ReadBool(element, "trayNotice", Alarms.TrayNotice),
            };
            if (options.hysteresis < 0 || options.hysteresis > 10)
            {
                throw new FormatException("Alarm hysteresis must be between 0 and 10 °C.");
            }
            return options;
        }

        private static void ApplyAlarmOptions(AlarmOptions options)
        {
            Alarms.Hysteresis = options.hysteresis;
            Alarms.PlaySound = options.sound;
            Alarms.TrayNotice = options.trayNotice;
        }

        private static float? ReadFloat(XmlElement element, string name)
        {
            if (element == null || !element.HasAttribute(name))
            {
                return null;
            }
            float value = XmlConvert.ToSingle(element.GetAttribute(name));
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new FormatException(string.Format("Invalid number in attribute \"{0}\".", name));
            }
            return value;
        }

        private static void WriteFile(string path, Action<XmlWriter> write)
        {
            string temp = path + ".tmp";
            try
            {
                using (XmlWriter writer = XmlWriter.Create(temp, WriterSettings()))
                {
                    write(writer);
                }
                if (!File.Exists(path))
                {
                    File.Move(temp, path);
                    return;
                }
                try
                {
                    File.Replace(temp, path, null);
                }
                catch (Exception e) when (e is IOException || e is PlatformNotSupportedException)
                {
                    File.Copy(temp, path, true);
                    File.Delete(temp);
                }
            }
            catch
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException)
                {
                }
                throw;
            }
        }

        public static string KeepBadSettings()
        {
            string bad = Path.ChangeExtension(SettingsPath, ".bad.xml");
            File.Copy(SettingsPath, bad, true);
            return bad;
        }

        private static bool LimitsValid(float? low, float? high)
        {
            return !(low.HasValue && high.HasValue && low.Value >= high.Value);
        }

        private static string CheckPortName(string name)
        {
            if (name.StartsWith(@"\\", StringComparison.Ordinal))
            {
                throw new FormatException(string.Format("Invalid port name \"{0}\".", name));
            }
            return name;
        }

        private static bool OffsetValid(float offset)
        {
            return offset >= -Device.MaxOffset && offset <= Device.MaxOffset;
        }

        private static void CheckLimits(string rom, float? low, float? high)
        {
            if (!LimitsValid(low, high))
            {
                throw new FormatException(string.Format("Sensor {0}: low alarm limit must be below the high limit.", rom));
            }
        }

        public static void Export(string path)
        {
            WriteFile(path, WriteConfiguration);
        }

        public static string Snapshot()
        {
            StringBuilder text = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(text, WriterSettings()))
            {
                WriteConfiguration(writer);
            }
            return text.ToString();
        }

        private static void WriteHeader(XmlWriter writer)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement(Consts.App.NAME);
            writer.WriteAttributeString("version", XmlConvert.ToString(VERSION));
            WriteLogging(writer);
            WriteAlarmOptions(writer);
            WriteChart(writer);
        }

        private static void WriteConfiguration(XmlWriter writer)
        {
            WriteHeader(writer);

            writer.WriteStartElement("Ports");
            foreach (Port port in Communication.GetPorts())
            {
                if (!port.enabled && !port.pendingEnable)
                {
                    continue;
                }
                writer.WriteStartElement("Port");
                writer.WriteAttributeString("name", port.ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("Devices");
            foreach (Device device in Communication.deviceList)
            {
                if (!device.enabled || device.port == null)
                {
                    continue;
                }
                writer.WriteStartElement("Device");
                writer.WriteAttributeString("rom", owAlgorithms.StringByte.ToHex(device.device.rom.id));
                writer.WriteAttributeString("port", device.port.ToString());
                if (!string.IsNullOrEmpty(device.Name))
                {
                    writer.WriteAttributeString("name", device.Name);
                }
                if (device.Low.HasValue)
                {
                    writer.WriteAttributeString("low", XmlConvert.ToString(device.Low.Value));
                }
                if (device.High.HasValue)
                {
                    writer.WriteAttributeString("high", XmlConvert.ToString(device.High.Value));
                }
                if (device.Offset != 0f)
                {
                    writer.WriteAttributeString("offset", XmlConvert.ToString(device.Offset));
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        public static string SettingsPath
        {
            get
            {
                return Path.Combine(Utils.Path.AppDataDirectory, "settings.xml");
            }
        }

        public static void SaveSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            WriteFile(SettingsPath, delegate (XmlWriter writer)
            {
                WriteHeader(writer);
                writer.WriteStartElement("SensorAlarms");
                Dictionary<string, float> lows = Communication.CopyLimits(Communication.lowLimits);
                Dictionary<string, float> highs = Communication.CopyLimits(Communication.highLimits);
                List<string> roms = new List<string>(lows.Keys);
                foreach (string rom in highs.Keys)
                {
                    if (!roms.Contains(rom))
                    {
                        roms.Add(rom);
                    }
                }
                foreach (string rom in roms)
                {
                    writer.WriteStartElement("Sensor");
                    writer.WriteAttributeString("rom", rom);
                    if (lows.TryGetValue(rom, out float limit))
                    {
                        writer.WriteAttributeString("low", XmlConvert.ToString(limit));
                    }
                    if (highs.TryGetValue(rom, out limit))
                    {
                        writer.WriteAttributeString("high", XmlConvert.ToString(limit));
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("SensorOffsets");
                foreach (KeyValuePair<string, float> entry in Communication.CopyLimits(Communication.offsets))
                {
                    writer.WriteStartElement("Sensor");
                    writer.WriteAttributeString("rom", entry.Key);
                    writer.WriteAttributeString("offset", XmlConvert.ToString(entry.Value));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("SensorNames");
                foreach (KeyValuePair<string, string> entry in Communication.sensorNames)
                {
                    writer.WriteStartElement("Sensor");
                    writer.WriteAttributeString("rom", entry.Key);
                    writer.WriteAttributeString("name", entry.Value);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            });
        }

        private static bool TryReadSensor(XmlElement element, out string rom)
        {
            rom = ReadString(element, "rom", string.Empty).Trim().ToUpperInvariant();
            return rom.Length == 2 * owDevice.owROM.LENGTH;
        }

        public static bool LoadSettings()
        {
            if (!File.Exists(SettingsPath))
            {
                return false;
            }
            XmlElement root = LoadRoot(SettingsPath);
            LoggingEntry logging = ReadLogging(root);
            AlarmOptions alarmOptions = ReadAlarmOptions(root);
            bool chartLimits = ReadChartLimits(root);

            Dictionary<string, float?[]> limits = new Dictionary<string, float?[]>();
            foreach (XmlElement element in Children(root, "SensorAlarms", "Sensor"))
            {
                try
                {
                    float? low = ReadFloat(element, "low");
                    float? high = ReadFloat(element, "high");
                    if (TryReadSensor(element, out string rom) && (low.HasValue || high.HasValue) && LimitsValid(low, high))
                    {
                        limits[rom] = new float?[] { low, high };
                    }
                }
                catch (Exception e) when (e is FormatException || e is OverflowException)
                {
                    Utils.Log.Warn("Settings: alarm limits skipped: {0}", e.Message);
                }
            }

            Dictionary<string, float> offsets = new Dictionary<string, float>();
            foreach (XmlElement element in Children(root, "SensorOffsets", "Sensor"))
            {
                try
                {
                    float? offset = ReadFloat(element, "offset");
                    if (TryReadSensor(element, out string rom) && offset.HasValue && offset.Value != 0f && OffsetValid(offset.Value))
                    {
                        offsets[rom] = offset.Value;
                    }
                }
                catch (Exception e) when (e is FormatException || e is OverflowException)
                {
                    Utils.Log.Warn("Settings: offset skipped: {0}", e.Message);
                }
            }

            Dictionary<string, string> names = new Dictionary<string, string>();
            foreach (XmlElement element in Children(root, "SensorNames", "Sensor"))
            {
                string rom = ReadString(element, "rom", string.Empty).Trim().ToUpperInvariant();
                string name = ReadString(element, "name", string.Empty).Trim();
                if (rom.Length == 2 * owDevice.owROM.LENGTH && name.Length > 0 && Device.NameValid(name))
                {
                    names[rom] = name;
                }
            }

            ApplyLogging(logging);
            ApplyAlarmOptions(alarmOptions);
            Chart.ShowAlarmLimits = chartLimits;
            foreach (KeyValuePair<string, float?[]> entry in limits)
            {
                Communication.SetLimit(Communication.lowLimits, entry.Key, entry.Value[0]);
                Communication.SetLimit(Communication.highLimits, entry.Key, entry.Value[1]);
            }
            foreach (KeyValuePair<string, float> entry in offsets)
            {
                Communication.SetLimit(Communication.offsets, entry.Key, entry.Value);
            }
            foreach (KeyValuePair<string, string> entry in names)
            {
                Communication.sensorNames[entry.Key] = entry.Value;
            }
            return true;
        }

        private class LoggingEntry
        {
            public int interval;
            public int recordsPerFile;
            public string pattern;
            public string directory;
            public bool newFilePerDay;
            public bool logToFile;
            public bool waitingToAppData;
            public int maxWaitingLines;
        }

        private static XmlWriterSettings WriterSettings()
        {
            return new XmlWriterSettings() { Indent = true, Encoding = new UTF8Encoding(false) };
        }

        private static void WriteLogging(XmlWriter writer)
        {
            writer.WriteStartElement("Logging");
            writer.WriteAttributeString("interval", XmlConvert.ToString(Logging.Interval.Value));
            writer.WriteAttributeString("recordsPerFile", XmlConvert.ToString(Logging.RecordsPerFile.Value));
            writer.WriteAttributeString("fileNamePattern", Logging.FileNamePattern ?? string.Empty);
            writer.WriteAttributeString("directory", Logging.LogDirectory ?? string.Empty);
            writer.WriteAttributeString("newFilePerDay", XmlConvert.ToString(Logging.NewFilePerDay));
            writer.WriteAttributeString("logToFile", XmlConvert.ToString(Logging.LogToFile));
            writer.WriteAttributeString("waitingToAppData", XmlConvert.ToString(Logging.WaitingToAppData));
            writer.WriteAttributeString("maxWaitingLines", XmlConvert.ToString(Logging.MaxWaitingLines));
            writer.WriteEndElement();
        }

        private static XmlElement LoadRoot(string path)
        {
            XmlDocument doc = new XmlDocument() { XmlResolver = null };
#if NET35
            XmlReaderSettings settings = new XmlReaderSettings() { ProhibitDtd = true, XmlResolver = null };
#else
            XmlReaderSettings settings = new XmlReaderSettings() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
#endif
            using (XmlReader reader = XmlReader.Create(path, settings))
            {
                doc.Load(reader);
            }

            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != Consts.App.NAME)
            {
                throw new FormatException(string.Format("Not a {0} configuration file.", Consts.App.NAME));
            }
            int version = ReadInt(root, "version", VERSION);
            if (version > VERSION)
            {
                throw new FormatException(string.Format("The file was saved by a newer version (format {0}).", version));
            }
            return root;
        }

        private static LoggingEntry ReadLogging(XmlElement root)
        {
            XmlElement element = root["Logging"];
            LoggingEntry entry = new LoggingEntry()
            {
                interval = ReadInt(element, "interval", Logging.Interval.Value),
                recordsPerFile = ReadInt(element, "recordsPerFile", Logging.RecordsPerFile.Value),
                pattern = ReadString(element, "fileNamePattern", Logging.FileNamePattern),
                directory = ReadString(element, "directory", Logging.LogDirectory),
                newFilePerDay = ReadBool(element, "newFilePerDay", Logging.NewFilePerDay),
                logToFile = ReadBool(element, "logToFile", Logging.LogToFile),
                waitingToAppData = ReadBool(element, "waitingToAppData", Logging.WaitingToAppData),
                maxWaitingLines = ReadInt(element, "maxWaitingLines", Logging.MaxWaitingLines),
            };
            if (entry.interval < 1 || entry.interval > Controller.Logging.MaxIntervalSeconds)
            {
                throw new FormatException(string.Format("Interval must be between 1 and {0} seconds.", Controller.Logging.MaxIntervalSeconds));
            }
            if (entry.recordsPerFile < 1)
            {
                throw new FormatException("Records per file must be at least 1.");
            }
            if (entry.maxWaitingLines < Logging.MinWaitingLinesLimit || entry.maxWaitingLines > Logging.MaxWaitingLinesLimit)
            {
                throw new FormatException(string.Format("Waiting lines must be between {0} and {1}.", Logging.MinWaitingLinesLimit, Logging.MaxWaitingLinesLimit));
            }
            if (!Utils.FileName.IsValid(entry.pattern))
            {
                throw new FormatException(string.Format("Invalid file name pattern \"{0}\".", entry.pattern));
            }
            return entry;
        }

        private static void ApplyLogging(LoggingEntry entry)
        {
            Logging.Interval.SetValue(entry.interval);
            Logging.RecordsPerFile.SetValue(entry.recordsPerFile);
            Logging.FileNamePattern = entry.pattern;
            Logging.LogDirectory = entry.directory;
            Logging.NewFilePerDay = entry.newFilePerDay;
            Logging.LogToFile = entry.logToFile;
            Logging.WaitingToAppData = entry.waitingToAppData;
            Logging.MaxWaitingLines = entry.maxWaitingLines;
        }

        public static void Import(string path)
        {
            XmlElement root = LoadRoot(path);

            LoggingEntry logging = ReadLogging(root);
            AlarmOptions alarmOptions = ReadAlarmOptions(root);
            bool chartLimits = ReadChartLimits(root);

            List<PortEntry> ports = new List<PortEntry>();
            foreach (XmlElement element in Children(root, "Ports", "Port"))
            {
                string name = ReadString(element, "name", string.Empty).Trim();
                if (name.Length == 0)
                {
                    throw new FormatException("A port without a name.");
                }
                ports.Add(new PortEntry() { name = CheckPortName(name), enabled = ReadBool(element, "enabled", true) });
            }

            List<DeviceEntry> devices = new List<DeviceEntry>();
            foreach (XmlElement element in Children(root, "Devices", "Device"))
            {
                string hex = ReadString(element, "rom", string.Empty);
                owDevice.owROM rom;
                try
                {
                    rom = new owDevice.owROM(ParseHex(hex));
                }
                catch (owUtils.owException)
                {
                    throw new FormatException(string.Format("Invalid device ROM \"{0}\" (checksum does not match).", hex));
                }
                if (owDevice.owFactory.GetDevice(rom) == null)
                {
                    throw new FormatException(string.Format("Unsupported device {0}.", hex));
                }
                devices.Add(new DeviceEntry()
                {
                    rom = rom,
                    port = CheckPortName(ReadString(element, "port", string.Empty).Trim()),
                    name = ReadString(element, "name", string.Empty).Trim(),
                    low = ReadFloat(element, "low"),
                    high = ReadFloat(element, "high"),
                    offset = ReadFloat(element, "offset") ?? 0f,
                    enabled = ReadBool(element, "enabled", true),
                });
                CheckLimits(hex, devices[devices.Count - 1].low, devices[devices.Count - 1].high);
                if (!OffsetValid(devices[devices.Count - 1].offset))
                {
                    throw new FormatException(string.Format("Sensor {0}: offset must be between -{1} and +{1} °C.", hex, Device.MaxOffset));
                }
                if (!Device.NameValid(devices[devices.Count - 1].name))
                {
                    throw new FormatException(string.Format("Sensor {0}: name must not start with \"=\".", hex));
                }
            }

            ApplyLogging(logging);
            ApplyAlarmOptions(alarmOptions);
            Chart.ShowAlarmLimits = chartLimits;

            foreach (Port port in Communication.GetPorts())
            {
                port.enabled = false;
                port.pendingEnable = false;
            }
            foreach (PortEntry entry in ports)
            {
                Port port = GetOrAddPort(entry.name);
                if (!entry.enabled)
                {
                    continue;
                }
                if (port.Available)
                {
                    port.enabled = true;
                }
                else
                {
                    port.pendingEnable = true;
                }
            }

            foreach (Device device in Communication.deviceList)
            {
                device.enabled = false;
            }
            foreach (DeviceEntry entry in devices)
            {
                Device device = Communication.FindDevice(entry.rom);
                if (device == null)
                {
                    device = new Device(owDevice.owFactory.GetDevice(entry.rom));
                    Communication.deviceList.Add(device);
                }
                device.port = entry.port.Length > 0 ? GetOrAddPort(entry.port) : null;
                device.enabled = entry.enabled && device.port != null;
                device.Name = entry.name.Length > 0 ? entry.name : null;
                device.Low = entry.low;
                device.High = entry.high;
                device.Offset = entry.offset;
            }
        }

        private static Port GetOrAddPort(string name)
        {
            return Communication.GetOrAddPort(name);
        }

        private static IEnumerable<XmlElement> Children(XmlElement root, string group, string name)
        {
            XmlElement parent = root[group];
            if (parent == null)
            {
                yield break;
            }
            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node is XmlElement element && element.Name == name)
                {
                    yield return element;
                }
            }
        }

        private static string ReadString(XmlElement element, string name, string fallback)
        {
            return element != null && element.HasAttribute(name) ? element.GetAttribute(name) : fallback;
        }

        private static int ReadInt(XmlElement element, string name, int fallback)
        {
            return element != null && element.HasAttribute(name) ? XmlConvert.ToInt32(element.GetAttribute(name)) : fallback;
        }

        private static bool ReadBool(XmlElement element, string name, bool fallback)
        {
            return element != null && element.HasAttribute(name) ? XmlConvert.ToBoolean(element.GetAttribute(name)) : fallback;
        }

        private static byte[] ParseHex(string hex)
        {
            if (hex.Length != 2 * owDevice.owROM.LENGTH)
            {
                throw new FormatException(string.Format("Invalid device ROM \"{0}\".", hex));
            }
            byte[] bytes = new byte[owDevice.owROM.LENGTH];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(2 * i, 2), 16);
            }
            return bytes;
        }
    }
}
