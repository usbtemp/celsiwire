using System;

namespace Utils
{
    class Path
    {
        public static string Root
        {
            get
            {
                string directory = AppDomain.CurrentDomain.BaseDirectory;
                string root = System.IO.Path.GetPathRoot(directory);
                return directory.Length > root.Length ? directory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) : directory;
            }
        }

        public static string DefaultLogDirectory
        {
            get
            {
                string root = Root;
                if (IsWritable(root))
                {
                    return root;
                }
                return System.IO.Path.Combine(Folder(Environment.SpecialFolder.MyDocuments), Consts.App.NAME);
            }
        }

        /* A special folder, or the user profile folder when Windows has none set (e.g. for a service account). */
        private static string Folder(Environment.SpecialFolder folder)
        {
            string path = Environment.GetFolderPath(folder);
            if (path.Length == 0)
            {
                path = Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty;
            }
            return path;
        }

        public static string AppDataDirectory
        {
            get { return System.IO.Path.Combine(Folder(Environment.SpecialFolder.ApplicationData), Consts.App.NAME); }
        }

        /* Local (not roaming) AppData, for CSV lines that could not be written to the log folder. */
        public static string LocalAppDataDirectory
        {
            get { return System.IO.Path.Combine(Folder(Environment.SpecialFolder.LocalApplicationData), Consts.App.NAME); }
        }

        private static bool IsWritable(string directory)
        {
            try
            {
                string probe = System.IO.Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
                using (new System.IO.FileStream(probe, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None, 1, System.IO.FileOptions.DeleteOnClose))
                {
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
