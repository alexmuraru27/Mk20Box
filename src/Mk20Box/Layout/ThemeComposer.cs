using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mk20Control.Protocol.Theme;
using Mk20Control.Protocol.Theme.Actions;
using Mk20Control.Protocol.Theme.Building;
using SixLabors.ImageSharp.Processing;

namespace Mk20Box.Layout
{
    /// <summary>
    /// Turns a stored layout into a device theme.
    ///
    /// Pages are created first so folder keys can reference a real page id, then each
    /// page is filled in - the order the MK20Control examples use, because
    /// <c>OpenPage</c> needs the target's GUID and the target must also declare its
    /// parent or the device refuses to leave it.
    /// </summary>
    public static class ThemeComposer
    {
        private const int CanvasWidth = 640;
        private const int CanvasHeight = 656;

        public static ThemeFile Compose(Mk20LayoutSettings layout)
        {
            if (layout == null || layout.Pages.Count == 0)
            {
                layout = Mk20LayoutSettings.CreateDefault();
            }

            var builder = new ThemeBuilder();
            var builders = new Dictionary<string, ThemePageBuilder>();

            foreach (Mk20PageSettings page in layout.Pages)
            {
                builders[page.Id] = builder.AddPage().SetCanvas(CanvasWidth, CanvasHeight);
            }

            // Declaring the parent is what makes a page a folder.
            foreach (Mk20PageSettings page in layout.Pages)
            {
                if (!string.IsNullOrEmpty(page.ParentPageId)
                    && builders.TryGetValue(page.ParentPageId, out ThemePageBuilder parent))
                {
                    builders[page.Id].AsFolderOf(parent);
                }
            }

            foreach (Mk20PageSettings page in layout.Pages)
            {
                ThemePageBuilder pageBuilder = builders[page.Id];

                // One shared strip when the layout asks for it, so every folder keeps
                // showing the same telemetry.
                Mk20PageSettings secondary = layout.GlobalSecondaryScreen ? layout.Pages[0] : page;

                AddBackgrounds(pageBuilder, page, secondary);

                foreach (Mk20KeySettings key in page.Keys)
                {
                    AddKey(pageBuilder, key, builders);
                }

                AddEncoder(pageBuilder, EncoderSide.Left, page.LeftEncoder);
                AddEncoder(pageBuilder, EncoderSide.Right, page.RightEncoder);
                AddWidgets(pageBuilder, secondary);
            }

            return builder.Build();
        }

        private static void AddBackgrounds(
            ThemePageBuilder page,
            Mk20PageSettings settings,
            Mk20PageSettings secondarySettings)
        {
            byte[] main = ReadFile(settings.BackgroundPath);
            if (main != null)
            {
                string name = Path.GetFileName(settings.BackgroundPath);
                page.AddDynamicImage(image => image.MainScreenBackgroundAutoFit(name, main));
            }

            byte[] secondary = ReadFile(secondarySettings.SecondaryBackgroundPath);
            if (secondary != null)
            {
                string name = Path.GetFileName(secondarySettings.SecondaryBackgroundPath);

                if (secondarySettings.SecondaryBackgroundFit)
                {
                    // Whole picture: padded to the strip so nothing is cut off, which
                    // matches how the device keeps the original proportions.
                    byte[] padded = FitToStrip(secondary);
                    page.AddDynamicImage(image => image.SecondaryScreenBackground(name, padded));
                    return;
                }

                // Offsets only pan the crop; the strip's on-device rectangle is fixed.
                double offsetX = Clamp(secondarySettings.SecondaryBackgroundOffsetX);
                double offsetY = Clamp(secondarySettings.SecondaryBackgroundOffsetY);

                page.AddDynamicImage(image =>
                    image.SecondaryScreenBackgroundAutoFit(name, secondary, offsetX, offsetY));
            }
        }

        private const int SecondaryWidth = 428;
        private const int SecondaryHeight = 142;
        private const int KeyIconSize = 128;

        /// <summary>The strip sits to the right of the left encoder.</summary>
        private const double SecondaryLeft = 106;

