using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ShapePath = System.Windows.Shapes.Path;   // System.IO.Path is also in scope
using System.Windows.Threading;
using System.Xml;

namespace KiroWidgets
{
    internal class WidgetEntry
    {
        internal Window Window;
        internal double DefLeft;
        internal double DefTop;
    }

    public class WidgetApp : Application
    {
        // macOS grid: a 160px unit with 16px gutters. Small cards are 160x160,
        // medium cards span two units (336x160). Six cards form a 2-wide,
        // 4-tall block parked on the right of the work area.
        private const int Unit = 160;
        private const int Gutter = 16;
        private const int Pitch = Unit + Gutter;              // 176
        private const double BarMax = 124.0;                  // inner width of a small card

        // The day banner is a seventh widget that deliberately ignores the card
        // grid: it is a bare line of text centred at the top of the work area.
        // Its width is fixed rather than measured because the Window uses
        // SizeToContent, so ActualWidth is still 0 when the position is assigned.
        private const int BannerWidth = 760;
        private const int BannerSize = 54;
        private const int BannerTop = 28;                     // gap below the screen top
        private const string BannerWeight = "Normal";

        // Progress wave on the media card. 300 is the card's inner width: a
        // medium card is 336 wide with 18px padding each side.
        private const double WaveWidth = 300;
        private const double WaveHeight = 18;
        private const double WaveAmplitude = 5;
        private const int WaveCycles = 7;
        private const double WaveStep = 2;        // sample spacing in px

        // Anurati is not a Windows font, so it is loaded from a loose file in the
        // fonts folder beside the exe instead of from the system font list. That
        // needs no install and no admin rights. WPF wants a directory URI followed
        // by the family name, and it falls through to the next family in the list
        // when the file is absent - which is what keeps the banner readable before
        // the font has been dropped in.
        private static string BannerFontSpec()
        {
            string dir;
            try
            {
                dir = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, ".." + Path.DirectorySeparatorChar + "fonts"));
                if (!dir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    dir += Path.DirectorySeparatorChar;
                dir = new Uri(dir).AbsoluteUri;
            }
            catch { return "Century Gothic, Segoe UI"; }

            return dir + "#Anurati, Century Gothic, Segoe UI";
        }

        private readonly Dictionary<string, WidgetEntry> widgets = new Dictionary<string, WidgetEntry>();
        private readonly Dictionary<string, FrameworkElement> ui = new Dictionary<string, FrameworkElement>();
        private readonly List<MenuItem> lockItems = new List<MenuItem>();

        private readonly Store store = new Store();
        private readonly Stats stats = new Stats();
        private readonly MediaMonitor media = new MediaMonitor();

        private WeatherConfig weatherConfig;
        private bool mediaBusy;
        private bool weatherBusy;
        private bool weatherEverSucceeded;
        private int weatherRetriesLeft;
        private bool suppressNotesSave;

