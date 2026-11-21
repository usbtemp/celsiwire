using System;

namespace owDevice
{
    class DS18B20 : owDevice
    {
        public const byte FamilyCode = 0x28;

        private const byte DS18B20_SCRATCHPAD_READ = 0xBE;
        private const byte DS18B20_CONVERT_T = 0x44;

        private const int DS18B20_SCRATCHPAD_LENGTH = 9;
        private const int DS18B20_SCRATCHPAD_CONFIG_OFFSET = 4;
        private const short DS18B20_POWER_ON_RESET = 0x0550;

        private byte[] sp;

        public DS18B20(owROM rom) : base(rom, FamilyCode, "DS18B20 Temperature Sensor") { }

        private void verifyScratchPad()
        {
            if (sp == null || sp.Length != DS18B20_SCRATCHPAD_LENGTH)
            {
                throw new InvalidOperationException("No valid scratchpad has been read.");
            }
        }

        public void convert()
        {
            SendCommand(new byte[] { DS18B20_CONVERT_T });
        }

        public static void convertAll(owAdapter.owAdapter adapter)
        {
            if (adapter == null)
            {
                throw new owUtils.owException(owUtils.owException.NOT_CONNECTED);
            }
            adapter.transmitAll(new byte[] { DS18B20_CONVERT_T }, null, 0);
        }

        public void read()
        {
            byte[] data = ReadCommand(new byte[] { DS18B20_SCRATCHPAD_READ }, DS18B20_SCRATCHPAD_LENGTH);

            if (data == null || data.Length != DS18B20_SCRATCHPAD_LENGTH || !owUtils.CRC.isValid(data))
            {
                sp = null;
                throw new owUtils.owException(owUtils.owException.INVALID_CRC);
            }

            if (IsAllZero(data))
            {
                sp = null;
                throw new owUtils.owException(owUtils.owException.INVALID_RESPONSE);
            }

            if (BitConverter.ToInt16(data, 0) == DS18B20_POWER_ON_RESET)
            {
                sp = null;
                throw new owUtils.owException(owUtils.owException.POWER_ON_RESET);
            }
            sp = data;
        }

        private static bool IsAllZero(byte[] data)
        {
            foreach (byte b in data)
            {
                if (b != 0)
                {
                    return false;
                }
            }
            return true;
        }

        public float getTemperatureC()
        {
            verifyScratchPad();
            return (float)BitConverter.ToInt16(sp, 0) / 16;
        }

        public int resolution
        {
            get
            {
                verifyScratchPad();
                return 9 + ((sp[DS18B20_SCRATCHPAD_CONFIG_OFFSET] >> 5) & 0x03);
            }
        }
    }

    class DS2401 : owDevice
    {
        public const byte FamilyCode = 0x81;

        public DS2401(owROM rom) : base(rom, FamilyCode, "DS2401 Serial Number (DS9490R/DS9490B)") { }
    }
}
