using System;
using System.Windows;
using Mk20Box.Layout;

namespace Mk20Box.Ui
{
    /// <summary>
    /// Editable view over one widget. The concrete settings type varies by kind, so the
    /// properties that do not apply simply report defaults and stay hidden in the UI.
    /// </summary>
    public sealed class WidgetViewModel : ViewModelBase
    {
        private Mk20WidgetSettings model;

        public WidgetViewModel(Mk20WidgetSettings model)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public Mk20WidgetSettings Model => model;

        /// <summary>Raised when the kind changes, so the page can swap the stored type.</summary>
        public event EventHandler<WidgetReplacedEventArgs> Replaced;

        public string Description => model.Describe();

        /// <summary>Outlines this widget on the screen preview while it is being edited.</summary>
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool isSelected;

        private Mk20TextWidget AsText => model as Mk20TextWidget;

        private Mk20OutlineTextWidget AsOutline => model as Mk20OutlineTextWidget;

        private Mk20ProgressBarWidget AsBar => model as Mk20ProgressBarWidget;

        private Mk20ClockWidget AsClock => model as Mk20ClockWidget;

        public bool IsText => AsText != null;

        public bool IsOutline => AsOutline != null;

        public bool IsBar => AsBar != null;

        public bool IsClock => AsClock != null;

        /// <summary>Text and outline text draw as one string; the others do not.</summary>
        public bool IsPlainText => AsText != null;

        /// <summary>Fixed text only applies when nothing is streamed into it.</summary>
        public bool ShowsText => IsText && !model.IsBound;

        /// <summary>
        /// Changing the kind swaps the settings object, since each kind stores
        /// different fields. Shared values are carried across.
        /// </summary>
        public string Kind
        {
            get { return model.Kind; }
            set
            {
                if (model.Kind == value || !WidgetKinds.IsKnown(value))
                {
                    return;
                }

                Mk20WidgetSettings replacement = Mk20WidgetSettings.Create(value);
                model.CopyCommonTo(replacement);

                Mk20WidgetSettings previous = model;
                model = replacement;

                EventHandler<WidgetReplacedEventArgs> handler = Replaced;
                if (handler != null)
                {
                    handler(this, new WidgetReplacedEventArgs(previous, replacement));
                }

                RaiseAll();
            }
        }

        public string Label
        {
            get { return model.Label; }
            set
            {
                if (model.Label != value)
                {
                    model.Label = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public string Property
        {
            get { return model.Property; }
            set
            {
                if (model.Property != value)
                {
                    model.Property = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsBound));
                    OnPropertyChanged(nameof(ShowsText));
                    OnPropertyChanged(nameof(Description));
                    OnPropertyChanged(nameof(Preview));
                    OnPropertyChanged(nameof(DisplayWidth));
                }
            }
        }

        public bool IsBound => model.IsBound;

        public int Decimals
        {
            get { return model.Decimals; }
            set
            {
                if (model.Decimals != value)
                {
                    model.Decimals = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Preview));
                }
            }
        }

        public double X
        {
            get { return model.X; }
            set
            {
                // Whole pixels only - the device ignores a fractional coordinate.
                double rounded = Math.Round(value, MidpointRounding.AwayFromZero);

                if (Math.Abs(model.X - rounded) > 0.001)
                {
                    model.X = rounded;
                    OnPropertyChanged();
                }
            }
        }

        public double Y
        {
            get { return model.Y; }
            set
            {
                double rounded = Math.Round(value, MidpointRounding.AwayFromZero);

                if (Math.Abs(model.Y - rounded) > 0.001)
                {
                    model.Y = rounded;
                    OnPropertyChanged();
                }
            }
        }