        [STAThread]
        public static void Main()
        {
            // Single instance: a second launch (startup shortcut plus a manual
            // start, say) would stack a duplicate set of cards on the desktop.
            bool isFirst;
            using (Mutex guard = new Mutex(true, "KiroDesktopWidgets.SingleInstance", out isFirst))
            {
                if (!isFirst) return;

                // Render in software. Hardware rendering maps the Intel GPU driver
                // stack (igc64, media_bin, igddxvacommon, igd12um - about 125 MB of
                // images) into the process, which measured as 81 MB of extra private
                // bytes. Six near-static cards do not need a GPU, and this is by far
                // the largest single saving available.
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

                WidgetApp app = new WidgetApp();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.Startup += app.OnStartup;
                app.Run();

                GC.KeepAlive(guard);
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            store.LoadLayout();
            weatherConfig = store.LoadWeatherConfig();
            BuildWidgets();
            WireUpMedia();
            BuildWave();
            WireUpWave();
            WireUpNotes();

            UpdateClock();
            UpdateStats();
            StartTimers();
            ShowWidgets();
            store.SaveLayout();

            RefreshMedia();
            RefreshWeather();
        }

        // ---------------------------------------------------------------- build
        private void BuildWidgets()
        {
            Rect wa = SystemParameters.WorkArea;
            int blockW = (Unit * 2) + Gutter;                 // 336
            int blockH = (Unit * 4) + (Gutter * 3);           // 688

            // Anchored to the work area rather than the screen, so a taskbar on any
            // edge is respected. wa.Top matters on multi-monitor and top-taskbar
            // setups where the work area does not start at y=0.
            //
            // The clamps are what make this survive a display smaller than the
            // card block: a 688px-tall block on a 1080p screen at 175% scaling
            // leaves a work area of ~569px, and centring it unclamped would put
            // the first row off the top of the screen.
            double gx = Math.Round((wa.Right - 40 - blockW) / 8) * 8;
            double gy = Math.Round((wa.Top + ((wa.Height - blockH) / 2)) / 8) * 8;

            double minX = Math.Round((wa.Left + 8) / 8) * 8;
            double minY = Math.Round((wa.Top + 8) / 8) * 8;
            if (gx < minX) gx = minX;
            if (gy < minY) gy = minY;

            double x1 = gx, x2 = gx + Pitch;
            double y1 = gy, y2 = gy + Pitch, y3 = gy + (Pitch * 2), y4 = gy + (Pitch * 3);

            Window clock = NewWidget("Clock", Markup.Clock, x1, y1, Unit, Unit, false, wa);
            Register(clock, "TimeText", "AmPmText", "DayText", "DateText");

            Window weather = NewWidget("Weather", Markup.Weather, x2, y1, Unit, Unit, false, wa);
            Register(weather, "WxPlace", "WxIcon", "WxTemp", "WxDesc", "WxRange");

            Window player = NewWidget("Media", Markup.Media, x1, y2, blockW, Unit, false, wa);
            Register(player, "MediaTitle", "MediaArtist", "BtnPrev", "BtnPlay", "BtnNext", "ArtBorder",
                     "ArtBeat", "Beat1", "Beat2", "Beat3", "Beat4",
                     "WaveDim", "WaveLit", "WaveHost");

            Window system = NewWidget("System", Markup.SysStats, x1, y3, Unit, Unit, false, wa);
            Register(system, "CpuText", "CpuBar", "RamText", "RamBar", "DiskText");

            if (Native.HasBattery())
            {
                Window battery = NewWidget("Battery", Markup.Battery, x2, y3, Unit, Unit, false, wa);
                Register(battery, "BattStatus", "BattText", "BattBar");
            }

            Window notes = NewWidget("Notes", Markup.Notes, x1, y4, blockW, Unit, true, wa);
            Register(notes, "NotesBox");

            // Top centre, measured from the work area so the taskbar is respected.
            double bx = Math.Round((wa.Left + ((wa.Width - BannerWidth) / 2)) / 8) * 8;
            double by = Math.Round((wa.Top + BannerTop) / 8) * 8;
            if (bx < minX) bx = minX;
            Window banner = NewWidget("DayBanner", Markup.DayBanner, bx, by, BannerWidth, BannerSize, false, wa);
            Register(banner, "BannerRow");
        }

        private Window NewWidget(string name, string body, double defLeft, double defTop,
                                  double width, double height, bool interactive, Rect wa)
        {
            string xaml = Markup.WindowHead + Markup.Styles + body + "</Window>";
            xaml = xaml.Replace("__SMALL__", Unit.ToString(CultureInfo.InvariantCulture))
                       .Replace("__MEDIUM__", ((Unit * 2) + Gutter).ToString(CultureInfo.InvariantCulture))
                       .Replace("__BANNERW__", BannerWidth.ToString(CultureInfo.InvariantCulture))
                       .Replace("__BANNERSIZE__", BannerSize.ToString(CultureInfo.InvariantCulture))
                       .Replace("__BANNERFONT__", BannerFontSpec())
                       .Replace("__BANNERWEIGHT__", BannerWeight)
                       .Replace("__WAVEW__", WaveWidth.ToString(CultureInfo.InvariantCulture))
                       .Replace("__WAVEH__", WaveHeight.ToString(CultureInfo.InvariantCulture));

            Window w;
            using (StringReader sr = new StringReader(xaml))
            using (XmlReader xr = XmlReader.Create(sr))
            {
                w = (Window)XamlReader.Load(xr);
            }
            w.Tag = name;

            if (store.Layout.ContainsKey(name))
            {
                w.Left = store.Layout[name].Left;
                w.Top = store.Layout[name].Top;
            }
            else
            {
                w.Left = defLeft;
                w.Top = defTop;
            }

            // A saved position is an absolute point in Windows' virtual desktop
            // space, which shifts whenever a monitor is added, removed or
            // rearranged - a spot that used to sit at the right edge can end up
            // anywhere, including looking centred, once that space changes. This
            // pulls the card back into the *current* work area if the saved (or
            // default) spot no longer fits, so a monitor change repositions it
            // sanely instead of leaving it stranded. A position that is still
            // valid is left exactly as it was.
            double maxLeft = wa.Right - width;
            double maxTop = wa.Bottom - height;
            if (w.Left < wa.Left || w.Left > maxLeft) w.Left = Math.Max(wa.Left, Math.Min(defLeft, maxLeft));
            if (w.Top < wa.Top || w.Top > maxTop) w.Top = Math.Max(wa.Top, Math.Min(defTop, maxTop));
            store.Layout[name] = new Point2(w.Left, w.Top);

            bool isInteractive = interactive;
            w.SourceInitialized += delegate(object s, EventArgs e)
            {
                IntPtr h = new WindowInteropHelper((Window)s).Handle;
                if (isInteractive) Native.MakeToolWindowOnly(h);
                else Native.MakeDesktopWidget(h);
                Native.PinToBottom(h);
            };

            // The interactive widget rises to the front when it takes focus, so
            // drop it back onto the desktop layer as soon as focus leaves.
            w.Deactivated += delegate(object s, EventArgs e) { SendToDesktopLayer((Window)s); };

            w.MouseLeftButtonDown += OnWidgetDrag;
            w.LocationChanged += OnWidgetMoved;
            w.ContextMenu = BuildMenu();

            WidgetEntry entry = new WidgetEntry();
            entry.Window = w;
            entry.DefLeft = defLeft;
            entry.DefTop = defTop;
            widgets[name] = entry;
            return w;
        }

        private void OnWidgetDrag(object sender, MouseButtonEventArgs e)
        {
            if (store.Locked) return;
            Window w = (Window)sender;
            try
            {
                w.DragMove();
                w.Left = Math.Round(w.Left / 8) * 8;   // snap to an 8px grid
                w.Top = Math.Round(w.Top / 8) * 8;
            }
            catch { }
            SendToDesktopLayer(w);
        }

        private void OnWidgetMoved(object sender, EventArgs e)
        {
            Window w = (Window)sender;
            if (w.Tag == null) return;
            store.Layout[(string)w.Tag] = new Point2(w.Left, w.Top);
            store.SaveLayout();
        }

        private ContextMenu BuildMenu()
        {
            ContextMenu menu = new ContextMenu();

            MenuItem lockItem = new MenuItem();
            lockItem.Header = "Lock positions";
            lockItem.IsCheckable = true;
            lockItem.IsChecked = store.Locked;
            lockItem.Click += delegate(object s, RoutedEventArgs e)
            {
                store.Locked = ((MenuItem)s).IsChecked;
                foreach (MenuItem mi in lockItems) mi.IsChecked = store.Locked;
                store.SaveLayout();
            };
            lockItems.Add(lockItem);

            MenuItem reset = new MenuItem();
            reset.Header = "Reset positions";
            reset.Click += delegate(object s, RoutedEventArgs e) { ResetPositions(); };

            MenuItem exit = new MenuItem();
            exit.Header = "Exit widgets";
            exit.Click += delegate(object s, RoutedEventArgs e) { StopWidgets(); };

            menu.Items.Add(lockItem);
            menu.Items.Add(reset);
            menu.Items.Add(new Separator());
            menu.Items.Add(exit);
            return menu;
        }

        private void ResetPositions()
        {
            foreach (KeyValuePair<string, WidgetEntry> kv in widgets)
            {
                kv.Value.Window.Left = kv.Value.DefLeft;
                kv.Value.Window.Top = kv.Value.DefTop;
                store.Layout[kv.Key] = new Point2(kv.Value.DefLeft, kv.Value.DefTop);
            }
            store.SaveLayout();
        }

        private void StopWidgets()
        {
            store.SaveLayout();
            foreach (KeyValuePair<string, WidgetEntry> kv in widgets)
            {
                try { kv.Value.Window.Close(); } catch { }
            }
            Shutdown();
        }

        private static void SendToDesktopLayer(Window w)
        {
            try
            {
                IntPtr h = new WindowInteropHelper(w).Handle;
                if (h != IntPtr.Zero) Native.PinToBottom(h);
            }
            catch { }
        }

        private void ShowWidgets()
        {
            foreach (KeyValuePair<string, WidgetEntry> kv in widgets)
            {
                Window w = kv.Value.Window;
                try
                {
                    w.Opacity = 0;
                    w.Show();
                    SendToDesktopLayer(w);
                    DoubleAnimation fade = new DoubleAnimation(0, 1,
                        new Duration(TimeSpan.FromMilliseconds(260)));
                    w.BeginAnimation(Window.OpacityProperty, fade);
                }
                catch { }
            }
        }

        // ---------------------------------------------------------------- lookup
        private void Register(Window w, params string[] names)
        {
            foreach (string n in names)
            {
                object found = w.FindName(n);
                if (found is FrameworkElement) ui[n] = (FrameworkElement)found;
            }
        }

        private TextBlock Text(string name)
        {
            return ui.ContainsKey(name) ? ui[name] as TextBlock : null;
        }

        private void SetText(string name, string value)
        {
            TextBlock t = Text(name);
            if (t != null) t.Text = value;
        }

        private void SetBar(string name, double percent)
        {
            Border b = ui.ContainsKey(name) ? ui[name] as Border : null;
            if (b == null) return;
            double w = BarMax * percent / 100.0;
            if (w < 0) w = 0;
            if (w > BarMax) w = BarMax;
            b.Width = w;
        }

        // ---------------------------------------------------------------- timers
        private void StartTimers()
        {
            AddTimer(TimeSpan.FromSeconds(1), delegate { UpdateClock(); });
            AddTimer(TimeSpan.FromSeconds(3), delegate { UpdateStats(); });
            AddTimer(TimeSpan.FromSeconds(2), delegate { RefreshMedia(); });
            AddTimer(TimeSpan.FromMinutes(15), delegate { RefreshWeather(); });

            // Safety net: Explorer restarts, wallpaper changes and display switches
            // can shuffle the z-order. Re-park anything that is not focused.
            AddTimer(TimeSpan.FromSeconds(3), delegate
            {
                foreach (KeyValuePair<string, WidgetEntry> kv in widgets)
                {
                    if (!kv.Value.Window.IsActive) SendToDesktopLayer(kv.Value.Window);
                }
            });
        }

        private void AddTimer(TimeSpan interval, Action tick)
        {
            DispatcherTimer t = new DispatcherTimer();
            t.Interval = interval;
            t.Tick += delegate(object s, EventArgs e) { tick(); };
            t.Start();
        }

        // ---------------------------------------------------------------- clock
        private void UpdateClock()
        {
            DateTime now = DateTime.Now;
            SetText("TimeText", now.ToString("hh:mm", CultureInfo.CurrentCulture));
            SetText("AmPmText", now.ToString("tt", CultureInfo.CurrentCulture).ToUpperInvariant());
            SetText("DayText", now.ToString("dddd", CultureInfo.CurrentCulture));
            SetText("DateText", now.ToString("dd MMM yyyy", CultureInfo.CurrentCulture).ToUpperInvariant());
            UpdateBanner(now.ToString("dddd", CultureInfo.CurrentCulture).ToUpperInvariant());
            UpdateWaveProgress();
        }

        // ----------------------------------------------------------- tracking
        // Tracking is applied by laying out one TextBlock per letter and giving
        // each a right margin. WPF has no CharacterSpacing property - that one is
        // UWP only - and the obvious alternative, weaving spacing characters into
        // the string, can only step tracking in whole-glyph widths: the closest a
        // woven thin space got to the reference was 11.25 against a target of
        // 11.875, and no combination of Unicode spaces lands on it. A margin is
        // continuous, so the value below is exact.
        //
        // 0.4714 em is measured, not taste. In the reference the word THURSDAY
        // occupies an ink box of 190x16 px; Anurati's cap height is 0.8 em, which
        // puts the reference at a 20 px font size, and the width left over after
        // eight glyphs divided by the seven gaps gives this figure.
        private const double BannerTrackingEm = 0.4714;

        // The banner is drawn letter by letter, so colour can vary per glyph
        // without a gradient brush. Frozen once at type load: these two brushes
        // are shared by every letter and never change, and a frozen Freezable
        // skips WPF's per-assignment change-notification plumbing.
        private static readonly Brush BannerWhite =
            Freeze(new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)));
        private static readonly Brush BannerSky =
            Freeze(new SolidColorBrush(Color.FromArgb(0xF2, 0x87, 0xCE, 0xFA)));

        private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

        /// <summary>
        /// Colour for one letter of the weekday banner. Alternating glyphs read
        /// as a deliberate two-tone word rather than a rendering fault, which a
        /// random or lopsided split does not.
        /// </summary>
        private static Brush BannerLetterBrush(int index, int length)
        {
            return (index % 2 == 0) ? BannerWhite : BannerSky;
        }

        private string bannerDayShown = "";

        private void UpdateBanner(string day)
        {
            StackPanel row = ui.ContainsKey("BannerRow") ? ui["BannerRow"] as StackPanel : null;
            if (row == null) return;

            // The clock ticks every second; the word changes once a day.
            if (day == bannerDayShown) return;
            bannerDayShown = day;

            double tracking = BannerTrackingEm * BannerSize;
            row.Children.Clear();
            for (int i = 0; i < day.Length; i++)
            {
                TextBlock letter = new TextBlock();
                letter.Text = day[i].ToString();
                letter.Foreground = BannerLetterBrush(i, day.Length);
                // Trailing margin is the gap, so the final letter must not carry
                // one or the word sits off-centre by half a gap.
                if (i < day.Length - 1) letter.Margin = new Thickness(0, 0, tracking, 0);
                row.Children.Add(letter);
            }
        }

        // ---------------------------------------------------------------- stats
        private void UpdateStats()
        {
            StatsSnapshot s = stats.Read();

            if (s.CpuPercent >= 0)
            {
                SetText("CpuText", s.CpuPercent + "%");
                SetBar("CpuBar", s.CpuPercent);
            }

            if (s.RamPercent >= 0)
            {
                SetText("RamText", s.RamPercent + "%");
                SetBar("RamBar", s.RamPercent);
            }

            SetText("DiskText", s.DiskFreeGB >= 0
                ? Math.Round(s.DiskFreeGB).ToString(CultureInfo.CurrentCulture) + " GB FREE"
                : "");

            if (ui.ContainsKey("BattText"))
            {
                if (s.BatteryPercent >= 0)
                {
                    SetText("BattText", s.BatteryPercent + "%");
                    SetText("BattStatus", s.Charging ? "Charging" : "On battery");
                    SetBar("BattBar", s.BatteryPercent);
                }
                else
                {
                    SetText("BattText", "n/a");
                }
            }
        }

        // ---------------------------------------------------------------- media
        private void WireUpMedia()
        {
            Button prev = ui.ContainsKey("BtnPrev") ? ui["BtnPrev"] as Button : null;
            Button play = ui.ContainsKey("BtnPlay") ? ui["BtnPlay"] as Button : null;
            Button next = ui.ContainsKey("BtnNext") ? ui["BtnNext"] as Button : null;

            if (prev != null) prev.Click += delegate { Native.SendMediaKey(Native.VK_MEDIA_PREV); };
            if (play != null) play.Click += delegate { Native.SendMediaKey(Native.VK_MEDIA_PLAY); };
            if (next != null) next.Click += delegate { Native.SendMediaKey(Native.VK_MEDIA_NEXT); };
        }

        private async void RefreshMedia()
        {
            if (mediaBusy) return;      // a stalled player must not queue up calls
            mediaBusy = true;
            try
            {
                MediaSnapshot snap = await media.ReadAsync();
                ApplyMedia(snap);
            }
            catch { }
            finally { mediaBusy = false; }
        }

        private void ApplyMedia(MediaSnapshot snap)
        {
            Button play = ui.ContainsKey("BtnPlay") ? ui["BtnPlay"] as Button : null;

            mediaTrackKey = snap.TrackKey == null ? "" : snap.TrackKey;
            mediaCanSeek = snap.CanSeek;
            mediaHasTimeline = snap.HasTimeline;

            // Only advertise the wave as grabbable when the player will honour a
            // seek, so a dead click is never invited.
            Panel waveHost = ui.ContainsKey("WaveHost") ? ui["WaveHost"] as Panel : null;
            if (waveHost != null)
            {
                waveHost.Cursor = (mediaCanSeek && snap.HasTimeline) ? Cursors.Hand : Cursors.Arrow;
            }

            mediaPosition = snap.Position;
            mediaDuration = snap.Duration;
            mediaPositionAt = snap.PositionAt;
            mediaPlaying = snap.IsPlaying;

            if (!snap.HasSession)
            {
                SetText("MediaTitle", "Nothing playing");
                SetText("MediaArtist", "");
                if (play != null) play.Content = "\uE768";
                ClearArt();
                mediaHasTimeline = false;
                UpdateWaveProgress();
                return;
            }

            if (snap.Title != null) SetText("MediaTitle", snap.Title);
            if (snap.Artist != null) SetText("MediaArtist", snap.Artist);
            if (play != null) play.Content = snap.IsPlaying ? "\uE769" : "\uE768";
            // Three distinct cases, and conflating any two of them shows the
            // wrong thing:
            //   new picture   -> paint it
            //   art cleared   -> this track has none, run the equaliser
            //   nothing new   -> leave whatever is on screen alone
            if (snap.Thumbnail != null) SetArt(snap.Thumbnail);
            else if (snap.ArtCleared) ClearArt();

            UpdateWaveProgress();
        }

        // ----------------------------------------------------------- art fallback
        // Four bars, each with its own period and peak. The periods are chosen
        // not to be multiples of one another: whole-number ratios make the bars
        // resynchronise into a single block every few seconds, which is exactly
        // what gives a fake equaliser away.
        private static readonly string[] BeatBars = { "Beat1", "Beat2", "Beat3", "Beat4" };
        private static readonly double[] BeatPeriods = { 0.62, 0.47, 0.73, 0.55 };
        private static readonly double[] BeatPeaks = { 0.85, 1.00, 0.62, 0.92 };
        private const double BeatRest = 0.28;
        private const int BeatFrameRate = 15;

        /// <summary>
        /// Runs the equaliser while something is actually playing, and parks the
        /// bars at a flat resting height when it is not - a paused track that
        /// keeps dancing reads as a bug.
        /// </summary>
        private void SetBeat(bool running)
        {
            for (int i = 0; i < BeatBars.Length; i++)
            {
                Border bar = ui.ContainsKey(BeatBars[i]) ? ui[BeatBars[i]] as Border : null;
                if (bar == null) continue;
                ScaleTransform st = bar.RenderTransform as ScaleTransform;
                if (st == null) continue;

                if (!running)
                {
                    // Passing null hands the property back to its local value.
                    // Without it the bar freezes at whatever height the animation
                    // happened to be at when it stopped.
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    st.ScaleY = BeatRest;
                    continue;
                }

                DoubleAnimation a = new DoubleAnimation();
                a.From = BeatRest;
                a.To = BeatPeaks[i];
                a.Duration = new Duration(TimeSpan.FromSeconds(BeatPeriods[i]));
                a.AutoReverse = true;
                a.RepeatBehavior = RepeatBehavior.Forever;
                SineEase ease = new SineEase();
                ease.EasingMode = EasingMode.EaseInOut;
                a.EasingFunction = ease;
                // Every widget window sets AllowsTransparency=True, which puts
                // WPF into software rendering - there is no GPU compositing for
                // a layered window. On top of that the card carries a 28px
                // DropShadowEffect, and WPF re-runs an effect over the whole
                // subtree whenever any descendant changes. So each animation
                // frame re-blurs the entire media card on the CPU. At the
                // default 60fps that measured ~56% of one core; an equaliser
                // does not need anywhere near that many frames.
                Timeline.SetDesiredFrameRate(a, BeatFrameRate);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            }
        }

        private void ClearArt()
        {
            Border art = ui.ContainsKey("ArtBorder") ? ui["ArtBorder"] as Border : null;
            StackPanel beat = ui.ContainsKey("ArtBeat") ? ui["ArtBeat"] as StackPanel : null;
            if (art != null) art.Background = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255));
            if (beat != null) beat.Visibility = Visibility.Visible;
            SetBeat(mediaPlaying);
        }

        private void SetArt(byte[] bytes)
        {
            Border art = ui.ContainsKey("ArtBorder") ? ui["ArtBorder"] as Border : null;
            StackPanel beat = ui.ContainsKey("ArtBeat") ? ui["ArtBeat"] as StackPanel : null;
            if (art == null) return;
            try
            {
                BitmapImage bmp = new BitmapImage();
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;   // decode now, release the stream
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                }
                bmp.Freeze();
                ImageBrush brush = new ImageBrush(bmp);
                brush.Stretch = Stretch.UniformToFill;
                art.Background = brush;
                if (beat != null) beat.Visibility = Visibility.Collapsed;
                // Stop the equaliser once it is hidden: an animation on a
                // collapsed element still ticks the composition clock forever.
                SetBeat(false);
            }
            catch { ClearArt(); }
        }

        // ------------------------------------------------------------------ wave
        // The progress indicator is a sine wave drawn twice: a dim copy spanning
        // the whole track, and a lit copy on top revealed left to right.
        //
        // The lit copy is uncovered by moving a clip rectangle rather than by
        // rebuilding its path, so the geometry is built once and frozen and a
        // progress update costs one Rect assignment instead of re-sampling 150
        // points every second.
        private RectangleGeometry waveClip;

        private TimeSpan mediaPosition;
        private TimeSpan mediaDuration;
        private DateTimeOffset mediaPositionAt;
        private bool mediaPlaying;
        private bool mediaHasTimeline;
        private string mediaTrackKey = "";

        // Our own playback clock. Needed because a player is allowed to report a
        // position once and never refresh it: Spotify stamps Position=0 at track
        // start and leaves it there for the whole track. Extrapolating from that
        // fixed anchor is right only while playback is running, so the elapsed
        // time is accumulated here instead, advancing only while Playing.
        private string waveTrackKey = "";
        private double waveSeconds;
        private double waveAnchorPos = -1;
        private DateTime waveLastTick = DateTime.MinValue;

        private bool mediaCanSeek;
        private bool waveDragging;
        private double waveDragFraction;

        private void BuildWave()
        {
            ShapePath dim = ui.ContainsKey("WaveDim") ? ui["WaveDim"] as ShapePath : null;
            ShapePath lit = ui.ContainsKey("WaveLit") ? ui["WaveLit"] as ShapePath : null;
            if (dim == null || lit == null) return;

            Geometry wave = MakeWaveGeometry();
            dim.Data = wave;
            lit.Data = wave;          // one frozen geometry, referenced twice

            waveClip = new RectangleGeometry(new Rect(0, 0, 0, WaveHeight));
            lit.Clip = waveClip;
        }

        private static Geometry MakeWaveGeometry()
        {
            double mid = WaveHeight / 2;
            PathFigure figure = new PathFigure();
            figure.StartPoint = new Point(0, mid);

            PolyLineSegment segment = new PolyLineSegment();
            for (double x = WaveStep; x <= WaveWidth; x += WaveStep)
            {
                double y = mid + (WaveAmplitude *
                    Math.Sin(2 * Math.PI * WaveCycles * x / WaveWidth));
                segment.Points.Add(new Point(x, y));
            }
            figure.Segments.Add(segment);

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        private void UpdateWaveProgress()
        {
            if (waveClip == null) return;

            // While a scrub is in progress the pointer owns the wave: showing the
            // player's position instead would fight the finger.
            if (waveDragging)
            {
                waveClip.Rect = new Rect(0, 0, WaveWidth * waveDragFraction, WaveHeight);
                waveLastTick = DateTime.Now;
                return;
            }

            DateTime now = DateTime.Now;
            double fraction = 0;

            if (!mediaHasTimeline || mediaDuration <= TimeSpan.Zero)
            {
                waveTrackKey = "";
                waveAnchorPos = -1;
                waveSeconds = 0;
            }
            else
            {
                double reported = mediaPosition.TotalSeconds;
                double drift = (DateTimeOffset.Now - mediaPositionAt).TotalSeconds;
                if (drift < 0) drift = 0;
                double total = mediaDuration.TotalSeconds;

                if (mediaTrackKey != waveTrackKey)
                {
                    // A different track: take the player's word for it once.
                    waveTrackKey = mediaTrackKey;
                    waveAnchorPos = reported;
                    waveSeconds = reported + (mediaPlaying ? drift : 0);
                }
                else if (Math.Abs(reported - waveAnchorPos) > 0.75)
                {
                    // The reported position moved, so this player does keep it
                    // current - or the user seeked. Either way, believe it.
                    waveAnchorPos = reported;
                    waveSeconds = reported + (mediaPlaying ? drift : 0);
                }
                else if (mediaPlaying && waveLastTick != DateTime.MinValue)
                {
                    // The player is not refreshing its position, so run our own
                    // clock. Advancing only while Playing is what makes a pause
                    // hold the wave still rather than collapse or race ahead.
                    waveSeconds += (now - waveLastTick).TotalSeconds;
                }

                if (waveSeconds < 0) waveSeconds = 0;
                if (waveSeconds > total) waveSeconds = total;
                fraction = waveSeconds / total;
            }

            waveLastTick = now;
            waveClip.Rect = new Rect(0, 0, WaveWidth * fraction, WaveHeight);
        }

        // --------------------------------------------------------------- scrub
        private void WireUpWave()
        {
            Panel host = ui.ContainsKey("WaveHost") ? ui["WaveHost"] as Panel : null;
            if (host == null) return;

            host.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                // Marking this handled is what stops the window-wide drag handler
                // from picking the card up instead of seeking.
                e.Handled = true;
                if (!mediaCanSeek || mediaDuration <= TimeSpan.Zero) return;

                waveDragging = true;
                waveDragFraction = FractionFromPoint(host, e.GetPosition(host));
                host.CaptureMouse();
                UpdateWaveProgress();
            };

            host.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!waveDragging) return;
                waveDragFraction = FractionFromPoint(host, e.GetPosition(host));
                UpdateWaveProgress();
            };

            host.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!waveDragging) return;
                e.Handled = true;
                waveDragging = false;
                host.ReleaseMouseCapture();

                // One seek, on release. Seeking per mouse-move would fire dozens
                // of requests across a single drag, which players handle badly.
                CommitSeek(waveDragFraction);
            };

            // Losing capture (Explorer restart, display change) must not leave the
            // wave stuck under a drag that is no longer happening.
            host.LostMouseCapture += delegate { waveDragging = false; };
        }

        private static double FractionFromPoint(FrameworkElement host, Point p)
        {
            double width = host.ActualWidth > 0 ? host.ActualWidth : WaveWidth;
            double fraction = p.X / width;
            if (fraction < 0) fraction = 0;
            if (fraction > 1) fraction = 1;
            return fraction;
        }

        private async void CommitSeek(double fraction)
        {
            TimeSpan target = TimeSpan.FromSeconds(mediaDuration.TotalSeconds * fraction);

            // Hold the wave at the requested spot rather than snapping back to the
            // stale position while the player catches up. The re-anchor in
            // UpdateWaveProgress takes over once the player reports the new spot.
            waveSeconds = target.TotalSeconds;
            UpdateWaveProgress();

            try { await media.SeekAsync(target); }
            catch { }
        }

        // ---------------------------------------------------------------- weather
        // A single dropped request (Wi-Fi blip, DNS hiccup) should not blank the
        // card for a full 15-minute cycle: keep showing the last good reading and
        // retry sooner. Only the very first fetch (nothing to show yet) reports
        // "Weather unavailable" on failure.
        private void RefreshWeather()
        {
            weatherRetriesLeft = 3;
            RefreshWeatherAttempt();
        }

        private async void RefreshWeatherAttempt()
        {
            if (weatherConfig == null)
            {
                SetText("WxDesc", "Add weather.json");
                return;
            }
            if (weatherBusy) return;
            weatherBusy = true;
            try
            {
                WeatherConfig cfg = weatherConfig;
                WeatherReading r = await Task.Run(delegate { return Weather.Fetch(cfg); });
                if (r == null)
                {
                    OnWeatherFailed();
                    return;
                }
                string deg = "\u00B0";
                SetText("WxTemp", r.TempC + deg);
                SetText("WxDesc", r.Description);
                SetText("WxIcon", r.Glyph);
                SetText("WxPlace", r.Place);
                SetText("WxRange", "H:" + r.HighC + deg + "   L:" + r.LowC + deg);
                weatherEverSucceeded = true;
            }
            catch { OnWeatherFailed(); }
            finally { weatherBusy = false; }
        }

        private void OnWeatherFailed()
        {
            if (!weatherEverSucceeded) SetText("WxDesc", "Weather unavailable");
            if (weatherRetriesLeft <= 0) return;
            weatherRetriesLeft--;

            DispatcherTimer retry = new DispatcherTimer();
            retry.Interval = TimeSpan.FromSeconds(30);
            retry.Tick += delegate(object s, EventArgs e)
            {
                ((DispatcherTimer)s).Stop();
                RefreshWeatherAttempt();
            };
            retry.Start();
        }

        // ---------------------------------------------------------------- notes
        private void WireUpNotes()
        {
            TextBox box = ui.ContainsKey("NotesBox") ? ui["NotesBox"] as TextBox : null;
            if (box == null) return;

            suppressNotesSave = true;
            box.Text = store.LoadNotes();
            suppressNotesSave = false;

            box.TextChanged += delegate(object s, TextChangedEventArgs e)
            {
                if (suppressNotesSave) return;
                store.SaveNotes(((TextBox)s).Text);
            };
        }
    }
}
