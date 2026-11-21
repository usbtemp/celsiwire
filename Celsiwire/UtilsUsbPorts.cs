using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Data;
using Microsoft.Win32;

namespace Utils
{
    static class UsbPorts
    {
        private const string ENUM_KEY = @"SYSTEM\CurrentControlSet\Enum";
        private static readonly string[] Buses = { "USB", "FTDIBUS" };
        private static readonly Regex DeviceId = new Regex(@"^VID_([0-9A-F]{4})[&+]PID_([0-9A-F]{4})(?:\+(.+))?", RegexOptions.IgnoreCase);

        private sealed class UsbDevice
        {
            public string Vid;
            public string Pid;
            public string SerialNumber;
            public string ParentIdPrefix;
        }

        /* COM port name -> USB device, read from the registry (the same data as Device Manager). */
        public static Dictionary<string, UsbPortInfo> Read()
        {
            Dictionary<string, UsbPortInfo> ports = new Dictionary<string, UsbPortInfo>(StringComparer.OrdinalIgnoreCase);
            try
            {
                List<UsbDevice> devices = ReadUsbDevices();
                foreach (string bus in Buses)
                {
                    ReadBus(bus, devices, ports);
                }
            }
            catch (Exception e) when (e is System.Security.SecurityException || e is UnauthorizedAccessException || e is System.IO.IOException)
            {
                Log.Warn("Cannot read USB information of serial ports: {0}", e.Message);
            }
            return ports;
        }

        private sealed class Instance
        {
            public string DeviceName;
            public Match Id;
            public string Name;
            public RegistryKey Key;
        }

        /* Every device instance key under Enum\<bus> whose device name holds a VID and PID. */
        private static IEnumerable<Instance> Instances(string bus)
        {
            using (RegistryKey busKey = Registry.LocalMachine.OpenSubKey(ENUM_KEY + @"\" + bus))
            {
                if (busKey == null)
                {
                    yield break;
                }
                foreach (string deviceName in busKey.GetSubKeyNames())
                {
                    Match match = DeviceId.Match(deviceName);
                    if (!match.Success)
                    {
                        continue;
                    }
                    using (RegistryKey deviceKey = busKey.OpenSubKey(deviceName))
                    {
                        if (deviceKey == null)
                        {
                            continue;
                        }
                        foreach (string instanceName in deviceKey.GetSubKeyNames())
                        {
                            using (RegistryKey instanceKey = deviceKey.OpenSubKey(instanceName))
                            {
                                if (instanceKey != null)
                                {
                                    yield return new Instance() { DeviceName = deviceName, Id = match, Name = instanceName, Key = instanceKey };
                                }
                            }
                        }
                    }
                }
            }
        }

        private static List<UsbDevice> ReadUsbDevices()
        {
            List<UsbDevice> devices = new List<UsbDevice>();
            foreach (Instance instance in Instances("USB"))
            {
                if (instance.DeviceName.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                devices.Add(new UsbDevice()
                {
                    Vid = instance.Id.Groups[1].Value.ToUpperInvariant(),
                    Pid = instance.Id.Groups[2].Value.ToUpperInvariant(),
                    SerialNumber = IsSerialNumber(instance.Name) ? instance.Name : null,
                    ParentIdPrefix = instance.Key.GetValue("ParentIdPrefix") as string,
                });
            }
            return devices;
        }

        /* Windows makes up an instance name with '&' when the device has no serial number. */
        private static bool IsSerialNumber(string instanceName)
        {
            return instanceName.IndexOf('&') < 0;
        }

        private static void ReadBus(string bus, List<UsbDevice> devices, Dictionary<string, UsbPortInfo> ports)
        {
            foreach (Instance instance in Instances(bus))
            {
                ReadInstance(instance.Key, instance.Id, instance.Name, devices, ports);
            }
        }

        private static void ReadInstance(RegistryKey instanceKey, Match match, string instanceName, List<UsbDevice> devices, Dictionary<string, UsbPortInfo> ports)
        {
            string portName;
            using (RegistryKey parameters = instanceKey.OpenSubKey("Device Parameters"))
            {
                portName = parameters == null ? null : parameters.GetValue("PortName") as string;
            }
            if (string.IsNullOrEmpty(portName))
            {
                return;
            }
            bool present;
            using (RegistryKey control = instanceKey.OpenSubKey("Control"))
            {
                present = control != null;
            }
            if (ports.TryGetValue(portName, out UsbPortInfo existing) && (existing.Present || !present))
            {
                return;
            }

            UsbPortInfo info = new UsbPortInfo()
            {
                Vid = match.Groups[1].Value.ToUpperInvariant(),
                Pid = match.Groups[2].Value.ToUpperInvariant(),
                Name = Text(instanceKey.GetValue("FriendlyName")) ?? Text(instanceKey.GetValue("DeviceDesc")),
                Manufacturer = Text(instanceKey.GetValue("Mfg")),
                Present = present,
            };
            info.SerialNumber = FindSerialNumber(info, match.Groups[3].Value, instanceName, devices);
            ports[portName] = info;
        }

        private static string FindSerialNumber(UsbPortInfo info, string ftdiSerial, string instanceName, List<UsbDevice> devices)
        {
            /* FTDI: FTDIBUS\VID_0403+PID_6001+<serial><port letter>\0000; the parent USB device has the exact serial number. */
            if (ftdiSerial.Length > 0)
            {
                /* No serial number in the chip: the driver uses a location ID made up by Windows (with '&'). */
                if (!IsSerialNumber(ftdiSerial))
                {
                    return null;
                }
                foreach (UsbDevice device in devices)
                {
                    if (device.Vid == info.Vid && device.Pid == info.Pid && device.SerialNumber != null
                        && ftdiSerial.Length == device.SerialNumber.Length + 1 && ftdiSerial.StartsWith(device.SerialNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        return device.SerialNumber;
                    }
                }
                return ftdiSerial;
            }
            if (IsSerialNumber(instanceName))
            {
                return instanceName;
            }
            /* One interface of a composite device (e.g. CDC): the serial number is on the parent, found by ParentIdPrefix. */
            foreach (UsbDevice device in devices)
            {
                if (device.Vid == info.Vid && device.Pid == info.Pid && device.SerialNumber != null && !string.IsNullOrEmpty(device.ParentIdPrefix)
                    && instanceName.StartsWith(device.ParentIdPrefix + "&", StringComparison.OrdinalIgnoreCase))
                {
                    return device.SerialNumber;
                }
            }
            return null;
        }

        /* Values like "@oem12.inf,%ftdi%;FTDI" hold the display text after the last ';'. */
        private static string Text(object value)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            int separator = text.LastIndexOf(';');
            return separator >= 0 ? text.Substring(separator + 1) : text;
        }
    }
}
