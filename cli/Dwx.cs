using System;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace KiroWidgets
{
    // Standalone CLI, deliberately separate from the widget exe: it needs to
    // keep running (to relaunch the installer) even while the thing it manages
    // is being stopped and overwritten.
    internal static class Dwx
    {
        private const string Repo = "jithin-jz/windows-desktop-widgets";
        private const string Branch = "main";

        private static string InstallDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopWidgets"); }
        }

        private static int Main(string[] args)
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { }

            string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "version";
            switch (cmd)
            {
                case "version": case "-v": case "--version": return CmdVersion();
                case "update": return CmdUpdate();
                default: return CmdHelp();
            }
        }

        private static int CmdVersion()
        {
            string local = ReadLocalVersion();
            Console.WriteLine("dwx  (Desktop Widgets)");
            Console.WriteLine("  installed: " + (local ?? "not installed"));

            string remote = TryReadRemoteVersion();
            if (remote == null)
            {
                Console.WriteLine("  latest   : could not check (no network / GitHub unreachable)");
                return 0;
            }
            Console.WriteLine("  latest   : " + remote);

            if (local == null)
            {
                Console.WriteLine();
                Console.WriteLine("Not installed yet. Run: dwx update");
            }
            else if (IsNewer(remote, local))
            {
                Console.WriteLine();
                Console.WriteLine("Update available: " + local + " -> " + remote);
                Console.WriteLine("Run: dwx update");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Up to date.");
            }
            return 0;
        }

        private static int CmdUpdate()
        {
            Console.WriteLine("Updating Desktop Widgets from " + Repo + " (" + Branch + ") ...");
            Console.WriteLine("A new window will show installer progress.");

            // Fire-and-forget: the installer stops the running widgets and, on
            // the next run of this same command, overwrites dwx.exe itself.
            // Waiting here would keep this exe's file locked against that.
            string psCommand = "irm https://raw.githubusercontent.com/" + Repo + "/" + Branch + "/install.ps1 | iex";
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + psCommand + "\"",
                UseShellExecute = true
            };
            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not start the installer: " + ex.Message);
                return 1;
            }
            return 0;
        }

        private static int CmdHelp()
        {
            Console.WriteLine("dwx - Desktop Widgets command-line tool");
            Console.WriteLine();
            Console.WriteLine("  dwx version   Show installed and latest version");
            Console.WriteLine("  dwx update    Update to the latest version");
            return 0;
        }

        private static string ReadLocalVersion()
        {
            try
            {
                string path = Path.Combine(InstallDir, "VERSION");
                return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
            }
            catch { return null; }
        }

        private static string TryReadRemoteVersion()
        {
            try
            {
                string url = "https://raw.githubusercontent.com/" + Repo + "/" + Branch + "/VERSION";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 6000;
                req.ReadWriteTimeout = 6000;
                req.UserAgent = "dwx-cli";
                using (WebResponse resp = req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                {
                    return sr.ReadToEnd().Trim();
                }
            }
            catch { return null; }
        }

        /// <summary>Numeric, dot-separated comparison (1.2.0 vs 1.10.0 sorts correctly).</summary>
        private static bool IsNewer(string remote, string local)
        {
            string[] r = remote.Split('.');
            string[] l = local.Split('.');
            int len = Math.Max(r.Length, l.Length);
            for (int i = 0; i < len; i++)
            {
                int rv = ParsePart(r, i);
                int lv = ParsePart(l, i);
                if (rv != lv) return rv > lv;
            }
            return false;
        }

        private static int ParsePart(string[] parts, int i)
        {
            if (i >= parts.Length) return 0;
            int v;
            return int.TryParse(parts[i], out v) ? v : 0;
        }
    }
}
