using System;
using System.Drawing;
using System.Windows.Forms;

namespace Display
{
    public class DevicesTabGroup : GroupBox
    {
        protected static int margin = Consts.Display.TAB_MARGIN;

        protected static int ButtonWidth { set; get; }

        private const int BUTTON_EXTRA = 40;

        protected static int ButtonColumnWidth
        {
            get { return ButtonWidth + (Helper.DpiScale >= 1.25f ? BUTTON_EXTRA : 0); }
        }

        private const string SELECT_ALL = "Select all";
        private const string SELECT_NONE = "Select none";

        public DevicesTabGroup() : base() { }

        protected Button CreateSelectAllButton(EventHandler onClick)
        {
            Button button = new Button() { Text = SELECT_NONE, Padding = Helper.ButtonVerticalPadding };
            button.MinimumSize = new Size(button.PreferredSize.Width, 0);
            button.Text = SELECT_ALL;
            button.Click += onClick;
            button.AutoSize = true;
            Controls.Add(button);
            return button;
        }

        protected static void SetSelectAllText(Button button, bool allSelected)
        {
            button.Text = allSelected ? SELECT_NONE : SELECT_ALL;
        }

        protected int ContentTop
        {
            get { return Math.Max(2 * margin, DisplayRectangle.Top); }
        }

        protected Size GetListBoxSize()
        {
            int groupWidth = ClientSize.Width;
            int groupHeight = ClientSize.Height;

            int x = groupWidth - (3 * margin) - ButtonColumnWidth, y = groupHeight - ContentTop - margin;

            return new Size(x, y);
        }
    }
}
