using System;
using System.Collections.Generic;

namespace Configuration
{
    public class Communication
    {
        public readonly static Dictionary<string, string> sensorNames;
        public readonly static Dictionary<string, float> lowLimits;
        public readonly static Dictionary<string, float> highLimits;
        public readonly static Dictionary<string, float> offsets;

        static Communication()
        {
            portList = new List<Port>();
            deviceList = new List<Device>();
            sensorNames = new Dictionary<string, string>();
            lowLimits = new Dictionary<string, float>();
            highLimits = new Dictionary<string, float>();
            offsets = new Dictionary<string, float>();
        }

        public readonly static List<Port> portList;
        public static int PortListCount
        {
            get
            {
                int n = 0;
                foreach (Port port in GetPorts())
                {
                    if (port.enabled)
                    {
                        n++;
                    }
                }
                return n;
            }
        }

        public static void UpdatePort(string name)
        {
            Port newPort = new Port(name);
            portList.AppendIfNot(ref newPort);
            newPort.type = Types.Present;
            newPort.present = true;
        }

        public static void ClearPorts()
        {
            foreach (Port port in GetPorts())
            {
                port.type = Types.None;
                port.present = false;
                port.inUse = false;
            }
        }

        public static bool PortInUse(Port port)
        {
            foreach (Device device in deviceList)
            {
                if (device.port == port && device.enabled)
                {
                    return true;
                }
            }
            return false;
        }

        public static int RemoveUnusedMissingPorts()
        {
            int removed = 0;
            for (int i = portList.Count - 1; i >= 0; i--)
            {
                if (!portList[i].present && !PortInUse(portList[i]))
                {
                    portList[i].Dispose();
                    portList.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        public static List<Port> ApplyPortAvailability()
        {
            List<Port> newlyEnabled = new List<Port>();
            foreach (Port port in GetPorts())
            {
                if (!port.Available)
                {
                    if (port.enabled)
                    {
                        port.enabled = false;
                        port.pendingEnable = true;
                    }
                }
                else if (port.pendingEnable)
                {
                    port.enabled = true;
                    port.pendingEnable = false;
                    newlyEnabled.Add(port);
                }
            }
            return newlyEnabled;
        }

        public static Port GetOrAddPort(string name)
        {
            foreach (Port existing in GetPorts())
            {
                if (existing.ToString() == name)
                {
                    return existing;
                }
            }
            Port port = new Port(name);
            portList.Add(port);
            return port;
        }

        public static void SetPort(Port port, bool value)
        {
            portList.Update(port, value);
        }

        public static IEnumerable<Port> GetPorts()
        {
            List<Port> ports = new List<Port>();
            for (int attempt = 0; attempt < 5; attempt++)
            {
                ports.Clear();
                try
                {
                    foreach (Port port in portList.ToArray())
                    {
                        if (port != null)
                        {
                            ports.Add(port);
                        }
                    }
                    break;
                }
                catch (ArgumentException)
                {
                }
            }
            ports.Sort();
            return ports;
        }

        public readonly static List<Device> deviceList;

        public static void UpdateDevice(owDevice.owDevice device, Port port)
        {
            Device newDevice = new Device(device);
            deviceList.AppendIfNot(ref newDevice);
            newDevice.available = true;
            newDevice.port = port;
        }

        public static void ClearDevices()
        {
            foreach (Device device in deviceList)
            {
                device.available = false;
            }
        }

        public static void SetDevice(Device device, bool value)
        {
            deviceList.Update(device, value);
        }

        public static IEnumerable<Device> GetDevices(Type deviceType, bool mustBeEnabled)
        {
            foreach (Device device in deviceList)
            {
                if (mustBeEnabled && !device.enabled)
                {
                    continue;
                }
                if (deviceType != null && device.device.GetType() != deviceType)
                {
                    continue;
                }
                yield return device;
            }
        }

        public static List<Device> GetDeviceList(Type deviceType, bool mustBeEnabled)
        {
            return new List<Device>(GetDevices(deviceType, mustBeEnabled));
        }

        public static Device FindDevice(owDevice.owDevice owdevice)
        {
            return FindDevice(owdevice.rom);
        }

        public static Device FindDevice(owDevice.owROM rom)
        {
            return FindDevice(deviceList, rom);
        }

        public static Device FindDevice(List<Device> devices, owDevice.owROM rom)
        {
            foreach (Device device in devices)
            {
                if (device.device.rom.Equals(rom))
                {
                    return device;
                }
            }
            return null;
        }

        public static string GetDisplayName(owDevice.owDevice owdevice)
        {
            Device device = FindDevice(owdevice);
            return device != null ? device.DisplayName : owAlgorithms.StringByte.ToHex(owdevice.rom.id);
        }

        public static float? GetLimit(Dictionary<string, float> limits, string rom)
        {
            lock (limits)
            {
                return limits.TryGetValue(rom, out float value) ? value : (float?)null;
            }
        }

        public static void SetLimit(Dictionary<string, float> limits, string rom, float? value)
        {
            lock (limits)
            {
                if (value.HasValue)
                {
                    limits[rom] = value.Value;
                }
                else
                {
                    limits.Remove(rom);
                }
            }
        }

        public static Dictionary<string, float> CopyLimits(Dictionary<string, float> limits)
        {
            lock (limits)
            {
                return new Dictionary<string, float>(limits);
            }
        }
    }
}
