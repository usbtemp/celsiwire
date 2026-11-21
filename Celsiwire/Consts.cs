using System.Drawing;

namespace Consts
{
    class App
    {
        public const string NAME = "Celsiwire";
        public const string PROJECT_URL = "https://github.com/usbtemp/celsiwire";
        public const string SPONSOR_URL = "https://usbtemp.com";
    }

    class Colors
    {
        public static readonly Color CRITICAL = Color.FromArgb(0xC4, 0x2B, 0x1C);
        public static readonly Color CRITICAL_BACKGROUND = Color.FromArgb(0xFD, 0xE7, 0xE9);
        public static readonly Color RUNNING = Color.FromArgb(0x2E, 0x7D, 0x32);
    }

    class Display
    {
        public static int MARGIN = 8;
        public static readonly int TAB_MARGIN = (int)(1.2 * MARGIN);
        public static int PANEL_MIN_WIDTH = 250;
        public static int PANEL_HEIGHT = 130;
    }

    class Default
    {
        public static readonly int[] Interval = new int[] { 1, 2, 5, 10, 15, 30, 60, 120, 600 };
        public static readonly int[] RecordsPerFile = new int[] { 1000, 2000, 5000 };
    }
}
