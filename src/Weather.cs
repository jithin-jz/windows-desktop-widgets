using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace KiroWidgets
{
    internal class WeatherReading
    {
        internal int TempC;
        internal int HighC;
        internal int LowC;
        internal string Description;
        internal string Glyph;
        internal string Place;
    }

    /// <summary>Current conditions from Open-Meteo, which needs no API key.</summary>
    internal static class Weather
    {
        private static readonly Dictionary<int, string> Descriptions = BuildDescriptions();
        private static readonly Dictionary<int, string> Glyphs = BuildGlyphs();

        static Weather()
        {
            // Open-Meteo is HTTPS only; be explicit so this does not depend on the
            // machine-wide default protocol setting.
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { }
        }

        /// <summary>Blocking fetch. Call this off the UI thread. Returns null on failure.</summary>
        internal static WeatherReading Fetch(WeatherConfig cfg)
        {
            if (cfg == null) return null;
            try
            {
                string url = "https://api.open-meteo.com/v1/forecast"
                    + "?latitude=" + cfg.Lat.ToString(CultureInfo.InvariantCulture)
                    + "&longitude=" + cfg.Lon.ToString(CultureInfo.InvariantCulture)
                    + "&current=temperature_2m,weather_code"
                    + "&daily=temperature_2m_max,temperature_2m_min"
                    + "&timezone=auto&forecast_days=1";

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 8000;
                req.ReadWriteTimeout = 8000;
                req.UserAgent = "KiroDesktopWidgets";

                string body;
                using (WebResponse resp = req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                {
                    body = sr.ReadToEnd();
                }

                Dictionary<string, object> root =
                    new JavaScriptSerializer().DeserializeObject(body) as Dictionary<string, object>;
                if (root == null) return null;

                Dictionary<string, object> current = root["current"] as Dictionary<string, object>;
                Dictionary<string, object> daily = root["daily"] as Dictionary<string, object>;
                if (current == null || daily == null) return null;

                int code = (int)Math.Round(Store.ToDouble(current["weather_code"]));

                WeatherReading r = new WeatherReading();
                r.TempC = (int)Math.Round(Store.ToDouble(current["temperature_2m"]));
                r.HighC = (int)Math.Round(FirstOf(daily["temperature_2m_max"]));
                r.LowC = (int)Math.Round(FirstOf(daily["temperature_2m_min"]));
                r.Description = Descriptions.ContainsKey(code) ? Descriptions[code] : "Weather";
                r.Glyph = Glyphs.ContainsKey(code) ? Glyphs[code] : "\uE753";
                r.Place = (cfg.Name == null ? "WEATHER" : cfg.Name.ToUpperInvariant());
                return r;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return null;
            }
        }

        private static void LogError(Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                           "KiroDesktopWidgets");
                Directory.CreateDirectory(dir);
                string line = DateTime.Now.ToString("s") + "  " + ex.GetType().FullName + ": " + ex.Message;
                if (ex.InnerException != null) line += "  --> " + ex.InnerException.Message;
                File.AppendAllText(Path.Combine(dir, "weather.log"), line + Environment.NewLine);
            }
            catch { }
        }

        private static double FirstOf(object array)
        {
            object[] items = array as object[];
            if (items == null || items.Length == 0) return 0;
            return Store.ToDouble(items[0]);
        }

        private static Dictionary<int, string> BuildDescriptions()
        {
            Dictionary<int, string> d = new Dictionary<int, string>();
            d[0] = "Clear"; d[1] = "Mainly clear"; d[2] = "Partly cloudy"; d[3] = "Overcast";
            d[45] = "Fog"; d[48] = "Freezing fog";
            d[51] = "Light drizzle"; d[53] = "Drizzle"; d[55] = "Heavy drizzle";
            d[61] = "Light rain"; d[63] = "Rain"; d[65] = "Heavy rain";
            d[66] = "Freezing rain"; d[67] = "Freezing rain";
            d[71] = "Light snow"; d[73] = "Snow"; d[75] = "Heavy snow"; d[77] = "Snow grains";
            d[80] = "Rain showers"; d[81] = "Rain showers"; d[82] = "Heavy showers";
            d[85] = "Snow showers"; d[86] = "Snow showers";
            d[95] = "Thunderstorm"; d[96] = "Thunderstorm, hail"; d[99] = "Thunderstorm, hail";
            return d;
        }

        private static Dictionary<int, string> BuildGlyphs()
        {
            Dictionary<int, string> g = new Dictionary<int, string>();
            // Every codepoint below was verified present in Segoe Fluent Icons
            // (C:\Windows\Fonts\SegoeIcons.ttf) by reading the font's own glyph
            // map, not copied from an icon chart. That check matters: six of the
            // eight codes this table used before were in neither Segoe Fluent
            // Icons nor Segoe MDL2 Assets and rendered as empty tofu boxes, and
            // a seventh - U+E9CA, used as the "cloud" and as the fallback - does
            // exist but draws a thermometer.
            //
            // The font ships no sun-behind-cloud glyph, so "partly cloudy" shares
            // the plain cloud with "overcast", and it has a single precipitation
            // glyph, so drizzle and rain share it too. The description text under
            // the icon is what tells those pairs apart.
            string sun = "\uE706", partly = "\uE753", cloud = "\uE753", fog = "\uEDA8";
            string drizzle = "\uEA91", rain = "\uEA91", snow = "\uEDAD", storm = "\uE945";
            g[0] = sun; g[1] = sun; g[2] = partly; g[3] = cloud;
            g[45] = fog; g[48] = fog;
            g[51] = drizzle; g[53] = drizzle; g[55] = drizzle;
            g[61] = rain; g[63] = rain; g[65] = rain; g[66] = rain; g[67] = rain;
            g[71] = snow; g[73] = snow; g[75] = snow; g[77] = snow;
            g[80] = rain; g[81] = rain; g[82] = rain;
            g[85] = snow; g[86] = snow;
            g[95] = storm; g[96] = storm; g[99] = storm;
            return g;
        }
    }
}
