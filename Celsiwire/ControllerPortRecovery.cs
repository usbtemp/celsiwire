using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace Controller
{
    static class PortRecovery
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
        private const int MAX_CONNECT_ATTEMPTS = 3;
        private static List<string> knownPorts = new List<string>();
        private static Dictionary<string, int> connectAttempts = new Dictionary<string, int>();
        private static DateTime lastAttempt;
        private static bool portsChanged;

        public static bool TakePortsChanged()
        {
            bool changed = portsChanged;
            portsChanged = false;
            return changed;
        }

        public static void Start()
        {
            knownPorts = new List<string>(SerialPort.GetPortNames());
            connectAttempts = new Dictionary<string, int>();
            lastAttempt = DateTime.MinValue;
            portsChanged = false;
        }

        public static void Run(List<Configuration.Port> portList, List<Configuration.Device> deviceList)
        {
            List<Configuration.Port> lostPorts = new List<Configuration.Port>();
            List<Configuration.Device> lostDevices = new List<Configuration.Device>();
            foreach (Configuration.Port port in portList)
            {
                List<Configuration.Device> devices = DevicesOn(deviceList, port);
                if (devices.Count > 0 && AllFailing(devices))
                {
                    lostPorts.Add(port);
                    lostDevices.AddRange(devices);
                }
            }
            if (lostPorts.Count == 0 || DateTime.Now - lastAttempt < Interval)
            {
                return;
            }
            lastAttempt = DateTime.Now;

            List<string> names = new List<string>(SerialPort.GetPortNames());
            foreach (Configuration.Port port in lostPorts)
            {
                if (names.Contains(port.ToString()) && Reopen(port, lostDevices))
                {
                    foreach (Configuration.Device device in DevicesOn(lostDevices, port))
                    {
                        lostDevices.Remove(device);
                    }
                }
            }
            if (lostDevices.Count == 0)
            {
                return;
            }

            foreach (string name in names)
            {
                if (knownPorts.Contains(name) || IsOpen(portList, name))
                {
                    continue;
                }
                SearchNewPort(name, portList, lostDevices);
                if (lostDevices.Count == 0)
                {
                    break;
                }
            }
            foreach (Configuration.Port port in lostPorts)
            {
                if (!names.Contains(port.ToString()) && !Configuration.Communication.PortInUse(port))
                {
                    Configuration.Communication.portList.Remove(port);
                    Utils.Log.Info("{0}: removed, its sensors moved to another port", port);
                }
            }
            for (int i = portList.Count - 1; i >= 0; i--)
            {
                if (portList[i].serialAdapter == null && DevicesOn(deviceList, portList[i]).Count == 0)
                {
                    portList.RemoveAt(i);
                }
            }
        }

        private static List<Configuration.Device> DevicesOn(List<Configuration.Device> devices, Configuration.Port port)
        {
            List<Configuration.Device> result = new List<Configuration.Device>();
            foreach (Configuration.Device device in devices)
            {
                if (device.port == port)
                {
                    result.Add(device);
                }
            }
            return result;
        }

        private static bool AllFailing(List<Configuration.Device> devices)
        {
            foreach (Configuration.Device device in devices)
            {
                if (!device.failing)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsOpen(List<Configuration.Port> ports, string name)
        {
            foreach (Configuration.Port port in ports)
            {
                if (port.ToString() == name && port.serialAdapter != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Reopen(Configuration.Port port, List<Configuration.Device> lostDevices)
        {
            Adapter.Disconnect(port);
            port.RenewSerial();
            port.type = Configuration.Types.Present;
            if (!Adapter.Connect(port))
            {
                return false;
            }
            if (FindLost(port, DevicesOn(lostDevices, port)).Count == 0)
            {
                Adapter.Disconnect(port);
                return false;
            }
            Utils.Log.Info("{0}: port opened again", port);
            Display.Event.RaiseMessageChanged(string.Format("{0}: port opened again, logging continues", port));
            return true;
        }

        private static List<Configuration.Device> FindLost(Configuration.Port port, List<Configuration.Device> lostDevices)
        {
            List<Configuration.Device> found = new List<Configuration.Device>();
            try
            {
                foreach (owDevice.owROM rom in port.serialAdapter.SearchROM())
                {
                    Configuration.Device device = Configuration.Communication.FindDevice(lostDevices, rom);
                    if (device != null)
                    {
                        found.Add(device);
                    }
                }
            }
            catch (Exception e)
            {
                Utils.Log.Warn("{0}: searching for sensors failed: {1}", port, e.Message);
            }
            return found;
        }

        private static void SearchNewPort(string name, List<Configuration.Port> portList, List<Configuration.Device> lostDevices)
        {
            Configuration.Port port = Configuration.Communication.GetOrAddPort(name);
            port.present = true;
            port.type = Configuration.Types.Present;
            if (!Adapter.Connect(port))
            {
                connectAttempts.TryGetValue(name, out int attempts);
                connectAttempts[name] = ++attempts;
                if (attempts >= MAX_CONNECT_ATTEMPTS)
                {
                    knownPorts.Add(name);
                    Utils.Log.Info("{0}: no 1-Wire adapter, not checked again during this logging", port);
                }
                return;
            }

            List<Configuration.Device> found = FindLost(port, lostDevices);
            if (found.Count == 0)
            {
                Adapter.Disconnect(port);
                return;
            }
            port.enabled = true;
            portsChanged = true;
            if (!portList.Contains(port))
            {
                portList.Add(port);
            }
            foreach (Configuration.Device device in found)
            {
                Configuration.Port oldPort = device.port;
                device.port = port;
                device.available = true;
                lostDevices.Remove(device);
                if (oldPort != null && !Configuration.Communication.PortInUse(oldPort))
                {
                    oldPort.enabled = false;
                    oldPort.pendingEnable = false;
                }
                Display.Event.RaiseMessageChanged(string.Format("{0} found on {1} (was {2}), logging continues", device.DisplayName, port, oldPort));
            }
        }
    }
}
