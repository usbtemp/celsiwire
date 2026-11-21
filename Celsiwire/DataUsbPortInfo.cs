using System.Collections.Generic;

namespace Data
{
    public sealed class UsbPortInfo
    {
        public string Vid { get; internal set; }
        public string Pid { get; internal set; }
        public string SerialNumber { get; internal set; }
        public string Name { get; internal set; }
        public string Manufacturer { get; internal set; }
        internal bool Present { get; set; }

        public string Id
        {
            get { return Vid + ":" + Pid; }
        }

        public string Description
        {
            get
            {
                List<string> lines = new List<string>();
                if (!string.IsNullOrEmpty(Name))
                {
                    lines.Add(Name);
                }
                if (!string.IsNullOrEmpty(Manufacturer))
                {
                    lines.Add("Manufacturer: " + Manufacturer);
                }
                lines.Add("VID:PID: " + Id);
                if (!string.IsNullOrEmpty(SerialNumber))
                {
                    lines.Add("Serial number: " + SerialNumber);
                }
                return string.Join("\n", lines.ToArray());
            }
        }
    }
}