        /// <summary>
        /// Widgets sit above a screen background. The examples place the strip's clock
        /// at z 2 for the same reason.
        /// </summary>
        private const double WidgetZ = 2;

        /// <summary>
        /// Draws the page's widgets on the secondary screen. Bound widgets carry a
        /// channel name; the plugin pushes values under that name while a game runs.
        /// </summary>
        private static void AddWidgets(ThemePageBuilder page, Mk20PageSettings settings)
        {
            if (settings.Widgets == null)
            {
                return;
            }

            foreach (Mk20WidgetSettings widget in settings.Widgets)
            {
                // Whole pixels only: a dragged position can carry a long fraction, and
                // the device drops an item whose coordinate is not an integer.
                double x = Px(SecondaryLeft + widget.X);
                double y = Px(widget.Y);
                string channel = ChannelFor(widget);
                ThemeColor front = ParseColor(widget.Color, ThemeColor.White);

                var outline = widget as Mk20OutlineTextWidget;
                if (outline != null)
                {
                    AddOutlineText(page, outline, x, y, front, channel);
                    continue;
                }

                var text = widget as Mk20TextWidget;
                if (text != null)
                {
                    page.AddText(item =>
                    {
                        // z 2 keeps widgets above a screen background, as the examples do.
                        item.At(x, y, z: WidgetZ).Font(FontDescriptor(text.FontSize)).Color(front);

                        if (text.IsBound)
                        {
                            item.BoundTo(channel);
                        }
                        else
                        {
                            item.Text(text.Text ?? string.Empty);
                        }
                    });
                    continue;
                }

                var bar = widget as Mk20ProgressBarWidget;
                if (bar != null)
                {
                    page.AddProgressBar(item => item
                        .At(x, y, Px(bar.Width), Px(bar.Height), z: WidgetZ)
                        .BoundTo(channel, bar.Minimum, bar.Maximum)
                        .Colors(
                            front,
                            ParseColor(bar.BackColor, new ThemeColor(48, 58, 68)),
                            ParseColor(bar.BorderColor, ThemeColor.Black.WithAlpha(180)),
                            bar.BorderWidth,
                            bar.CornerRadius));
                    continue;
                }

                var clock = widget as Mk20ClockWidget;
                if (clock != null)
                {
                    AddClock(page, clock, x, y, front);
                }
            }
        }

        private static void AddOutlineText(
            ThemePageBuilder page,
            Mk20OutlineTextWidget widget,
            double x,
            double y,
            ThemeColor front,
            string channel)
        {
            page.AddShadowText(item =>
            {
                item.At(x, y, z: WidgetZ)
                    .Font(FontDescriptor(widget.FontSize))
                    .Color(front)
                    .Border(ParseColor(widget.OutlineColor, ThemeColor.Black), widget.OutlineWidth)

                    // The builder defaults to a visible shadow, so switch it off.
                    .Shadow(WireForm(ThemeColor.Transparent), 0);

                if (widget.IsBound)
                {
                    item.BoundTo(channel);
                }
                else
                {
                    item.Text(widget.Text ?? string.Empty);
                }
            });
        }

        /// <summary>
        /// A clock is one item per field, drawn left to right. There is no separator
        /// and no letter-spacing setting, so the box and the gap between boxes are
        /// the only spacing.
        /// </summary>
        private static void AddClock(
            ThemePageBuilder page,
            Mk20ClockWidget widget,
            double x,
            double y,
            ThemeColor front)
        {
            double boxWidth = Px(widget.SafeDigitWidth);
            double boxHeight = Px(widget.SafeDigitHeight);
            double pitch = Px(widget.FieldPitch);
            string font = FontDescriptor(widget.FontSize);

            string[] fields = widget.ShowSeconds
                ? new[] { "hour", "minute", "second" }
                : new[] { "hour", "minute" };

            for (int index = 0; index < fields.Length; index++)
            {
                string field = fields[index];
                double left = x + (index * pitch);

                page.AddDigitalClockField(clock => clock
                    .At(left, y, boxWidth, boxHeight, z: WidgetZ)
                    .Field(field)
                    .Font(font)
                    .Colors(front, ThemeColor.Transparent, ThemeColor.Transparent));
            }
        }

