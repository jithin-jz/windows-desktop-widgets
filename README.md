# Desktop Widgets

macOS-style widget cards for the Windows desktop: clock, weather, media player
with a seekable wave progress bar, system stats, battery, notes, and a large
weekday banner across the top of the screen.

No installer bundle, no runtime download, no third-party app. It compiles from
source using the C# compiler that is already inside Windows.

---

## Install

### Easiest: download and double-click

1. Click the green **Code** button above, then **Download ZIP**
2. Extract it anywhere
3. Double-click **`install.cmd`**

No commands to type. If Windows shows a "protected your PC" notice, choose
**More info -> Run anyway** - it appears for any script downloaded from the web.

### One-liner

In PowerShell:

```powershell
irm https://raw.githubusercontent.com/jithin-jz/windows-desktop-widgets/main/install.ps1 | iex
```

Copy that **whole line**. It starts with `irm` and ends with `| iex`; pasting
only the URL makes PowerShell try to run the address as a command and fail with
`is not recognized as the name of a cmdlet`.

### From a clone

```powershell
git clone https://github.com/jithin-jz/windows-desktop-widgets.git
cd windows-desktop-widgets
.\install.ps1
```

The installer checks requirements, builds the exe, optionally asks for your city
for the weather card, registers a logon entry, and starts the widgets. It needs
**no admin rights**.

