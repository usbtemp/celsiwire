using System;
using System.Drawing;
using System.Windows.Forms;

namespace Display
{
    class AboutTab : TabPage
    {
        private readonly FlowLayoutPanel panel;
        private readonly LinkLabel programFolderLink;
        private readonly LinkLabel appDataFolderLink;
        private readonly LinkLabel logFolderLink;

        private Label AddLabel(string text, Font font, int topMargin)
        {
            Label label = new Label() { Text = text, AutoSize = true, Margin = new Padding(0, topMargin, 0, 0) };
            if (font != null)
            {
                label.Font = font;
            }
            panel.Controls.Add(label);
            return label;
        }

        private static readonly string[,] Shortcuts = {
            { "F5", "Start logging" },
            { "F6", "Start logging and minimize to tray" },
            { "F8", "Stop logging" },
            { "Ctrl+M", "Minimize to tray" },
            { "Ctrl+L", "Load configuration" },
            { "Ctrl+S", "Save configuration" },
            { "Ctrl+1 … Ctrl+6", "Switch tabs" },
            { "F2", "Rename sensor (Devices list; right-click for more)" },
            { "Ctrl+C, Ctrl+A", "Copy, select all (Log tab); right-click to copy elsewhere" },
            { "Ctrl + close (X)", "Close without asking (stops logging)" },
        };

        private void AddShortcuts()
        {
            AddLabel("Keyboard shortcuts", new Font(Helper.UiFont, FontStyle.Bold), 2 * Consts.Display.MARGIN);
            TableLayoutPanel table = new TableLayoutPanel()
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 2, 0, 0),
            };
            for (int i = 0; i < Shortcuts.GetLength(0); i++)
            {
                table.Controls.Add(new Label() { Text = Shortcuts[i, 0], AutoSize = true, Margin = new Padding(0, 1, 2 * Consts.Display.MARGIN, 1) }, 0, i);
                table.Controls.Add(new Label() { Text = Shortcuts[i, 1], AutoSize = true, Margin = new Padding(0, 1, 0, 1) }, 1, i);
            }
            panel.Controls.Add(table);
        }

        private LinkLabel AddFolderLink(string title)
        {
            AddLabel(title, null, Consts.Display.MARGIN);
            LinkLabel link = new LinkLabel() { AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
            link.LinkClicked += new LinkLabelLinkClickedEventHandler(OnFolderLinkClicked);
            panel.Controls.Add(link);
            return link;
        }

        private void AddWebLink(string text, string linkText, string url, int topMargin)
        {
            LinkLabel link = new LinkLabel()
            {
                Text = text,
                LinkArea = new LinkArea(text.IndexOf(linkText, StringComparison.Ordinal), linkText.Length),
                AutoSize = true,
                Margin = new Padding(0, topMargin, 0, 0),
            };
            link.Links[0].LinkData = url;
            link.LinkClicked += new LinkLabelLinkClickedEventHandler(OnWebLinkClicked);
            panel.Controls.Add(link);
        }

        private void OnWebLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Helper.OpenUrl(this, (string)e.Link.LinkData);
        }

        private void UpdateFolders()
        {
            programFolderLink.Text = Utils.Path.Root;
            appDataFolderLink.Text = Utils.Path.AppDataDirectory;
            logFolderLink.Text = Configuration.Logging.LogDirectory;
        }

        private void OnFolderLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Helper.OpenFolder(this, ((LinkLabel)sender).Text);
        }

        private void OnVisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
            {
                UpdateFolders();
            }
        }

        public AboutTab()
        {
            int margin = Consts.Display.TAB_MARGIN;

            Panel scroller = new Panel()
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMargin = new Size(margin, margin),
            };
            Controls.Add(scroller);

            panel = new FlowLayoutPanel()
            {
                Location = new Point(margin, margin),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            scroller.Controls.Add(panel);

            AddLabel(Consts.App.NAME, new Font(Helper.UiFont.FontFamily, 16, FontStyle.Bold), 0);
            AddLabel("Temperature logger for 1-Wire DS18B20 sensors on DS9097E and DS9097U serial adapters.", null, 4);
            AddLabel(string.Format("Version {0}", Application.ProductVersion), null, Consts.Display.MARGIN);
            AddLabel(string.Format("Running on {0}, .NET {1}", Environment.OSVersion.VersionString, Environment.Version), null, 2);
            AddWebLink("Source code: " + Consts.App.PROJECT_URL, Consts.App.PROJECT_URL, Consts.App.PROJECT_URL, Consts.Display.MARGIN);
            AddWebLink("Sponsored by usbtemp.com", "usbtemp.com", Consts.App.SPONSOR_URL, 2);

            programFolderLink = AddFolderLink("Program folder:");
            appDataFolderLink = AddFolderLink(string.Format("Settings and diagnostic log ({0}.log):", Consts.App.NAME.ToLowerInvariant()));
            logFolderLink = AddFolderLink("Measurement files (CSV):");
            AddShortcuts();
            UpdateFolders();

            VisibleChanged += new EventHandler(OnVisibleChanged);
        }
    }
}
