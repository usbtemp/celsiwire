using System;
using System.Collections.Generic;

namespace Data
{
    public class MeasurePoint
    {
        public Dictionary<owDevice.owDevice, float> Data { get; }
        public DateTime Time { get; }

        public void Add(Configuration.Device device)
        {
            Data[device.device] = device.Value;
        }

        public override string ToString()
        {
            List<string> outputs = new List<string>();
            foreach (KeyValuePair<owDevice.owDevice, float> entry in Data)
            {
                outputs.Add(string.Format("{0}: {1}", entry.Key.ToString(), entry.Value));
            }
            return String.Join(" ", outputs.ToArray());
        }

        public MeasurePoint()
        {
            Time = DateTime.Now;
            Data = new Dictionary<owDevice.owDevice, float>();
        }
    }
}