        /// <summary>
        /// Whole pixels. Confirmed on hardware: a clock at x=224 renders, while the
        /// same clock at x=224.0395833 does not appear at all.
        /// </summary>
        private static double Px(double value)
        {
            return Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>Stable per widget, so renaming a label keeps the binding working.</summary>
        public static string ChannelFor(Mk20WidgetSettings widget)
        {
            if (string.IsNullOrEmpty(widget.Channel))
            {
                widget.Channel = "mk20w" + Guid.NewGuid().ToString("N").Substring(0, 10);
            }

            return widget.Channel;
        }

        private static string FontDescriptor(double fontSize)
        {
            double size = fontSize < 6 ? 6 : fontSize;
            return FormattableString.Invariant($"Microsoft YaHei,{size:0},-1,5,50,0,0,0,0,0");
        }

        /// <summary>
        /// Widget colours must go out as "r=..,g=..,b=..,a=..". A ThemeColor parsed from
        /// text reproduces that same text, so a hex value is rebuilt from its components
        /// to force the wire form. Key titles are the exception and keep hex.
        /// </summary>
        private static ThemeColor ParseColor(string value, ThemeColor fallback)
        {
            ThemeColor parsed;
            if (string.IsNullOrWhiteSpace(value) || !ThemeColor.TryParse(value, out parsed))
            {
                return WireForm(fallback);
            }

            return WireForm(parsed);
        }

        private static ThemeColor WireForm(ThemeColor color)
        {
            return new ThemeColor(color.R, color.G, color.B, color.A);
        }

        /// <summary>
        /// Scales the picture to fit inside the strip, keeping its proportions and
        /// padding the rest with black. Every frame of a GIF is processed, so the
        /// animation survives.
        /// </summary>
        private static byte[] FitToStrip(byte[] imageOrGifBytes)
        {
            using (var source = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(imageOrGifBytes))
            {
                source.Mutate(context => context.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(SecondaryWidth, SecondaryHeight),
                    Mode = SixLabors.ImageSharp.Processing.ResizeMode.Pad,
                    PadColor = SixLabors.ImageSharp.Color.Black,
                }));

                using (var stream = new MemoryStream())
                {
                    if (source.Frames.Count > 1)
                    {
                        source.Save(stream, new SixLabors.ImageSharp.Formats.Gif.GifEncoder());
                    }
                    else
                    {
                        source.Save(stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                    }

                    return stream.ToArray();
                }
            }
        }

        /// <summary>The normalizer rejects anything outside [-1, 1].</summary>
        private static double Clamp(double offset)
        {
            return offset < -1 ? -1 : offset > 1 ? 1 : offset;
        }

        private static void AddKey(
            ThemePageBuilder page,
            Mk20KeySettings key,
            IDictionary<string, ThemePageBuilder> builders)
        {
            // A key with no action produces no wire traffic, so skip blank ones
            // unless they carry artwork worth drawing.
            bool hasArtwork = !string.IsNullOrWhiteSpace(key.MediaPath)
                || !string.IsNullOrWhiteSpace(key.Title);

            if (key.ActionType == KeyActionKinds.Unassigned && !hasArtwork)
            {
                return;
            }

            page.AddKey(key.Row, key.Column, item =>
            {
                ApplyIcon(item, key);

                if (!string.IsNullOrWhiteSpace(key.Title))
                {
                    item.Title(key.Title);
                    ApplyTitleStyle(item, key);
                }

                KeyAction action = BuildAction(key, builders);
                if (action != null)
                {
                    item.Action(action);
                }
            });
        }

