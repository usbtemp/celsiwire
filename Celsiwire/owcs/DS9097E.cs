using System;
using System.IO.Ports;

namespace owAdapter
{
    class DS9097E : owAdapterSerial
    {
        private const int RESET_BAUD = 9600;
        private const int RESET_BITS = 8;
        private const int DATA_BAUD = 115200;
        private const int DATA_BITS = 6;

        public DS9097E(SerialPort serialPort) : base(serialPort)
        {
            this.serialPort.BaudRate = DATA_BAUD;
            this.serialPort.DataBits = DATA_BITS;

            this.name = "DS9097E";
        }

        override public void owReset()
        {
            byte rbuff;

            serialPort.DiscardInBuffer();
            serialPort.BaudRate = RESET_BAUD;
            serialPort.DataBits = RESET_BITS;

            try
            {
                byte[] buf = new byte[1];
                buf[0] = 0xf0;
                serialPort.Write(buf, 0, 1);
                serialPort.Read(buf, 0, 1);
                rbuff = buf[0];
            }
            catch (TimeoutException)
            {
                throw new owUtils.owException(owUtils.owException.NO_SLAVES);
            }
            finally
            {
                serialPort.BaudRate = DATA_BAUD;
                serialPort.DataBits = DATA_BITS;
            }

            if (rbuff == 0x00)
            {
                throw new owUtils.owException(owUtils.owException.BUS_SHORTED);
            }

            if (rbuff == 0xf0)
            {
                throw new owUtils.owException(owUtils.owException.NO_SLAVES);
            }
        }

        private bool owWriteBit(bool wb)
        {
            byte[] wbuf = new byte[1];
            byte[] rbuf = new byte[1];

            wbuf[0] = wb ? (byte)0xff : (byte)0x00;
            serialPort.Write(wbuf, 0, 1);

            int rbuflen = serialPort.Read(rbuf, 0, rbuf.Length);
            if (rbuflen != rbuf.Length)
            {
                throw new owUtils.owException(owUtils.owException.TX_RX_ERROR);
            }
            return (rbuf[0] & 0x01) == 0x01;
        }

        private bool owReadBit()
        {
            return owWriteBit(true);
        }

        override protected byte owWrite(byte wb)
        {
            byte[] wbuf = new byte[8];

            for (int i = 0; i < wbuf.Length; i++)
            {
                wbuf[i] = Convert.ToBoolean(wb & (1 << (i & 0x7))) ? (byte)0xff : (byte)0x00;
            }
            serialPort.Write(wbuf, 0, wbuf.Length);

            byte rb = 0;
            byte[] rbuf = new byte[8];
            for (int remaining = rbuf.Length; remaining > 0;)
            {
                int rbytes = serialPort.Read(rbuf, 0, remaining);
                for (int i = 0; i < rbytes; i++)
                {
                    rb >>= 1;
                    rb |= (rbuf[i] & 0x01) == 0x01 ? (byte)0x80 : (byte)0x00;
                    remaining--;
                }
            }
            return rb;
        }

        override public System.Collections.Generic.IEnumerable<byte[]> owSearch()
        {
            byte[] id = new byte[owDevice.owROM.LENGTH];
            uint next_diff;

            for (uint cur_diff = uint.MaxValue; cur_diff > 0; cur_diff = next_diff)
            {
                next_diff = 0;
                owReset();
                owWrite(owUtils.COMMAND.SEARCH_ROM);

                int i = 0;
                uint k = owDevice.owROM.LENGTH * 8;
                do
                {
                    int j = 8;
                    do
                    {
                        bool b = owReadBit();
                        if (owReadBit())
                        {
                            if (b) /* 11 = 0 devices */
                            {
                                throw new owUtils.owException(owUtils.owException.SEARCH_DISCREPANCY);
                            }
                        }
                        else if (!b) /* 00 = 2 devices */
                        {
                            if (cur_diff > k || ((id[i] & 1) == 1 && cur_diff != k))
                            {
                                b = true;
                                next_diff = k;
                            }
                        }

                        owWriteBit(b);
                        id[i] >>= 1;
                        if (b)
                        {
                            id[i] |= 0x80;
                        }
                        k--;

                    } while (--j > 0);
                    i++;

                } while (k > 0);

                yield return (byte[])id.Clone();
            }
        }
    }
}
