using System;
using System.Drawing;
using System.Windows.Forms;

namespace Display
{
    public class DevicesTab : TabPage
    {
        public void RefreshPorts()
        {
            serialGroupBox.SerialPortRefresh();
        }

        private readonly GroupBox sensorGroupBox;
        private readonly DevicesTabSerialPorts serialGroupBox;

        private void ResizeComponents()
        {
            int margin = Consts.Display.MARGIN;
            Size size = ClientSize;
            int h = size.Height;
            int w = size.Width;

            int controlX = margin;
            int controlY = 0;

            int groupBoxWidth = w - 2 * margin;
            int groupBoxHeight = h - 2 * margin;

            controlY += margin;
            int serialGroupBoxHeight = groupBoxHeight / 2;
            serialGroupBox.Location = new Point(controlX, controlY);
            serialGroupBox.Size = new Size(groupBoxWidth, serialGroupBoxHeight);

            serialGroupBoxHeight = serialGroupBox.Size.Height;

            controlY += serialGroupBoxHeight;
            int sensorGroupBoxHeight = groupBoxHeight - serialGroupBoxHeight;
            sensorGroupBox.Location = new Point(controlX, controlY);
            sensorGroupBox.Size = new Size(groupBoxWidth, sensorGroupBoxHeight);
        }

        public void Disable()
        {
            serialGroupBox.Enabled = false;
            sensorGroupBox.Enabled = false;
        }

        public void Enable()
        {
            serialGroupBox.Enabled = true;
            sensorGroupBox.Enabled = true;
        }

        private void OnResize(object sender, EventArgs e)
        {
            ResizeComponents();
        }

        public DevicesTab()
        {
            serialGroupBox = new DevicesTabSerialPorts() { Text = "Serial ports" };
            Controls.Add(serialGroupBox);

            sensorGroupBox = new DevicesTabSensors() { Text = "Sensors" };
            Controls.Add(sensorGroupBox);

            Event.DeviceBusy += new Event.DisplayEvent(Disable);
            Event.DeviceAvailable += new Event.DisplayEvent(Enable);

            Resize += new EventHandler(OnResize);
        }
    }
}
