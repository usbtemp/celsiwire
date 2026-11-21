using System;
using System.IO.Ports;

namespace owAdapter
{
    abstract public class owAdapter : IDisposable
    {
        protected string name;
        abstract public void owReset();
        abstract protected byte owWrite(byte b);
        abstract public System.Collections.Generic.IEnumerable<byte[]> owSearch();
        abstract public void Dispose();

        public void owWriteByte(byte wb)
        {
            if (owWrite(wb) != wb)
            {
                throw new owUtils.owException(owUtils.owException.TX_RX_ERROR);
            }
        }

        public byte owReadByte()
        {
            return owWrite(0xff);
        }

        protected byte[] owReadBytes(int len)
        {
            byte[] buf = new byte[len];
            for (int i = 0; i < len; i++)
            {
                buf[i] = owReadByte();
            }
            return buf;
        }

        protected void owWriteBytes(byte[] buf)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                owWriteByte(buf[i]);
            }
        }

        protected void owBlock(ref byte[] data)
        {
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = owWrite(data[i]);
            }
        }

        private byte[] transmit(owDevice.owROM rom, byte[] command, byte[] data, int rlen)
        {
            owReset();

            if (rom == null || rom.id == null)
            {
                owWriteByte(owUtils.COMMAND.SKIP_ROM);
            }
            else
            {
                owWriteByte(owUtils.COMMAND.MATCH_ROM);
                owWriteBytes(rom.id);
            }

            owWriteBytes(command);

            if (data != null)
            {
                owWriteBytes(data);
            }

            return rlen > 0 ? owReadBytes(rlen) : null;
        }

        public byte[] transmitAll(byte[] command, byte[] data, int rlen)
        {
            return transmit(null, command, data, rlen);
        }

        public byte[] transmitOne(owDevice.owROM rom, byte[] command, byte[] data, int rlen)
        {
            return transmit(rom, command, data, rlen);
        }

        public owDevice.owROM readRom()
        {
            owReset();
            owWriteByte(owUtils.COMMAND.READ_ROM);

            byte[] id = owReadBytes(owDevice.owROM.LENGTH);
            return new owDevice.owROM(id);
        }

        private const int MAX_SEARCH_RESULTS = 256;
        private const int MAX_BAD_CRC_IN_ROW = 8;

        public System.Collections.Generic.IEnumerable<owDevice.owROM> SearchROM()
        {
            int results = 0;
            int badInRow = 0;
            foreach (byte[] id in owSearch())
            {
                if (++results > MAX_SEARCH_RESULTS)
                {
                    throw new owUtils.owException(owUtils.owException.SEARCH_DISCREPANCY);
                }
                owDevice.owROM owrom = ToROM(id);
                if (owrom == null)
                {
                    if (++badInRow >= MAX_BAD_CRC_IN_ROW)
                    {
                        throw new owUtils.owException(owUtils.owException.INVALID_CRC);
                    }
                    continue;
                }
                badInRow = 0;
                yield return owrom;
            }
        }

        public System.Collections.Generic.IEnumerable<owDevice.owDevice> SearchDevices()
        {
            foreach (owDevice.owROM owrom in SearchROM())
            {
                owDevice.owDevice owdevice = owDevice.owFactory.GetDevice(owrom);
                if (owdevice == null)
                {
                    continue;
                }
                owdevice.adapter = this;
                yield return owdevice;
            }
        }

        private static owDevice.owROM ToROM(byte[] id)
        {
            try
            {
                return new owDevice.owROM(id);
            }
            catch (owUtils.owException)
            {
                return null;
            }
        }

        public override string ToString()
        {
            return name;
        }
    }

    abstract public class owAdapterSerial : owAdapter
    {
        protected SerialPort serialPort;
        public bool IsOpen { get { return serialPort != null && serialPort.IsOpen; } }

        public owAdapterSerial(SerialPort serialPort)
        {
            this.serialPort = serialPort;
            this.serialPort.Parity = Parity.None;
            this.serialPort.StopBits = StopBits.One;
            this.serialPort.Handshake = Handshake.None;
            this.serialPort.RtsEnable = false;
        }

        public virtual void Connect()
        {
            serialPort.ReadTimeout = 500;
            serialPort.WriteTimeout = 500;

            if (serialPort.IsOpen)
            {
                throw new owUtils.owException(owUtils.owException.ALREADY_CONNECTED);
            }
            serialPort.Open();
#pragma warning disable CA1816
            GC.SuppressFinalize(serialPort.BaseStream);
#pragma warning restore CA1816
        }

        public void Disconnect()
        {
            if (!serialPort.IsOpen)
            {
                throw new owUtils.owException(owUtils.owException.NOT_CONNECTED);
            }
            try
            {
                serialPort.Close();
            }
            catch (Exception e) when (e is System.IO.IOException || e is ObjectDisposedException || e is UnauthorizedAccessException)
            {
            }
        }

        override public void Dispose()
        {
            if (IsOpen)
            {
                Disconnect();
            }
            GC.SuppressFinalize(this);
        }
    }
}
