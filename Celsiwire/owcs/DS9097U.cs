using System.IO.Ports;

namespace owAdapter
{
    public class DS9097U : owAdapterSerial
    {
        private const byte RESET_PULSE = 0xc1;
        private const byte MODE_DATA = 0xe1;
        private const byte MODE_COMMAND = 0xe3;

        private const byte ACCELERATOR_ON = 0xb1;
        private const byte ACCELERATOR_OFF = 0xa1;

        private bool commstate;

        public DS9097U(SerialPort serialPort) : base(serialPort)
        {
            this.serialPort.BaudRate = 9600;
            this.serialPort.DataBits = 8;

            this.commstate = true;
            this.name = "DS9097U";
        }

        private void Flush()
        {
            this.serialPort.DiscardOutBuffer();
            this.serialPort.DiscardInBuffer();
        }

        private void Detect()
        {
            this.serialPort.BreakState = true;
            System.Threading.Thread.Sleep(2);
            this.serialPort.BreakState = false;

            this.serialPort.DiscardOutBuffer();
            this.serialPort.DiscardInBuffer();

            byte[] wbuf = new byte[1];
            wbuf[0] = RESET_PULSE;
            serialPort.Write(wbuf, 0, 1);

            System.Threading.Thread.Sleep(2);

            this.Flush();
        }

        public override void Connect()
        {
            base.Connect();
            Detect();
        }

        private void SendCommand(byte cb)
        {
            byte[] wbuf = new byte[1];

            if (!this.commstate)
            {
                wbuf[0] = MODE_COMMAND;
                this.serialPort.Write(wbuf, 0, 1);
                this.Flush();
            }
            this.commstate = true;

            wbuf[0] = cb;
            this.serialPort.Write(wbuf, 0, 1);
        }

        override public void owReset()
        {
            SendCommand(RESET_PULSE);

            byte[] rbuf = new byte[1];
            int rbuflen = serialPort.Read(rbuf, 0, rbuf.Length);
            if (rbuflen != rbuf.Length)
            {
                throw new owUtils.owException(owUtils.owException.TX_RX_ERROR);
            }
            if ((rbuf[0] & 0xfc) != 0xcc)
            {
                throw new owUtils.owException(owUtils.owException.INVALID_RESPONSE);
            }
            switch (rbuf[0] & 0x0f)
            {
                case 0x0f:
                    throw new owUtils.owException(owUtils.owException.NO_SLAVES);
                case 0x0c:
                    throw new owUtils.owException(owUtils.owException.BUS_SHORTED);
            }
        }

        override protected byte owWrite(byte wb)
        {
            byte[] wbuf = new byte[1];

            if (this.commstate)
            {
                wbuf[0] = MODE_DATA;
                this.serialPort.Write(wbuf, 0, 1);
            }
            this.commstate = false;

            wbuf[0] = wb;
            if (wb == MODE_COMMAND)
            {
                this.serialPort.Write(wbuf, 0, 1);
            }
            this.serialPort.Write(wbuf, 0, 1);

            byte[] rbuf = new byte[1];
            this.serialPort.Read(rbuf, 0, 1);

            return rbuf[0];
        }

        private static byte CompactByte(byte b)
        {
            return (byte)((b & 0x80) >> 4 | (b & 0x20) >> 3 | (b & 0x08) >> 2 | (b & 0x02) >> 1);
        }

        private static byte[] ParseROM(byte[] d)
        {
            byte[] id = new byte[owDevice.owROM.LENGTH];

            for (int i = 0; i < owDevice.owROM.LENGTH; i++)
            {
                id[i] = (byte)((CompactByte(d[2 * i + 1]) << 4) | CompactByte(d[2 * i]));
            }
            return id;
        }

        private byte[] SearchAccelerator(byte[] data)
        {
            owReset();
            owWrite(owUtils.COMMAND.SEARCH_ROM);
            SendCommand(ACCELERATOR_ON);
            this.owBlock(ref data);
            SendCommand(ACCELERATOR_OFF);
            owReset();

            return data;
        }

        override public System.Collections.Generic.IEnumerable<byte[]> owSearch()
        {
            byte[] data = new byte[2 * owDevice.owROM.LENGTH];

            for (var i = 0; i < data.Length; i++)
            {
                data[i] = 0x0;
            }

            int k;
            do
            {
                k = 64;
                data = SearchAccelerator(data);
                byte[] id = ParseROM(data);

                for (int i = data.Length - 1; i >= 0; i--)
                {
                    byte mask = 0x40;
                    for (int j = 7; j > 0; j -= 2)
                    {
                        if ((mask & data[i]) != 0 && (data[i] & (mask << 1)) == 0)
                        {
                            mask <<= 1;
                            data[i] |= mask;
                            data[i] &= (byte)(0xff >> (7 - j));
                            for (int l = i + 1; l < data.Length; l++)
                            {
                                data[l] = 0;
                            }
                            break;
                        }
                        k--;
                        mask >>= 2;
                    }
                    if ((mask & 0xaa) != 0)
                    {
                        break;
                    }
                }
                yield return id;
            } while (k > 0);
        }
    }
}
