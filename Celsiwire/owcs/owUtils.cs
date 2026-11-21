using System;

namespace owUtils
{
    class COMMAND
    {
        public const byte MATCH_ROM = 0x55;
        public const byte SKIP_ROM = 0xCC;
        public const byte SEARCH_ROM = 0xF0;
        public const byte READ_ROM = 0x33;
    }

    class CRC
    {
        private const byte lsb_generator = 0x8c;

        private static byte lsb_crc8(byte[] buf, int buflen, byte generator)
        {
            byte crc = 0;
            for (int i = 0; i < buflen; i++)
            {
                crc ^= buf[i];
                for (int bit_counter = 8; bit_counter > 0; bit_counter--)
                {
                    crc = (crc & 0x01) == 0x01 ? (byte)(((crc >> 1) & 0x7f) ^ generator) : (byte)((crc >> 1) & 0x7f);
                }
            }
            return crc;
        }

        private static byte calculateCRC8(byte[] data)
        {
            return lsb_crc8(data, data.Length, lsb_generator);
        }

        public static bool isValid(byte[] data)
        {
            return data.Length > 0 && calculateCRC8(data) == 0;
        }
    }

    [Serializable]
    public class owException : Exception
    {
        public const int NO_SLAVES = -1;
        public const int SEARCH_DISCREPANCY = -2;
        public const int INVALID_CRC = -3;
        public const int TX_RX_ERROR = -4;
        public const int INVALID_ROM_LENGTH = -5;
        public const int NOT_CONNECTED = -6;
        public const int INVALID_RESPONSE = -7;
        public const int BUS_SHORTED = -8;
        public const int ALREADY_CONNECTED = -9;
        public const int POWER_ON_RESET = -10;

        private static readonly string[] errMsg = {
            "",
            "No slaves",
            "Search discrepancy",
            "Invalid CRC",
            "TX RX Error",
            "Invalid ROM length",
            "Not connected",
            "Invalid response",
            "Bus on ground",
            "Already connected",
            "Power-on reset value (85 °C)"
            };

        public int ErrorCode { get; }

        private static string getMessage(int errNo)
        {
            int index = -errNo;
            return index > 0 && index < errMsg.Length ? errMsg[index] : "Unknown error " + errNo;
        }

        public owException()
        {
        }

        public owException(string message) : base(message)
        {
        }

        public owException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected owException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
            ErrorCode = info.GetInt32(nameof(ErrorCode));
        }

        public owException(int errNo) : base(getMessage(errNo))
        {
            ErrorCode = errNo;
        }

        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ErrorCode), ErrorCode);
        }
    }
}
