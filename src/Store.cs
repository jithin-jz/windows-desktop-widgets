using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace KiroWidgets
{
    /// <summary>
    /// Reads and writes the on-disk state, kept in the same location and the same
    /// file formats the PowerShell version used, so existing positions, notes and
    /// weather settings carry over untouched.
    /// </summary>
    internal class Store
    {
        internal readonly string Dir;
        private readonly string layoutPath;
        private readonly string notesPath;
        private readonly string weatherPath;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();

        internal Dictionary<string, Point2> Layout = new Dictionary<string, Point2>();
        internal bool Locked;

        internal Store()
        {
            Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                               "KiroDesktopWidgets");
            layoutPath = Path.Combine(Dir, "layout.json");
            notesPath = Path.Combine(Dir, "notes.txt");
            weatherPath = Path.Combine(Dir, "weather.json");
            try { Directory.CreateDirectory(Dir); } catch { }
        }

        // ---- layout -------------------------------------------------------
        internal void LoadLayout()
        {
            try
            {
                if (!File.Exists(layoutPath)) return;
                Dictionary<string, object> root = Parse(File.ReadAllText(layoutPath));
                if (root == null) return;
                foreach (KeyValuePair<string, object> kv in root)
                {
                    if (kv.Key == "_locked")
                    {
                        Locked = Convert.ToBoolean(kv.Value, CultureInfo.InvariantCulture);
                        continue;
                    }
                    if (kv.Key.StartsWith("_")) continue;
                    Dictionary<string, object> pos = kv.Value as Dictionary<string, object>;
                    if (pos == null || !pos.ContainsKey("Left") || !pos.ContainsKey("Top")) continue;
                    Layout[kv.Key] = new Point2(ToDouble(pos["Left"]), ToDouble(pos["Top"]));
                }
            }
            catch { }
        }

        internal void SaveLayout()
        {
            try
            {
                Dictionary<string, object> root = new Dictionary<string, object>();
                foreach (KeyValuePair<string, Point2> kv in Layout)
                {
                    Dictionary<string, object> pos = new Dictionary<string, object>();
                    pos["Left"] = kv.Value.Left;
                    pos["Top"] = kv.Value.Top;
                    root[kv.Key] = pos;
                }
                root["_locked"] = Locked;
                File.WriteAllText(layoutPath, json.Serialize(root));
            }
            catch { }
        }

        // ---- notes --------------------------------------------------------
        internal string LoadNotes()
        {
            try { return File.Exists(notesPath) ? File.ReadAllText(notesPath) : ""; }
            catch { return ""; }
        }

        internal void SaveNotes(string text)
        {
            try { File.WriteAllText(notesPath, text); } catch { }
        }

        // ---- weather config ----------------------------------------------
        /// <summary>Returns null when weather.json is absent or unreadable.</summary>
        internal WeatherConfig LoadWeatherConfig()
        {
            try
            {
                if (!File.Exists(weatherPath)) return null;
                Dictionary<string, object> root = Parse(File.ReadAllText(weatherPath));
                if (root == null || !root.ContainsKey("lat") || !root.ContainsKey("lon")) return null;
                WeatherConfig c = new WeatherConfig();
                c.Lat = ToDouble(root["lat"]);
                c.Lon = ToDouble(root["lon"]);
                c.Name = root.ContainsKey("name") && root["name"] != null ? root["name"].ToString() : "WEATHER";
                return c;
            }
            catch { return null; }
        }

        // ---- helpers ------------------------------------------------------
        private Dictionary<string, object> Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            // Strip a UTF-8 BOM, which the PowerShell version wrote.
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            return json.DeserializeObject(text) as Dictionary<string, object>;
        }

        /// <summary>
        /// JavaScriptSerializer hands back int, decimal or double depending on the
        /// literal, so normalise through Convert with invariant culture.
        /// </summary>
        internal static double ToDouble(object o)
        {
            if (o == null) return 0;
            return Convert.ToDouble(o, CultureInfo.InvariantCulture);
        }
    }

    internal struct Point2
    {
        internal double Left;
        internal double Top;
        internal Point2(double left, double top) { Left = left; Top = top; }
    }

    internal class WeatherConfig
    {
        internal double Lat;
        internal double Lon;
        internal string Name;
    }
}
