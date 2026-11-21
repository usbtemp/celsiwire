using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Display
{
    /* Badge on the taskbar button (Windows 7 and newer): a green dot while logging, a red dot during an alarm. */
    partial class MainForm
    {
        private const int BADGE_NONE = 0;
        private const int BADGE_LOGGING = 1;
        private const int BADGE_ALARM = 2;

        private static readonly int TaskbarButtonCreatedMessage = Utils.WindowMessages.Register("TaskbarButtonCreated");
        private ITaskbarList3 taskbar;
        private bool taskbarFailed;
        private int taskbarBadge = -1;
        private Icon loggingBadgeIcon;
        private Icon alarmBadgeIcon;

        /* Also when running as administrator, where Explorer's message would otherwise be blocked. */
        private static void InitializeTaskbar()
        {
            Utils.WindowMessages.AllowFromLowerIntegrity(TaskbarButtonCreatedMessage);
        }

        private bool EnsureTaskbar()
        {
            if (taskbar != null)
            {
                return true;
            }
            if (taskbarFailed)
            {
                return false;
            }
            try
            {
                ITaskbarList3 list = (ITaskbarList3)new TaskbarList();
                list.HrInit();
                taskbar = list;
                return true;
            }
            catch (Exception e) when (e is COMException || e is InvalidCastException)
            {
                taskbarFailed = true;
                Utils.Log.Warn("Taskbar badge not available: {0}", e.Message);
                return false;
            }
        }

        /* Explorer sends TaskbarButtonCreated when the button exists (again after restoring from the tray); the badge is set again then. */
        private void OnTaskbarButtonCreated()
        {
            taskbarBadge = -1;
            UpdateTaskbarBadge();
        }

        private void UpdateTaskbarBadge()
        {
            if (!IsHandleCreated || !Visible || !EnsureTaskbar())
            {
                return;
            }
            int badge = !loggingTask.Running ? BADGE_NONE : AnyAlarmActive() ? BADGE_ALARM : BADGE_LOGGING;
            if (badge == taskbarBadge)
            {
                return;
            }
            try
            {
                if (badge == BADGE_NONE)
                {
                    taskbar.SetOverlayIcon(Handle, IntPtr.Zero, null);
                }
                else if (badge == BADGE_ALARM)
                {
                    taskbar.SetOverlayIcon(Handle, BadgeIcon(ref alarmBadgeIcon, Consts.Colors.CRITICAL).Handle, "Alarm");
                }
                else
                {
                    taskbar.SetOverlayIcon(Handle, BadgeIcon(ref loggingBadgeIcon, Consts.Colors.RUNNING).Handle, "Logging");
                }
                taskbarBadge = badge;
            }
            catch (COMException e)
            {
                Utils.Log.Warn("Taskbar badge could not be set: {0}", e.Message);
            }
        }

        /* A filled dot with a white ring, drawn once per color at the small icon size. */
        private static Icon BadgeIcon(ref Icon icon, Color color)
        {
            if (icon != null)
            {
                return icon;
            }
            Size size = SystemInformation.SmallIconSize;
            using (Bitmap bitmap = new Bitmap(size.Width, size.Height))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.Clear(Color.Transparent);
                    float ring = Math.Max(1f, size.Width / 10f);
                    using (SolidBrush white = new SolidBrush(Color.White))
                    {
                        graphics.FillEllipse(white, 0, 0, size.Width - 1, size.Height - 1);
                    }
                    using (SolidBrush brush = new SolidBrush(color))
                    {
                        graphics.FillEllipse(brush, ring, ring, size.Width - 1 - 2 * ring, size.Height - 1 - 2 * ring);
                    }
                }
                icon = IconWithAlpha(bitmap);
            }
            return icon;
        }

        /*
         * Bitmap.GetHicon() loses the soft edge (half-transparent pixels turn dark), so the icon is built
         * as a 32-bit .ico in memory: straight BGRA pixels, bottom-up, with an empty AND mask.
         */
        private static Icon IconWithAlpha(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int pixelBytes = width * height * 4;
            int maskBytes = (width + 31) / 32 * 4 * height;
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
                {
                    writer.Write((short)0);
                    writer.Write((short)1);
                    writer.Write((short)1);
                    writer.Write((byte)width);
                    writer.Write((byte)height);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((short)1);
                    writer.Write((short)32);
                    writer.Write(40 + pixelBytes + maskBytes);
                    writer.Write(6 + 16);
                    writer.Write(40);
                    writer.Write(width);
                    writer.Write(height * 2);
                    writer.Write((short)1);
                    writer.Write((short)32);
                    writer.Write(0);
                    writer.Write(pixelBytes + maskBytes);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                    for (int y = height - 1; y >= 0; y--)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            Color pixel = bitmap.GetPixel(x, y);
                            writer.Write(pixel.B);
                            writer.Write(pixel.G);
                            writer.Write(pixel.R);
                            writer.Write(pixel.A);
                        }
                    }
                    writer.Write(new byte[maskBytes]);
                    writer.Flush();
                    stream.Position = 0;
                    return new Icon(stream);
                }
            }
        }

        private void DestroyBadgeIcons()
        {
            if (loggingBadgeIcon != null)
            {
                loggingBadgeIcon.Dispose();
                loggingBadgeIcon = null;
            }
            if (alarmBadgeIcon != null)
            {
                alarmBadgeIcon.Dispose();
                alarmBadgeIcon = null;
            }
        }

        [ComImport]
        [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            /* All methods up to SetOverlayIcon, in the order of the COM interface (ITaskbarList, ITaskbarList2, ITaskbarList3). */
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
            void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
            void SetProgressState(IntPtr hwnd, int state);
            void RegisterTab(IntPtr hwndTab, IntPtr hwndMdi);
            void UnregisterTab(IntPtr hwndTab);
            void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
            void SetTabActive(IntPtr hwndTab, IntPtr hwndMdi, uint reserved);
            void ThumbBarAddButtons(IntPtr hwnd, uint count, IntPtr buttons);
            void ThumbBarUpdateButtons(IntPtr hwnd, uint count, IntPtr buttons);
            void ThumbBarSetImageList(IntPtr hwnd, IntPtr imageList);
            void SetOverlayIcon(IntPtr hwnd, IntPtr icon, [MarshalAs(UnmanagedType.LPWStr)] string description);
        }

        [ComImport]
        [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
        [ClassInterface(ClassInterfaceType.None)]
        private class TaskbarList
        {
        }
    }
}
