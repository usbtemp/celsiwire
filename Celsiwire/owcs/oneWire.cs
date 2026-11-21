using System;

namespace owAlgorithms
{
    class StringByte
    {
        public static string ToHex(byte[] buf)
        {
            return BitConverter.ToString(buf).Replace("-", string.Empty);
        }
    }
}

namespace owDevice
{
    public class owROM
    {
        public byte[] id { get; }
        public byte family { get { return id != null && id.Length == LENGTH ? id[0] : (byte)0x00; } }
        public const int LENGTH = 8;

        public owROM(byte[] data)
        {
            if (data.Length != LENGTH)
            {
                throw new owUtils.owException(owUtils.owException.INVALID_ROM_LENGTH);
            }
            if (!owUtils.CRC.isValid(data))
            {
                throw new owUtils.owException(owUtils.owException.INVALID_CRC);
            }
            id = data;
        }

        public bool Equals(owROM obj)
        {
            if (obj == null || id.Length != obj.id.Length)
            {
                return false;
            }

            for (int i = 0; i < id.Length; i++)
            {
                if (id[i] != obj.id[i])
                {
                    return false;
                }
            }
            return true;
        }
    }

    class owFactory
    {
        public static owDevice GetDevice(owROM rom)
        {
            switch (rom.family)
            {
                case DS18B20.FamilyCode:
                    return new DS18B20(rom);
                case DS2401.FamilyCode:
                    return new DS2401(rom);
                default:
                    return null;
            }
        }
    }

    public class owDevice
    {
        public byte family { get; }
        public string name { get; }
        public owROM rom { get; }
        public owAdapter.owAdapter adapter;

        public owDevice(owROM rom, byte family, string name)
        {
            this.rom = rom;
            this.name = name;
            this.family = family;
        }

        override public string ToString()
        {
            return name;
        }

        virtual public bool Equals(owDevice obj)
        {
            return obj != null && rom.Equals(obj.rom);
        }

        protected void SendCommand(byte[] command)
        {
            if (adapter == null)
            {
                throw new owUtils.owException(owUtils.owException.NOT_CONNECTED);
            }
            adapter.transmitOne(rom, command, null, 0);
        }

        protected byte[] ReadCommand(byte[] command, int rlen)
        {
            if (adapter == null)
            {
                throw new owUtils.owException(owUtils.owException.NOT_CONNECTED);
            }
            return adapter.transmitOne(rom, command, null, rlen);
        }
    }
}
