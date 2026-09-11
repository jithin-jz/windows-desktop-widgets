namespace KiroWidgets
{
    /// <summary>
    /// The widget markup, kept as strings and parsed at runtime by XamlReader so
    /// the visual design stays byte-identical to the original script. __SMALL__
    /// and __MEDIUM__ are substituted with the macOS card sizes before parsing.
    /// </summary>
    internal static class Markup
    {
        internal const string WindowHead =
@"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""KiroDesktopWidgets""
        WindowStyle=""None"" AllowsTransparency=""True"" Background=""Transparent""
        ShowInTaskbar=""False"" ResizeMode=""NoResize"" SizeToContent=""WidthAndHeight""
        ShowActivated=""False"" Topmost=""False"">";

        internal const string Styles =
@"  <Window.Resources>
    <Style x:Key=""Card"" TargetType=""Border"">
      <Setter Property=""CornerRadius"" Value=""20""/>
      <Setter Property=""Padding"" Value=""22,18,22,18""/>
      <Setter Property=""BorderThickness"" Value=""1""/>
      <Setter Property=""BorderBrush"" Value=""#2AFFFFFF""/>
      <Setter Property=""Background"" Value=""#B5141418""/>
      <Setter Property=""Effect"">
        <Setter.Value>
          <DropShadowEffect BlurRadius=""28"" ShadowDepth=""6"" Direction=""270"" Opacity=""0.45"" Color=""#FF000000""/>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key=""Cap"" TargetType=""TextBlock"">
      <Setter Property=""FontFamily"" Value=""Segoe UI""/>
      <Setter Property=""FontSize"" Value=""11""/>
      <Setter Property=""Foreground"" Value=""#8CFFFFFF""/>
      <Setter Property=""FontWeight"" Value=""SemiBold""/>
    </Style>
    <Style x:Key=""Val"" TargetType=""TextBlock"">
      <Setter Property=""FontFamily"" Value=""Segoe UI""/>
      <Setter Property=""FontSize"" Value=""15""/>
      <Setter Property=""Foreground"" Value=""#F2FFFFFF""/>
    </Style>
    <Style x:Key=""GlyphBtn"" TargetType=""Button"">
      <Setter Property=""FontFamily"" Value=""Segoe Fluent Icons, Segoe MDL2 Assets""/>
      <Setter Property=""FontSize"" Value=""13""/>
      <Setter Property=""Foreground"" Value=""#F2FFFFFF""/>
      <Setter Property=""Background"" Value=""Transparent""/>
      <Setter Property=""BorderThickness"" Value=""0""/>
      <Setter Property=""Width"" Value=""44""/>
      <Setter Property=""Height"" Value=""30""/>
      <Setter Property=""Cursor"" Value=""Hand""/>
      <Setter Property=""Template"">
        <Setter.Value>
          <ControlTemplate TargetType=""Button"">
            <Border x:Name=""bd"" CornerRadius=""10"" Background=""{TemplateBinding Background}"">
              <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property=""IsMouseOver"" Value=""True"">
                <Setter TargetName=""bd"" Property=""Background"" Value=""#26FFFFFF""/>
              </Trigger>
              <Trigger Property=""IsPressed"" Value=""True"">
                <Setter TargetName=""bd"" Property=""Background"" Value=""#40FFFFFF""/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>";

        /// <summary>
        /// The day banner: a bare, card-less line of text parked at the top centre
        /// of the screen. Unlike the six cards it has no Card background, so only
        /// the letters sit on the wallpaper. __BANNERW__ is substituted with a fixed
        /// width because the Window uses SizeToContent, and a known width is what
        /// makes the top-centre position computable before the window is shown.
        /// The drop shadow is not decoration - it keeps the thin strokes legible
        /// over a light wallpaper.
        /// </summary>
        internal const string DayBanner =
@"  <Border Width=""__BANNERW__"" Height=""92"" Background=""Transparent"">
    <Border.Resources>
      <!-- Implicit style, so it reaches every letter TextBlock the code adds to
           BannerRow without the code having to know about fonts or colours. -->
      <Style TargetType=""TextBlock"">
        <Setter Property=""FontFamily"" Value=""__BANNERFONT__""/>
        <Setter Property=""FontSize"" Value=""__BANNERSIZE__""/>
        <Setter Property=""FontWeight"" Value=""__BANNERWEIGHT__""/>
        <Setter Property=""Foreground"" Value=""#F2FFFFFF""/>
      </Style>
    </Border.Resources>
    <StackPanel x:Name=""BannerRow"" Orientation=""Horizontal""
                HorizontalAlignment=""Center"" VerticalAlignment=""Center"">
      <!-- One shadow for the whole word rather than one per letter: it keeps the
           thin strokes legible over a light wallpaper without the overlapping
           haloes that per-letter effects produce. -->
      <StackPanel.Effect>
        <DropShadowEffect BlurRadius=""18"" ShadowDepth=""0"" Opacity=""0.55"" Color=""#FF000000""/>
      </StackPanel.Effect>
    </StackPanel>
  </Border>";

        internal const string Clock =
@"  <Border Style=""{StaticResource Card}"" Width=""__SMALL__"" Height=""__SMALL__"" Padding=""18,16,18,16"">
    <StackPanel VerticalAlignment=""Center"">
      <TextBlock x:Name=""TimeText"" Text=""00:00"" FontFamily=""Segoe UI"" FontSize=""42""
                 FontWeight=""Thin"" Foreground=""White""/>
      <TextBlock x:Name=""AmPmText"" Text=""AM"" Style=""{StaticResource Cap}"" Margin=""2,-2,0,0""/>
      <TextBlock x:Name=""DayText"" Text=""Day"" FontFamily=""Segoe UI"" FontSize=""14""
                 FontWeight=""Light"" Foreground=""#E8FFFFFF"" Margin=""2,14,0,0""/>
      <TextBlock x:Name=""DateText"" Text=""Date"" Style=""{StaticResource Cap}"" Margin=""2,2,0,0""/>
    </StackPanel>
  </Border>";

        internal const string Weather =
@"  <Border Style=""{StaticResource Card}"" Width=""__SMALL__"" Height=""__SMALL__"" Padding=""18,16,18,16"">
    <StackPanel>
      <TextBlock x:Name=""WxPlace"" Text=""WEATHER"" Style=""{StaticResource Cap}""
                 TextTrimming=""CharacterEllipsis""/>
      <StackPanel Orientation=""Horizontal"" Margin=""0,8,0,0"">
        <TextBlock x:Name=""WxTemp"" Text=""--"" FontFamily=""Segoe UI"" FontSize=""38""
                   FontWeight=""Thin"" Foreground=""White""/>
        <TextBlock x:Name=""WxIcon"" Text=""&#xE9CA;"" FontFamily=""Segoe Fluent Icons, Segoe MDL2 Assets""
                   FontSize=""22"" Foreground=""#E0FFFFFF"" VerticalAlignment=""Center"" Margin=""8,4,0,0""/>
      </StackPanel>
      <TextBlock x:Name=""WxDesc"" Text=""Not set"" Style=""{StaticResource Val}"" FontSize=""12""
                 Foreground=""#E0FFFFFF"" TextTrimming=""CharacterEllipsis"" Margin=""1,2,0,0""/>
      <TextBlock x:Name=""WxRange"" Text="""" Style=""{StaticResource Cap}"" Margin=""1,10,0,0""/>
    </StackPanel>
  </Border>";

        internal const string Media =
@"  <Border Style=""{StaticResource Card}"" Width=""__MEDIUM__"" Height=""__SMALL__"" Padding=""18,16,18,16"">
    <Grid>
      <!-- The original two-column layout is nested here untouched. Because it is
           a single child of this overlay root it measures exactly as it did
           before, so adding the wave cannot reflow the art, text or buttons. -->
      <Grid>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width=""Auto""/><ColumnDefinition Width=""*""/>
        </Grid.ColumnDefinitions>
        <Border x:Name=""ArtBorder"" Width=""96"" Height=""96"" CornerRadius=""14""
                Background=""#26FFFFFF"" VerticalAlignment=""Center"" Margin=""0,0,16,0"">
          <TextBlock x:Name=""ArtGlyph"" Text=""&#xE8D6;"" FontFamily=""Segoe Fluent Icons, Segoe MDL2 Assets""
                     FontSize=""28"" Foreground=""#8CFFFFFF""
                     HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
        </Border>
        <StackPanel Grid.Column=""1"" VerticalAlignment=""Center"">
          <TextBlock Text=""NOW PLAYING"" Style=""{StaticResource Cap}"" Margin=""0,0,0,6""/>
          <TextBlock x:Name=""MediaTitle"" Text=""Nothing playing"" FontFamily=""Segoe UI"" FontSize=""14""
                     Foreground=""#F2FFFFFF"" MaxWidth=""176"" TextTrimming=""CharacterEllipsis""/>
          <TextBlock x:Name=""MediaArtist"" Text="""" Style=""{StaticResource Val}"" FontSize=""11""
                     Foreground=""#8CFFFFFF"" MaxWidth=""176"" TextTrimming=""CharacterEllipsis"" Margin=""0,3,0,0""/>
          <StackPanel Orientation=""Horizontal"" HorizontalAlignment=""Left"" Margin=""-10,8,0,0"">
            <Button x:Name=""BtnPrev"" Style=""{StaticResource GlyphBtn}"" Content=""&#xE892;""/>
            <Button x:Name=""BtnPlay"" Style=""{StaticResource GlyphBtn}"" Content=""&#xE768;"" FontSize=""17""/>
            <Button x:Name=""BtnNext"" Style=""{StaticResource GlyphBtn}"" Content=""&#xE893;""/>
          </StackPanel>
        </StackPanel>
      </Grid>

      <!-- Progress wave. It sits in the card's bottom padding via a negative
           margin, so it claims no layout height and the existing content keeps
           its exact position. Two copies of one geometry: a dim full-width wave
           and a lit copy revealed left-to-right by a clip. -->
      <!-- Background is Transparent on purpose: a Path is hit-tested only on its
           1.6px stroke, which is far too thin to click. The transparent panel
           gives the whole strip a hit area for seeking. -->
      <Grid x:Name=""WaveHost"" Width=""__WAVEW__"" Height=""__WAVEH__""
            HorizontalAlignment=""Center"" VerticalAlignment=""Bottom""
            Margin=""0,0,0,-6"" Background=""Transparent"">
        <Path x:Name=""WaveDim"" Stroke=""#33FFFFFF"" StrokeThickness=""1.6""
              StrokeStartLineCap=""Round"" StrokeEndLineCap=""Round""/>
        <!-- The lit wave carries the whole green-to-rose ramp across its full
             width, and the clip reveals it left to right. So the colour at the
             leading edge is the colour for that point in the track: green near
             the start, amber through the middle, rose towards the end. -->
        <Path x:Name=""WaveLit"" StrokeThickness=""1.6""
              StrokeStartLineCap=""Round"" StrokeEndLineCap=""Round"">
          <Path.Stroke>
            <LinearGradientBrush StartPoint=""0,0"" EndPoint=""1,0"">
              <GradientStop Offset=""0"" Color=""#34D399""/>
              <GradientStop Offset=""0.5"" Color=""#FBA94C""/>
              <GradientStop Offset=""1"" Color=""#FB7185""/>
            </LinearGradientBrush>
          </Path.Stroke>
        </Path>
      </Grid>
    </Grid>
  </Border>";

        internal const string SysStats =
@"  <Border Style=""{StaticResource Card}"" Width=""__SMALL__"" Height=""__SMALL__"" Padding=""18,16,18,16"">
    <StackPanel>
      <TextBlock Text=""SYSTEM"" Style=""{StaticResource Cap}"" Margin=""0,0,0,12""/>
      <Grid Margin=""0,0,0,5"">
        <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
        <TextBlock Text=""CPU"" Style=""{StaticResource Val}"" FontSize=""11"" Foreground=""#B8FFFFFF""/>
        <TextBlock x:Name=""CpuText"" Grid.Column=""1"" Text=""--"" Style=""{StaticResource Val}""/>
      </Grid>
      <Border Height=""4"" CornerRadius=""2"" Background=""#1FFFFFFF"" Margin=""0,0,0,12"">
        <Border x:Name=""CpuBar"" HorizontalAlignment=""Left"" Width=""0"" Height=""4"" CornerRadius=""2"" Background=""#FF5E9BFF""/>
      </Border>
      <Grid Margin=""0,0,0,5"">
        <Grid.ColumnDefinitions><ColumnDefinition Width=""*""/><ColumnDefinition Width=""Auto""/></Grid.ColumnDefinitions>
        <TextBlock Text=""RAM"" Style=""{StaticResource Val}"" FontSize=""11"" Foreground=""#B8FFFFFF""/>
        <TextBlock x:Name=""RamText"" Grid.Column=""1"" Text=""--"" Style=""{StaticResource Val}""/>
      </Grid>
      <Border Height=""4"" CornerRadius=""2"" Background=""#1FFFFFFF"" Margin=""0,0,0,12"">
        <Border x:Name=""RamBar"" HorizontalAlignment=""Left"" Width=""0"" Height=""4"" CornerRadius=""2"" Background=""#FF7DDE8A""/>
      </Border>
      <TextBlock x:Name=""DiskText"" Text="""" Style=""{StaticResource Cap}""/>
    </StackPanel>
  </Border>";

        internal const string Notes =
@"  <Border Style=""{StaticResource Card}"" Width=""__MEDIUM__"" Height=""__SMALL__"" Padding=""20,16,20,16"">
    <StackPanel>
      <TextBlock Text=""NOTES"" Style=""{StaticResource Cap}"" Margin=""0,0,0,8""/>
      <TextBox x:Name=""NotesBox"" Height=""96"" Background=""Transparent"" BorderThickness=""0""
               Foreground=""#F2FFFFFF"" FontFamily=""Segoe UI"" FontSize=""12""
               TextWrapping=""Wrap"" AcceptsReturn=""True"" VerticalScrollBarVisibility=""Auto""
               CaretBrush=""White"" SelectionOpacity=""0.3""/>
    </StackPanel>
  </Border>";

        internal const string Battery =
@"  <Border Style=""{StaticResource Card}"" Width=""__SMALL__"" Height=""__SMALL__"" Padding=""18,16,18,16"">
    <StackPanel>
      <TextBlock Text=""BATTERY"" Style=""{StaticResource Cap}""/>
      <TextBlock x:Name=""BattText"" Text=""--"" FontFamily=""Segoe UI"" FontSize=""40""
                 FontWeight=""Thin"" Foreground=""White"" Margin=""0,10,0,0""/>
      <TextBlock x:Name=""BattStatus"" Text=""--"" Style=""{StaticResource Val}"" FontSize=""12""
                 Foreground=""#E0FFFFFF"" Margin=""1,2,0,0""/>
      <Border Height=""5"" CornerRadius=""3"" Background=""#1FFFFFFF"" Margin=""0,14,0,0"">
        <Border x:Name=""BattBar"" HorizontalAlignment=""Left"" Width=""0"" Height=""5"" CornerRadius=""3"" Background=""#FF7DDE8A""/>
      </Border>
    </StackPanel>
  </Border>";
    }
}
