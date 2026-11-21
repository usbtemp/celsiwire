using System.Drawing;
using System.Windows.Forms;

namespace Display
{
    class Helper
    {
        public static void CopyToClipboard(string text, string description)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            try
            {
                Clipboard.SetText(text);
                if (description != null)
                {
                    Event.RaiseMessageChanged(description + " copied to the clipboard.");
                }
            }
            catch (System.Runtime.InteropServices.ExternalException e)
            {
                Event.RaiseMessageChanged("Could not copy to the clipboard: " + e.Message);
            }
        }

        private static float dpiScale;
        private static Icon appIcon;
        private static Icon runningIcon;
        private static Icon alarmIcon;

        public static void OpenFolder(IWin32Window owner, string directory)
        {
            try
            {
                System.IO.Directory.CreateDirectory(directory);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + directory + "\"");
            }
            catch (System.Exception ex)
            {
                ReportOpenError(owner, directory, ex);
            }
        }

        public static void OpenUrl(IWin32Window owner, string url)
        {
            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch (System.Exception ex)
            {
                ReportOpenError(owner, url, ex);
            }
        }

        private static void ReportOpenError(IWin32Window owner, string target, System.Exception ex)
        {
            Utils.Log.Error("Cannot open {0}: {1}", target, ex.Message);
            MessageBox.Show(owner, string.Format("Cannot open {0}:\n\n{1}", target, ex.Message), Consts.App.NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static float DpiScale
        {
            get
            {
                if (dpiScale == 0)
                {
                    using (Graphics graphics = Graphics.FromHwnd(System.IntPtr.Zero))
                    {
                        dpiScale = graphics.DpiY / 96f;
                    }
                }
                return dpiScale;
            }
        }

        public static Padding ButtonPadding
        {
            get { return new Padding(Consts.Display.MARGIN, Consts.Display.MARGIN / 2, Consts.Display.MARGIN, Consts.Display.MARGIN / 2); }
        }

        public static Padding ButtonVerticalPadding
        {
            get { return DpiScale >= 1.25f ? new Padding(0, ButtonPadding.Top, 0, ButtonPadding.Bottom) : Padding.Empty; }
        }

        private static Icon LoadIcon(string name)
        {
            using (System.IO.Stream stream = typeof(Helper).Assembly.GetManifestResourceStream(name))
            {
                return stream != null ? new Icon(stream) : SystemIcons.Application;
            }
        }

        public static Icon AppIcon
        {
            get { return appIcon ?? (appIcon = LoadIcon("Celsiwire.ico")); }
        }

        public static Icon RunningIcon
        {
            get { return runningIcon ?? (runningIcon = LoadIcon("Celsiwire-running.ico")); }
        }

        public static Icon AlarmIcon
        {
            get { return alarmIcon ?? (alarmIcon = LoadIcon("Celsiwire-alarm.ico")); }
        }

        public static Font UiFont
        {
            get { return SystemFonts.MessageBoxFont ?? Control.DefaultFont; }
        }

        public static void CenterForm(Control left, Control right)
        {
            int leftX = left.Size.Height;
            int rightX = right.Size.Height;

            int y = (leftX - rightX) / 2;
            right.Location = new Point(right.Location.X, left.Location.Y + y);
        }

        public static void RightForm(Control left, Control right, int margin)
        {
            int y = (left.Size.Height - right.Size.Height) / 2;
            int x = left.Size.Width + margin;
            right.Location = new Point(left.Location.X + x, left.Location.Y + y);
        }

        public static int Max(params int[] values)
        {
            if (values.Length < 2)
            {
                return values[0];
            }

            int max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];
                }
            }
            return max;
        }
    }
}
