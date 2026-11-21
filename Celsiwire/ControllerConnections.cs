using System.Collections.Generic;
using static Configuration.ListExtensions;

namespace Controller
{
    static class Connections
    {
        public static readonly List<Configuration.Port> _portList;
        private static readonly Dictionary<Configuration.Port, List<Configuration.Device>> _deviceList;

        private static void AddDeviceToPort(Configuration.Port port, Configuration.Device device)
        {
            if (!_deviceList.TryGetValue(port, out List<Configuration.Device> devices))
            {
                devices = new List<Configuration.Device>();
                _deviceList[port] = devices;
            }
            devices.Add(device);
        }

        public static bool AddDevice(Configuration.Device device)
        {
            if (device == null || device.port == null)
            {
                return false;
            }
            _portList.AppendIfNot(device.port);
            AddDeviceToPort(device.port, device);
            return true;
        }

        public static List<Configuration.Device> GetDevices(Configuration.Port port)
        {
            return _deviceList.TryGetValue(port, out List<Configuration.Device> devices) ? devices : new List<Configuration.Device>();
        }

        public static void ConnectPort(Configuration.Port port)
        {
            _portList.AppendIfNot(ref port);
            if (!Adapter.Connect(port))
            {
                throw new System.IO.IOException(string.Format("Cannot connect to {0}.", port));
            }
        }

        public static void DisconnectPort(Configuration.Port port)
        {
            port = _portList.ContainsT(port);
            if (port == null)
            {
                return;
            }
            Adapter.Disconnect(port);
            _portList.Remove(port);
        }

        public static void ConnectAll()
        {
            foreach (Configuration.Port port in _portList)
            {
                Adapter.Connect(port);
            }
        }

        public static void DisconnectAll()
        {
            for (int i = _portList.Count - 1; i >= 0; i--)
            {
                Configuration.Port port = _portList[i];
                Adapter.Disconnect(port);
                _portList.RemoveAt(i);
            }
            _deviceList.Clear();
        }

        static Connections()
        {
            _portList = new List<Configuration.Port>();
            _deviceList = new Dictionary<Configuration.Port, List<Configuration.Device>>();
        }
    }
}