        /// <summary>
        /// Uses the vendor's own artwork for navigation keys when the user has not
        /// chosen an icon, which is what real themes do.
        /// </summary>
        /// <summary>
        /// Applies the key's title style. Only fields that differ from the vendor
        /// default are sent, so untouched keys keep the device's native look.
        /// </summary>
        private static void ApplyTitleStyle(KeyItemBuilder item, Mk20KeySettings key)
        {
            double? fontSize = key.TitleFontSize > 0
                && Math.Abs(key.TitleFontSize - KeyTitleDefaults.FontSize) > 0.01
                    ? key.TitleFontSize
                    : (double?)null;

            string position = string.Equals(key.TitlePosition, "top", StringComparison.OrdinalIgnoreCase)
                ? "top"
                : null;

            ThemeColor parsed;
            ThemeColor? color =
                !string.IsNullOrWhiteSpace(key.TitleColor)
                && !string.Equals(key.TitleColor, KeyTitleDefaults.Color, StringComparison.OrdinalIgnoreCase)
                && ThemeColor.TryParse(key.TitleColor, out parsed)
                    ? parsed
                    : (ThemeColor?)null;

            if (fontSize != null || position != null || color != null)
            {
                item.TitleStyle(fontSize: fontSize, alignment: position, color: color);
            }
        }

        private static void ApplyIcon(KeyItemBuilder item, Mk20KeySettings key)
        {
            byte[] media = ReadFile(key.MediaPath);

            if (media != null)
            {
                string name = Path.GetFileName(key.MediaPath);

                // The builder always pads to the square, so cropping has to happen
                // first when the user wants the picture to fill the key.
                if (!key.IconFit)
                {
                    media = BackgroundImageNormalizer.ResizeToFill(
                        media,
                        KeyIconSize,
                        KeyIconSize,
                        Clamp(key.IconOffsetX),
                        Clamp(key.IconOffsetY));
                }

                if (name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    item.AnimatedIcon(Path.GetFileNameWithoutExtension(name), media);
                }
                else if (key.PreserveAlpha)
                {
                    item.IconPreservingAlpha(name, media);
                }
                else
                {
                    item.Icon(name, media);
                }

                return;
            }

            switch (key.ActionType)
            {
                case KeyActionKinds.OpenFolder:
                    item.IconDevice(DeviceIcon.OpenFolder);
                    break;

                case KeyActionKinds.OneLevelUp:
                    item.IconDevice(DeviceIcon.OneLevelUp);
                    break;

                case KeyActionKinds.PreviousPage:
                case KeyActionKinds.NextPage:
                    item.IconDevice(DeviceIcon.PageSwitch);
                    break;
            }
        }

        private static KeyAction BuildAction(
            Mk20KeySettings key,
            IDictionary<string, ThemePageBuilder> builders)
        {
            string label = string.IsNullOrWhiteSpace(key.Title) ? null : key.Title;

            switch (key.ActionType)
            {
                case KeyActionKinds.KeyboardKey:
                    return BuildKeystroke(key.Keystroke, label);

                case KeyActionKinds.OpenFolder:
                    ThemePageBuilder target;
                    return key.TargetPageId != null && builders.TryGetValue(key.TargetPageId, out target)
                        ? KeyActions.OpenPage(target.PageId, label)
                        : null;

                case KeyActionKinds.OneLevelUp:
                    return KeyActions.OneLevelUp(label);

                case KeyActionKinds.PreviousPage:
                    return KeyActions.PreviousPage(label);

                case KeyActionKinds.NextPage:
                    return KeyActions.NextPage(label);

                case KeyActionKinds.Macro:
                case KeyActionKinds.SimHubAction:
                case KeyActionKinds.SimHubInput:
                    // The device only reports these; the plugin does the work.
                    return KeyActions.Command(CommandIdFor(key), label);

                default:
                    return null;
            }
        }

        /// <summary>
        /// Stable id the device echoes back on press, so a binding survives the key
        /// being moved to another cell or page.
        /// </summary>
        public static string CommandIdFor(Mk20KeySettings key)
        {
            if (string.IsNullOrEmpty(key.CommandId))
            {
                key.CommandId = "mk20." + Guid.NewGuid().ToString("N").Substring(0, 12);
            }

            return key.CommandId;
        }

