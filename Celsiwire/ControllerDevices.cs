using System;
using System.Collections.Generic;

namespace Controller
{
    public class Devices
    {
        /* A DS18B20 needs up to 750 ms for a 12-bit conversion. */
        private const int CONVERSION_TIME_MS = 750;

        private static int DetectDevicesOnSerialPort(Configuration.Port port)
        {
            int nrDetected = 0;
            Display.Event.RaiseMessageChanged(string.Format("Searching for devices ... {0}", port.serialAdapter.ToString()));
            foreach (owDevice.owDevice device in port.serialAdapter.SearchDevices())
            {
                if (device == null)
                {
                    continue;
                }
                Display.Event.RaiseMessageChanged(string.Format("Found a device {0} on {1}", device.ToString(), port.serialAdapter.ToString()));
                nrDetected++;
                Configuration.Communication.UpdateDevice(device, port);
            }
            port.serialAdapter.Disconnect();

            return nrDetected;
        }

        public static int Detect()
        {
            int nrDetected = 0;

            Configuration.Communication.ClearDevices();
            foreach (Configuration.Port port in Configuration.Communication.GetPorts())
            {
                if (!port.enabled)
                {
                    continue;
                }

                try
                {
                    Connections.ConnectPort(port);
                    int found = DetectDevicesOnSerialPort(port);
                    nrDetected += found;
                    Utils.Log.Info("{0}: adapter {1}, {2} sensor(s) found", port, port.type, found);
                }
                catch (Exception e)
                {
                    Utils.Log.Warn("{0}: {1}", port, port.inUse ? "in use by another program" : port.type == Configuration.Types.None ? "no adapter found" : e.Message);
                }
                Connections.DisconnectPort(port);
            }
            Connections.DisconnectAll();

            return nrDetected;
        }

        public static void SensorConvert(Configuration.Device device)
        {
            owDevice.DS18B20 ds18b20 = (owDevice.DS18B20)device.device;
            ds18b20.adapter = device.port.serialAdapter;
            ds18b20.convert();
            ds18b20.adapter = null;
        }

        public static void SensorRead(Configuration.Device device)
        {
            owDevice.DS18B20 ds18b20 = (owDevice.DS18B20)device.device;
            ds18b20.adapter = device.port.serialAdapter;
            ds18b20.read();
            ds18b20.adapter = null;
            device.Value = ds18b20.getTemperatureC() + device.Offset;
        }

        private static void ReportReadFailure(Configuration.Device device, Exception e)
        {
            if (!device.failing)
            {
                device.failing = true;
                Utils.Log.Warn("{0}: reading failed: {1}", device.DisplayName, e.Message);
            }
        }

        private static void ReportReadSuccess(Configuration.Device device)
        {
            if (device.failing)
            {
                device.failing = false;
                Utils.Log.Info("{0}: reading again", device.DisplayName);
            }
        }

        public static List<Configuration.Device> Convert(List<Configuration.Device> devices, Action<Configuration.Device, Exception> onFailure)
        {
            List<Configuration.Device> converted = new List<Configuration.Device>();
            List<Configuration.Port> ports = new List<Configuration.Port>();
            foreach (Configuration.Device device in devices)
            {
                if (device.port != null && !ports.Contains(device.port))
                {
                    ports.Add(device.port);
                }
            }

            foreach (Configuration.Port port in ports)
            {
                List<Configuration.Device> onPort = new List<Configuration.Device>();
                foreach (Configuration.Device device in devices)
                {
                    if (device.port == port)
                    {
                        onPort.Add(device);
                    }
                }
                if (port.serialAdapter == null)
                {
                    InvalidOperationException notOpen = new InvalidOperationException(string.Format("{0} is not open", port));
                    foreach (Configuration.Device device in onPort)
                    {
                        onFailure(device, notOpen);
                    }
                    continue;
                }
                if (AllSensorsSelected(port, onPort))
                {
                    try
                    {
                        owDevice.DS18B20.convertAll(port.serialAdapter);
                        converted.AddRange(onPort);
                    }
                    catch (Exception e)
                    {
                        foreach (Configuration.Device device in onPort)
                        {
                            onFailure(device, e);
                        }
                    }
                    continue;
                }
                ConvertEach(onPort, converted, onFailure);
            }

            List<Configuration.Device> withoutPort = new List<Configuration.Device>();
            foreach (Configuration.Device device in devices)
            {
                if (device.port == null)
                {
                    withoutPort.Add(device);
                }
            }
            ConvertEach(withoutPort, converted, onFailure);
            return converted;
        }

        private static void ConvertEach(List<Configuration.Device> devices, List<Configuration.Device> converted, Action<Configuration.Device, Exception> onFailure)
        {
            foreach (Configuration.Device device in devices)
            {
                try
                {
                    SensorConvert(device);
                    converted.Add(device);
                }
                catch (Exception e)
                {
                    onFailure(device, e);
                }
            }
        }

        private static bool AllSensorsSelected(Configuration.Port port, List<Configuration.Device> onPort)
        {
            foreach (Configuration.Device device in Configuration.Communication.deviceList)
            {
                if (device.port == port && device.device is owDevice.DS18B20 && !onPort.Contains(device))
                {
                    return false;
                }
            }
            return true;
        }

        /* Starts the conversion on all sensors, waits for it once, then reads each sensor; returns the ones read. */
        private static List<Configuration.Device> ConvertAndRead(List<Configuration.Device> devices, Action<Configuration.Device, Exception> onFailure)
        {
            List<Configuration.Device> successfulRead = new List<Configuration.Device>();
            List<Configuration.Device> successfulConverted = Convert(devices, onFailure);
            if (successfulConverted.Count == 0)
            {
                return successfulRead;
            }
            System.Threading.Thread.Sleep(CONVERSION_TIME_MS);
            foreach (Configuration.Device device in successfulConverted)
            {
                try
                {
                    SensorRead(device);
                    successfulRead.Add(device);
                }
                catch (Exception e)
                {
                    onFailure(device, e);
                }
            }
            return successfulRead;
        }

        /* Used by logging: a failing sensor is logged once, and again when it reads again. */
        public static List<Configuration.Device> ReadDevices(List<Configuration.Device> deviceList)
        {
            List<Configuration.Device> successfulRead = ConvertAndRead(deviceList, ReportReadFailure);
            foreach (Configuration.Device device in successfulRead)
            {
                ReportReadSuccess(device);
            }
            return successfulRead;
        }

        private static void Read(bool readOnlyEnabled)
        {
            foreach (Configuration.Device device in Configuration.Communication.GetDeviceList(typeof(owDevice.DS18B20), readOnlyEnabled))
            {
                Connections.AddDevice(device);
            }
            try
            {
                Connections.ConnectAll();
                ReadPorts();
            }
            finally
            {
                Connections.DisconnectAll();
            }
        }

        private static void ReadPorts()
        {
            List<Configuration.Device> devices = new List<Configuration.Device>();
            foreach (Configuration.Port port in Controller.Connections._portList)
            {
                devices.AddRange(Connections.GetDevices(port));
            }
            ConvertAndRead(devices, LogReadFailure);
        }

        private static void LogReadFailure(Configuration.Device device, Exception e)
        {
            Utils.Log.Warn("{0}: reading failed: {1}", device.DisplayName, e.Message);
        }

        public static void ReadAll()
        {
            Read(false);
        }
    }
}
