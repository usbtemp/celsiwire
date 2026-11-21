using System.Collections.Generic;
using System.IO.Ports;

namespace Controller
{
    public class Ports
    {
        public static List<Configuration.Port> Detect()
        {
            /* Vrni vsa zaporedna vrata zaznana na sistemu. */
            foreach (string name in SerialPort.GetPortNames())
            {
                Configuration.Communication.UpdatePort(name);
            }
            Dictionary<string, Data.UsbPortInfo> usbPorts = Utils.UsbPorts.Read();
            foreach (Configuration.Port port in Configuration.Communication.GetPorts())
            {
                usbPorts.TryGetValue(port.ToString(), out port.usb);
            }
            int removed = Configuration.Communication.RemoveUnusedMissingPorts();
            if (removed > 0)
            {
                Utils.Log.Info("{0} missing port(s) without sensors removed from the list", removed);
            }
            return Configuration.Communication.ApplyPortAvailability();
        }
    }
}
