Anurati-Regular.otf - used by the day banner widget.

Typeface:  Anurati, designed by Emmeran Richard
Licence:   free for PERSONAL use only
Source:    downloaded from font.download on 2026-09-11
Commercial use requires the paid release:
           https://www.emmeranrichard.fr/foundry/anurati-pro/

The widget loads this font from this folder BY PATH, not from the Windows font
list, so it is not installed system-wide and needs no admin rights. See
BannerFontSpec() in ..\src\WidgetApp.cs.

The font family name must be exactly "Anurati" for the banner to pick it up. If
it ever stops matching, the banner silently falls back to Century Gothic rather
than failing - run ..\check-font.ps1 to see what family names WPF actually finds
here.

Anurati is an uppercase-only display face. That is fine here because the banner
uppercases the weekday name before rendering, but it is why the file is only 8 KB
and why lowercase text would render as missing glyphs.

Tracking note: the banner spaces letters by 0.4714 em, derived by measuring the
reference screenshot rather than by eye. Anurati's cap height is exactly 0.8 em,
which is the constant that conversion depends on - see the comment above
BannerTrackingEm in ..\src\WidgetApp.cs before changing the font.