        public string Color
        {
            get { return model.Color; }
            set
            {
                if (model.Color != value)
                {
                    model.Color = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Brush));
                }
            }
        }

        public System.Windows.Media.Brush Brush
        {
            get
            {
                try
                {
                    object converted = System.Windows.Media.ColorConverter.ConvertFromString(Color);
                    return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)converted);
                }
                catch (FormatException)
                {
                    return System.Windows.Media.Brushes.White;
                }
            }
        }

        // ---- text and outline text -------------------------------------------------

        public string Text
        {
            get { return AsText == null ? string.Empty : AsText.Text; }
            set
            {
                if (AsText != null && AsText.Text != value)
                {
                    AsText.Text = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Description));
                    OnPropertyChanged(nameof(Preview));
                    OnPropertyChanged(nameof(DisplayWidth));
                }
            }
        }

        public string Unit
        {
            get { return AsText == null ? string.Empty : AsText.Unit; }
            set
            {
                if (AsText != null && AsText.Unit != value)
                {
                    AsText.Unit = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Preview));
                }
            }
        }

        public double FontSize
        {
            get
            {
                if (AsText != null)
                {
                    return AsText.FontSize;
                }

                return AsClock != null ? AsClock.FontSize : 20;
            }

            set
            {
                double rounded = Math.Round(value, MidpointRounding.AwayFromZero);

                if (AsText != null)
                {
                    AsText.FontSize = rounded;
                }
                else if (AsClock != null)
                {
                    AsClock.FontSize = rounded;
                }
                else
                {
                    return;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayWidth));
                OnPropertyChanged(nameof(DisplayHeight));
                OnPropertyChanged(nameof(DigitWidth));
                OnPropertyChanged(nameof(DigitHeight));
                OnPropertyChanged(nameof(DigitMargin));
                OnPropertyChanged(nameof(DigitGroupMargin));
            }
        }

        // ---- outline text only -----------------------------------------------------

        public string OutlineColor
        {
            get { return AsOutline == null ? "#000000" : AsOutline.OutlineColor; }
            set
            {
                if (AsOutline != null && AsOutline.OutlineColor != value)
                {
                    AsOutline.OutlineColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public double OutlineWidth
        {
            get { return AsOutline == null ? 0 : AsOutline.OutlineWidth; }
            set
            {
                double rounded = Math.Round(value, MidpointRounding.AwayFromZero);

                if (AsOutline != null && Math.Abs(AsOutline.OutlineWidth - rounded) > 0.001)
                {
                    AsOutline.OutlineWidth = rounded;
                    OnPropertyChanged();
                }
            }
        }

        // ---- progress bar ----------------------------------------------------------

        public double Minimum
        {
            get { return AsBar == null ? 0 : AsBar.Minimum; }
            set
            {
                if (AsBar != null && Math.Abs(AsBar.Minimum - value) > 0.0001)
                {
                    AsBar.Minimum = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Maximum
        {
            get { return AsBar == null ? 100 : AsBar.Maximum; }
            set
            {
                if (AsBar != null && Math.Abs(AsBar.Maximum - value) > 0.0001)
                {
                    AsBar.Maximum = value;
                    OnPropertyChanged();
                }
            }
        }

        public double BarWidth
        {
            get { return AsBar == null ? 0 : AsBar.Width; }
            set
            {
                double rounded = Math.Round(value, MidpointRounding.AwayFromZero);

                if (AsBar != null && Math.Abs(AsBar.Width - rounded) > 0.001)
                {
                    AsBar.Width = rounded;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayWidth));
                }
            }
        }

        public double BarHeight
        {
            get { return AsBar == null ? 0 : AsBar.Height; }
            set
            {
                double rounded = Math.Round(value, MidpointRounding.AwayFromZero);

                if (AsBar != null && Math.Abs(AsBar.Height - rounded) > 0.001)
                {
                    AsBar.Height = rounded;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayHeight));
                }
            }
        }

        // ---- clock -----------------------------------------------------------------

        public bool ShowSeconds
        {
            get { return AsClock != null && AsClock.ShowSeconds; }
            set
            {
                if (AsClock != null && AsClock.ShowSeconds != value)
                {
                    AsClock.ShowSeconds = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ClockDigits));
                    OnPropertyChanged(nameof(DisplayWidth));
                }
            }
        }

        /// <summary>The digits are centred in this box, which scales with the font size.</summary>
        public double DigitWidth => AsClock == null ? 0 : AsClock.SafeDigitWidth;

        public double DigitHeight => AsClock == null ? 0 : AsClock.SafeDigitHeight;

        /// <summary>Negative values pull the pairs together; the device has no separator.</summary>
        public double Spacing
        {
            get { return AsClock == null ? 0 : AsClock.Spacing; }
            set
            {
                double rounded = Math.Round(value, MidpointRounding.AwayFromZero);

                if (AsClock != null && Math.Abs(AsClock.Spacing - rounded) > 0.001)
                {
                    AsClock.Spacing = rounded;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayWidth));
                    OnPropertyChanged(nameof(DigitMargin));
                    OnPropertyChanged(nameof(DigitGroupMargin));
                }
            }
        }

        /// <summary>
        /// The preview mirrors the device: every box is offset by the spacing, and the
        /// group is pulled back by one so only the gaps between boxes are affected.
        /// </summary>
        public Thickness DigitMargin => new Thickness(AppliedSpacing, 0, 0, 0);

        public Thickness DigitGroupMargin => new Thickness(-AppliedSpacing, 0, 0, 0);

        private double AppliedSpacing =>
            AsClock == null ? 0 : AsClock.FieldPitch - AsClock.SafeDigitWidth;

        /// <summary>
        /// The digit pairs as the device draws them: one per field, each centred in
        /// its own box, with no separator between them.
        /// </summary>
        public System.Collections.Generic.IList<string> ClockDigits
        {
            get
            {
                DateTime now = DateTime.Now;

                var digits = new System.Collections.Generic.List<string>
                {
                    now.ToString("HH"),
                    now.ToString("mm"),
                };

                if (ShowSeconds)
                {
                    digits.Add(now.ToString("ss"));
                }

                return digits;
            }
        }

        // ---- preview ---------------------------------------------------------------

        /// <summary>Footprint on the strip, so the editor can draw it in place.</summary>
        public double DisplayWidth => model.DisplayWidth;

        public double DisplayHeight => model.DisplayHeight;

        /// <summary>Bars are drawn part-filled so they read as a bar, not a box.</summary>
        public double PreviewFillWidth => Math.Max(0, (DisplayWidth - 4) * 0.6);

        /// <summary>Roughly what the widget will show, for the on-screen preview.</summary>
        public string Preview
        {
            get
            {
                if (IsClock)
                {
                    // Drawn as separate boxes in the preview, not as one string.
                    return string.Empty;
                }

                if (IsBar)
                {
                    return string.Empty;
                }

                if (model.IsBound)
                {
                    string sample = Decimals > 0 ? 0d.ToString("F" + Decimals) : "--";
                    return sample + (Unit ?? string.Empty);
                }

                return string.IsNullOrEmpty(Text) ? "text" : Text;
            }
        }

        /// <summary>Applies a preset from the telemetry list in one step.</summary>
        public void Apply(CommonTelemetry.Entry entry)
        {
            if (entry == null)
            {
                return;
            }

            Property = entry.Property;
            Decimals = entry.Decimals;

            if (AsText != null)
            {
                Unit = entry.Unit;
            }

            if (string.IsNullOrWhiteSpace(Label))
            {
                Label = entry.Label;
            }

            if (AsBar != null && entry.Maximum > entry.Minimum)
            {
                Minimum = entry.Minimum;
                Maximum = entry.Maximum;
            }
        }

        /// <summary>
        /// Moves the widget, keeping it inside the strip. The setters round, so a drag
        /// always lands on a whole pixel.
        /// </summary>
        public void MoveTo(double x, double y)
        {
            X = Clamp(x, 428 - DisplayWidth);
            Y = Clamp(y, 142 - DisplayHeight);
        }

        private static double Clamp(double value, double maximum)
        {
            double limit = maximum < 0 ? 0 : maximum;
            return value < 0 ? 0 : value > limit ? limit : value;
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(Kind));
            OnPropertyChanged(nameof(IsText));
            OnPropertyChanged(nameof(IsOutline));
            OnPropertyChanged(nameof(IsBar));
            OnPropertyChanged(nameof(IsClock));
            OnPropertyChanged(nameof(ShowsText));
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(Unit));
            OnPropertyChanged(nameof(FontSize));
            OnPropertyChanged(nameof(OutlineColor));
            OnPropertyChanged(nameof(OutlineWidth));
            OnPropertyChanged(nameof(Minimum));
            OnPropertyChanged(nameof(Maximum));
            OnPropertyChanged(nameof(BarWidth));
            OnPropertyChanged(nameof(BarHeight));
            OnPropertyChanged(nameof(ShowSeconds));
            OnPropertyChanged(nameof(ClockDigits));
            OnPropertyChanged(nameof(DigitWidth));
            OnPropertyChanged(nameof(DigitHeight));
            OnPropertyChanged(nameof(Spacing));
            OnPropertyChanged(nameof(DigitMargin));
            OnPropertyChanged(nameof(DigitGroupMargin));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(Preview));
            OnPropertyChanged(nameof(DisplayWidth));
            OnPropertyChanged(nameof(DisplayHeight));
        }
    }

    /// <summary>Carries the swap when a widget changes kind.</summary>
    public sealed class WidgetReplacedEventArgs : EventArgs
    {
        public WidgetReplacedEventArgs(Mk20WidgetSettings oldWidget, Mk20WidgetSettings newWidget)
        {
            OldWidget = oldWidget;
            NewWidget = newWidget;
        }

        public Mk20WidgetSettings OldWidget { get; private set; }

        public Mk20WidgetSettings NewWidget { get; private set; }
    }
}
