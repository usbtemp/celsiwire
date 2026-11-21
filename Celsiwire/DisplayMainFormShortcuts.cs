using System;
using System.Windows.Forms;

namespace Display
{
    partial class MainForm
    {
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (keyData == Keys.F5 && !loggingTask.Running && logBtn.Enabled)
            {
                OnLogBtn(logBtn, EventArgs.Empty);
                return true;
            }
            if (keyData == Keys.F6)
            {
                if (loggingTask.Running)
                {
                    WindowState = FormWindowState.Minimized;
                }
                else if (logBtn.Enabled)
                {
                    StartAndMinimize();
                }
                return true;
            }
            if (keyData == Keys.F8 && loggingTask.Running && logBtn.Enabled)
            {
                OnLogBtn(logBtn, EventArgs.Empty);
                return true;
            }
            if ((keyData & Keys.Modifiers) == Keys.Control)
            {
                if (key == Keys.M)
                {
                    WindowState = FormWindowState.Minimized;
                    return true;
                }
                if (key == Keys.L && confLoadBtn.Enabled)
                {
                    OnLoadBtn(confLoadBtn, EventArgs.Empty);
                    return true;
                }
                if (key == Keys.S && confSaveBtn.Enabled)
                {
                    OnSaveBtn(confSaveBtn, EventArgs.Empty);
                    return true;
                }
                int index = -1;
                if (key >= Keys.D1 && key <= Keys.D9)
                {
                    index = key - Keys.D1;
                }
                else if (key >= Keys.NumPad1 && key <= Keys.NumPad9)
                {
                    index = key - Keys.NumPad1;
                }
                if (index >= 0 && index < tabControl1.TabPages.Count)
                {
                    tabControl1.SelectedIndex = index;
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
