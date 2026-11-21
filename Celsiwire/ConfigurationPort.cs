using System;
using System.IO.Ports;

namespace Configuration
{
    public enum Types { None, Present, DS9097U, DS9097E };

    public sealed class Port : IEnabled, IEquatable<Port>, IComparable<Port>, IDisposable
    {
        private readonly string name;
        public SerialPort Serial { get; private set; }
        public owAdapter.owAdapterSerial serialAdapter;
        public Types type;
        public bool pendingEnable;
        public bool present;
        public bool inUse;
        public Data.UsbPortInfo usb;
        public bool Available { get { return present; } }

        public Port(string name)
        {
            this.name = name;
            enabled = false;
            Serial = new SerialPort(name);
            type = Types.None;
            present = false;
        }

        public override string ToString()
        {
            return name;
        }

        public bool Equals(Port other)
        {
            return other != null && string.Equals(name, other.name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Port);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(name);
        }

        public int CompareTo(Port other)
        {
            return other == null ? 1 : string.CompareOrdinal(name, other.name);
        }

        public void RenewSerial()
        {
            try
            {
                Serial.Dispose();
            }
            catch (Exception e) when (e is System.IO.IOException || e is ObjectDisposedException || e is UnauthorizedAccessException)
            {
            }
            Serial = new SerialPort(name);
        }

        public void Dispose()
        {
            Serial.Dispose();
        }
    }
}
