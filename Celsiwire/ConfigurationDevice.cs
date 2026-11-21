using System;

namespace Configuration
{
    public enum AlarmState { None, Low, High, NoReading };

    public class Device : IEnabled, IEquatable<Device>
    {
        public readonly owDevice.owDevice device;
        public bool available;
        private bool hasValue;
        public bool HasValue { get { return hasValue; } }
        private float value;
        public float Value
        {
            set { this.value = value; hasValue = true; }
            get { return value; }
        }
        public Port port;
        public bool failing;
        public AlarmState alarm;
        public DateTime lastRead;
        public readonly Data.SensorStats stats = new Data.SensorStats();

        public float? Low
        {
            get { return Communication.GetLimit(Communication.lowLimits, RomId); }
            set { Communication.SetLimit(Communication.lowLimits, RomId, value); }
        }

        public float? High
        {
            get { return Communication.GetLimit(Communication.highLimits, RomId); }
            set { Communication.SetLimit(Communication.highLimits, RomId, value); }
        }

        public const float MaxOffset = 5f;

        public static bool NameValid(string name)
        {
            return !name.StartsWith("=", StringComparison.Ordinal);
        }

        public float Offset
        {
            get { return Communication.GetLimit(Communication.offsets, RomId) ?? 0f; }
            set { Communication.SetLimit(Communication.offsets, RomId, value != 0f ? value : (float?)null); }
        }
        public string Name
        {
            get
            {
                return Communication.sensorNames.TryGetValue(RomId, out string name) ? name : null;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Communication.sensorNames.Remove(RomId);
                }
                else
                {
                    Communication.sensorNames[RomId] = value;
                }
            }
        }

        public string RomId
        {
            get { return owAlgorithms.StringByte.ToHex(device.rom.id); }
        }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(Name) ? RomId : Name; }
        }

        public Device(owDevice.owDevice device)
        {
            this.device = device;
            enabled = false;
            available = false;
            hasValue = false;
            port = null;
        }

        public void SetPort(Port port)
        {
            this.port = port;
        }

        public override string ToString()
        {
            return DisplayName + (port != null ? " - " + port.ToString() : "") + (port != null && port.type == Types.DS9097E ? " (DS9097E)" : "") + (hasValue ? " (" + value + "°C)" : "");
        }

        public override int GetHashCode()
        {
            return BitConverter.ToInt32(device.rom.id, 0) + BitConverter.ToInt32(device.rom.id, 4);
        }

        public bool Equals(Device other)
        {
            return other != null && device.rom.Equals(other.device.rom);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Device);
        }
    }
}
