using System;
using System.Threading;
using System.Windows.Forms;

namespace Program
{
    static class MainClass
    {
        [STAThread]
        static public void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += new ThreadExceptionEventHandler(OnThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnUnhandledException);

            Utils.Log.Info("{0} {1} started on {2}, .NET {3}", Consts.App.NAME, Application.ProductVersion, Environment.OSVersion.VersionString, Environment.Version);

            if (!SingleInstance.Acquire())
            {
                Utils.Log.Info("{0} is already running, showing it{1}", Consts.App.NAME, args.Length > 0 ? "; command line ignored: " + string.Join(" ", args) : string.Empty);
                SingleInstance.ShowRunningInstance();
                return;
            }

            /* Before changing the working folder, so a relative /config path is taken from the folder it was typed in. */
            if (args.Length > 0)
            {
                Utils.Log.Info("Command line: {0}", string.Join(" ", args));
                StartupOptions.Parse(args);
            }
            try
            {
                Environment.CurrentDirectory = Utils.Path.Root;
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)
            {
                Utils.Log.Warn("Could not change the working folder to {0}: {1}", Utils.Path.Root, e.Message);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Display.MainForm());
            SingleInstance.Release();
            Utils.Log.Info("{0} exited", Consts.App.NAME);
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ReportCrash(e.Exception, false);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ReportCrash(e.ExceptionObject as Exception, e.IsTerminating);
        }

        private static void ReportCrash(Exception e, bool terminating)
        {
            Utils.Log.Error("Unhandled exception{0}: {1}", terminating ? " (closing)" : string.Empty, e);
            Controller.Logging.FlushPending();
            string message = string.Format("An unexpected error occurred{0}:\n\n{1}\n\nDetails were written to:\n{2}",
                terminating ? " and " + Consts.App.NAME + " has to close" : string.Empty,
                e != null ? e.Message : "unknown error",
                Utils.Log.FilePath);
            MessageBox.Show(message, Consts.App.NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