        private static KeyAction BuildKeystroke(Mk20KeystrokeSettings keystroke, string label)
        {
            HidKey hidKey;
            if (keystroke == null || !TryParseKey(keystroke, out hidKey))
            {
                return null;
            }

            KeyModifiers modifiers = ToModifiers(keystroke);

            return modifiers == KeyModifiers.None
                ? KeyActions.Keyboard(hidKey, keystroke.ToString(), label)
                : KeyActions.KeyboardCombo(modifiers, hidKey, keystroke.ToString(), label);
        }

        private static void AddEncoder(ThemePageBuilder page, EncoderSide side, Mk20EncoderSettings encoder)
        {
            if (encoder == null || encoder.Mode == EncoderModes.Unassigned)
            {
                return;
            }

            switch (encoder.Mode)
            {
                case EncoderModes.BuiltInFunction:
                    page.AddEncoder(side, key => key
                        .IconDevice(IconForFunction(encoder.Function))
                        .Opacity(0)
                        .Action(KeyActions.EncoderFunction(ToFunctionType(encoder.Function))));
                    break;

                case EncoderModes.Keystrokes:
                    page.AddEncoder(side, key => key
                        .IconDevice(DeviceIcon.EncoderKeyboard)
                        .Opacity(0)
                        .Action(KeyActions.EncoderKeyboard(
                            ToBinding(encoder.RotateLeft),
                            ToBinding(encoder.Click),
                            ToBinding(encoder.RotateRight))));
                    break;

                case EncoderModes.ReportToPlugin:
                    string id = string.IsNullOrWhiteSpace(encoder.CommandId)
                        ? "mk20.encoder." + side.ToString().ToLowerInvariant()
                        : encoder.CommandId;

                    page.AddEncoder(side, key => key
                        .IconDevice(DeviceIcon.EncoderKeyboard)
                        .Opacity(0)
                        .Action(KeyActions.Command(id)));
                    break;
            }
        }

        private static (KeyModifiers Modifiers, HidKey Key)? ToBinding(Mk20KeystrokeSettings keystroke)
        {
            HidKey hidKey;
            return keystroke != null && TryParseKey(keystroke, out hidKey)
                ? ((KeyModifiers, HidKey)?)(ToModifiers(keystroke), hidKey)
                : null;
        }

        private static bool TryParseKey(Mk20KeystrokeSettings keystroke, out HidKey hidKey)
        {
            hidKey = default(HidKey);
            return keystroke.HasKey && Enum.TryParse(keystroke.Key, out hidKey);
        }

        private static KeyModifiers ToModifiers(Mk20KeystrokeSettings keystroke)
        {
            KeyModifiers modifiers = KeyModifiers.None;

            if (keystroke.Ctrl) modifiers |= KeyModifiers.LeftCtrl;
            if (keystroke.Shift) modifiers |= KeyModifiers.LeftShift;
            if (keystroke.Alt) modifiers |= KeyModifiers.LeftAlt;
            if (keystroke.Win) modifiers |= KeyModifiers.LeftWin;

            return modifiers;
        }

        private static EncoderFunctionType ToFunctionType(string function)
        {
            switch (function)
            {
                case EncoderFunctions.DeviceVolume: return EncoderFunctionType.DeviceVolume;
                case EncoderFunctions.DeviceBrightness: return EncoderFunctionType.DeviceBrightness;
                case EncoderFunctions.SystemMedia: return EncoderFunctionType.SystemMedia;
                default: return EncoderFunctionType.SystemVolume;
            }
        }

        private static DeviceIcon IconForFunction(string function)
        {
            switch (function)
            {
                case EncoderFunctions.DeviceBrightness: return DeviceIcon.EncoderDeviceBrightness;
                case EncoderFunctions.SystemMedia: return DeviceIcon.EncoderSystemMedia;
                case EncoderFunctions.DeviceVolume: return DeviceIcon.EncoderDeviceVolume;
                default: return DeviceIcon.EncoderSystemVolume;
            }
        }

        private static byte[] ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                return File.ReadAllBytes(path);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