Useful switches: `-NoStartup` (don't run at logon), `-NoLaunch` (build only),
`-InstallDir <path>`.

Remove it with `.\uninstall.ps1` (add `-Purge` to also delete your layout and
notes).

### Updating

The installer also sets up a `dwx` command (no admin rights needed - it's
placed in `%LOCALAPPDATA%\Microsoft\WindowsApps`, which is on `PATH` by
default on Windows 10+):

```powershell
dwx version   # shows your installed version and the latest one on GitHub
dwx update    # re-runs the installer to pull and build the latest version
```

### Requirements

- Windows 10 or later (the media card uses WinRT media-session APIs)
- .NET Framework 4.x, which ships with Windows

That's the whole list. There is no .NET SDK, NuGet, Visual Studio or MSBuild
dependency.

---

## How it is built

### One exe, compiled by Windows itself

`build.cmd` invokes `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe` —
the C# compiler that ships as part of .NET Framework — and references
assemblies straight out of the GAC. There is no project file. The whole build is
one command and takes about a second.

Two details in there are not optional:

- **`/noconfig`.** Without it the compiler applies `csc.rsp`, which
  auto-references about forty legacy Framework assemblies. At least one now
  drags in the .NET Standard shim `System.ComponentModel.Primitives.dll`, and
  that assembly and `System.dll` hold type forwarders pointing at each other.
  The compiler rejects the cycle (`CS0731`) and then cannot resolve
  `ISupportInitialize`, which every WPF `Window` implements — so the build fails
  on the first line that mentions a window. `/noconfig` skips `csc.rsp`, making
  the curated list in `build.cmd` the complete reference set. Never add one of
  the small netstandard shims to it.
- **The individual `.winmd` references.** The media card needs
  `Windows.Media.Control`, and the unified `Windows.WinMD` facade is too old on
  many machines to contain it. So the build references
  `C:\Windows\System32\WinMetadata\*.winmd` directly, and `src\WinRtAsync.cs`
  hand-rolls the `IAsyncOperation` → `Task` bridge that
  `System.Runtime.WindowsRuntime` would normally provide.

### The widgets are separate transparent windows

Each card is its own borderless, transparent WPF `Window`, built at runtime by
`XamlReader.Load` from a markup string in `src\Markup.cs`. Keeping the markup as
strings avoids needing a XAML compiler in the build.

`src\Native.cs` does the desktop integration through P/Invoke:

- `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` — the card never takes focus and never
  appears in Alt+Tab. Mouse clicks still arrive, which is why the media buttons
  and the wave scrubber work on a window that cannot be focused.
- `SetWindowPos(..., HWND_BOTTOM, ...)` — parks each card at the bottom of the
  z-order so every app window draws over it. That is what makes them feel like
  they live *on* the desktop rather than floating above everything.

A 3-second timer re-parks any unfocused card, because Explorer restarts,
wallpaper changes and display switches all reshuffle the z-order.

The notes card is the one exception: it is created as interactive so you can
type in it, and it drops back to the desktop layer as soon as focus leaves.

### Why it stays small

Roughly 54 MB and a fraction of a percent of one CPU core, from three choices:

- **`RenderMode.SoftwareOnly`.** Hardware rendering maps the Intel GPU driver
  stack into the process — measured at about 81 MB of extra private bytes. Seven
  near-static cards do not need a GPU. This is the single largest saving
  available, and it is why adding anything continuously animated to this process
  is a bad trade.
- **`/platform:x86`.** Measured 12 MB lower private bytes than x64 for identical
  behaviour, because pointers and the NGEN images are half the size.
- **P/Invoke instead of WMI** for CPU, memory, disk and battery
  (`GetSystemTimes`, `GlobalMemoryStatusEx`, `GetDiskFreeSpaceEx`,
  `GetSystemPowerStatus`), so nothing wakes the WMI provider host every few
  seconds.

Timers are deliberately coarse: 1s clock, 2s media, 3s stats, 15min weather.

### Two techniques worth knowing

**Letter tracking.** WPF has no `CharacterSpacing` property — that is UWP only.
The weekday banner therefore lays out one `TextBlock` per letter with a right
margin, which is continuous and exact. Weaving spacing characters into the
string was the first attempt, but it can only step tracking in whole-glyph
widths and could not hit the target spacing.

**The media progress clock.** A player is allowed to report its position once and
never refresh it — Spotify stamps `Position = 0` at track start and leaves it
there for the whole track, relying on `LastUpdatedTime`. So the wave keeps its
own clock that advances only while playback is `Playing`, re-anchoring whenever
the player actually reports a changed position. That is correct both for players
that report live positions and for those that do not, and it makes a pause hold
the wave still instead of collapsing it to zero or letting it run ahead.

Seeking is gated on `Controls.IsPlaybackPositionEnabled`, which varies by player
— Chrome reports true, some players report false. The cursor reflects it so a
dead click is never invited.

---

## Layout and settings

Positions, lock state, notes and weather live in
`%LOCALAPPDATA%\KiroDesktopWidgets\`:

| File | Contents |
|---|---|
| `layout.json` | Card positions and the lock flag |
| `notes.txt` | The notes card |
| `weather.json` | `{ "name": "City", "lat": 0.0, "lon": 0.0 }` |

Drag a card to move it — positions snap to an 8px grid and save immediately.
Right-click any card for **Lock positions**, **Reset positions** and **Exit**.

Weather uses [Open-Meteo](https://open-meteo.com), which needs no API key.

---

## The display font

The weekday banner is designed for **Anurati** by Emmeran Richard.

**It is not included in this repo.** Anurati is licensed for personal use only,
so bundling it would be redistribution. Download it yourself and drop the `.otf`
into `fonts\`; the widget loads it *by path*, so it does not need to be
installed system-wide and needs no admin rights.

Without it the banner falls back to Century Gothic and everything still works —
it just won't have the cut-away letterforms the tracking was measured against.

Verify a font is being picked up with:

```powershell
.\check-font.ps1
```

The family name must be exactly `Anurati`. If a file declares something else,
the banner silently falls back rather than failing.

---

## Layout of this repo

```
VERSION              current release, compared by `dwx version`
build.cmd            one-command build, no SDK required
install.ps1          installer (also works piped from the web); also builds dwx
uninstall.ps1        removal, keeps settings unless -Purge
check-font.ps1       reports which font families WPF finds in fonts\
launcher/
  DesktopWidgets.vbs starts the exe with no console window; builds it if missing
cli/
  Dwx.cs             `dwx version` / `dwx update`, built by cli\build.cmd
  build.cmd          builds bin\dwx.exe
fonts/               drop Anurati.otf here (not redistributed)
src/
  WidgetApp.cs       app, layout, timers, wave progress, seeking
  Markup.cs          all widget XAML, as strings
  Native.cs          P/Invoke: window styles, z-order, stats, media keys
  MediaMonitor.cs    WinRT media session: metadata, art, timeline, seek
  WinRtAsync.cs      IAsyncOperation -> Task bridge
  Stats.cs           CPU, RAM, disk, battery
  Store.cs           layout / notes / weather persistence
  Weather.cs         Open-Meteo client
```

## Troubleshooting

**Cards vanished.** Explorer probably restarted. They re-park themselves within
3 seconds; if not, run the launcher again.

**Build fails with `CS0731` or `ISupportInitialize`.** `/noconfig` is missing
from `build.cmd` — see the build notes above.

**Banner shows the wrong font.** Run `check-font.ps1`; the family name must be
exactly `Anurati`.

**Media card says "Nothing playing" while music plays.** The player is not
reporting to the Windows media session. Check whether the volume flyout shows
the track — the widget reads the same source.

**Cards partly off-screen on a small display.** The card block is 688px tall;
on a short work area the last card can sit under the taskbar. Drag it where you
want it — positions persist.

---

## Licence

Code is [MIT](LICENSE).

The **Anurati** typeface is not covered by that licence and is not included in
this repository — it is licensed for personal use only. See
[the font section](#the-display-font).
