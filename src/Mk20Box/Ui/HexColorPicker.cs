using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace Mk20Box.Ui
{
    /// <summary>
    /// The toolkit colour picker, exposing its value as the "#rrggbb" string the
    /// layout stores. Alpha is off because the device takes flat hex for key
    /// titles, and every widget colour is opaque.
    /// </summary>
    public class HexColorPicker : ColorPicker
    {
        public static readonly DependencyProperty HexColorProperty =
            DependencyProperty.Register(
                "HexColor",
                typeof(string),
                typeof(HexColorPicker),
                new FrameworkPropertyMetadata(
                    "#ffffff",
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnHexColorChanged));

        private bool syncing;

        static HexColorPicker()
        {
            // Without this the subclass has no style of its own and renders blank.
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(HexColorPicker),
                new FrameworkPropertyMetadata(typeof(ColorPicker)));
        }

        public HexColorPicker()
        {
            UsingAlphaChannel = false;
            ShowRecentColors = true;
            ShowStandardColors = false;
            AvailableColorsHeader = "MK20 palette";
            LoadPalette();
            SelectedColorChanged += (sender, args) => PushToHex();
        }

        /// <summary>
        /// Offers the icon-template colours as one-click swatches; the advanced tab
        /// still gives any colour at all.
        /// </summary>
        private void LoadPalette()
        {
            AvailableColors.Clear();

            foreach (var preset in Mk20Box.Layout.KeyTitleDefaults.Colors)
            {
                Color parsed;
                if (TryParse(preset.Value, out parsed))
                {
                    AvailableColors.Add(new ColorItem(parsed, preset.Key));
                }
            }
        }

        public string HexColor
        {
            get { return (string)GetValue(HexColorProperty); }
            set { SetValue(HexColorProperty, value); }
        }

        private static void OnHexColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((HexColorPicker)d).PullFromHex(e.NewValue as string);
        }

        private void PullFromHex(string value)
        {
            Color parsed;
            if (syncing || !TryParse(value, out parsed))
            {
                return;
            }

            syncing = true;
            try
            {
                SelectedColor = parsed;
            }
            finally
            {
                syncing = false;
            }
        }

        private void PushToHex()
        {
            if (syncing)
            {
                return;
            }

            Color color = SelectedColor ?? Colors.White;

            syncing = true;
            try
            {
                HexColor = string.Format(
                    CultureInfo.InvariantCulture,
                    "#{0:x2}{1:x2}{2:x2}",
                    color.R,
                    color.G,
                    color.B);
            }
            finally
            {
                syncing = false;
            }
        }

        private static bool TryParse(string value, out Color color)
        {
            color = Colors.White;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                object converted = ColorConverter.ConvertFromString(value.Trim());
                if (converted == null)
                {
                    return false;
                }

                color = (Color)converted;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
    }
}
