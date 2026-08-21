using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mk20Box.Layout
{
    /// <summary>
    /// A widget drawn on the secondary screen. Each kind takes different settings, so
    /// each has its own type; <see cref="Kind"/> is the discriminator used on load.
    /// </summary>
    [JsonConverter(typeof(Mk20WidgetConverter))]
    public abstract class Mk20WidgetSettings
    {
        /// <summary>Identifies the concrete type. Persisted, so keep the values stable.</summary>
        public abstract string Kind { get; }

        /// <summary>Name shown in the editor list.</summary>
        public string Label { get; set; }

        /// <summary>
        /// SimHub property to stream, e.g. "DataCorePlugin.GameData.SpeedKmh". Empty
        /// means the widget is not bound to live data.
        /// </summary>
        public string Property { get; set; }

        /// <summary>Decimal places for numeric values; -1 keeps the raw text.</summary>
        public int Decimals { get; set; }

        /// <summary>Position within the 428 x 142 strip.</summary>
        public double X { get; set; } = 20;

        public double Y { get; set; } = 20;

        public string Color { get; set; } = "#ffffff";

        /// <summary>Id the device binds to, kept stable so a rename does not break it.</summary>
        public string Channel { get; set; }

        public bool IsBound => !string.IsNullOrWhiteSpace(Property);

        /// <summary>Appended to the pushed value. Bars must stay bare numbers.</summary>
        public virtual string ValueSuffix => null;

        /// <summary>Footprint on the strip, used for the editor preview.</summary>
        public abstract double DisplayWidth { get; }

        public abstract double DisplayHeight { get; }

        public virtual string Describe()
        {
            string what = string.IsNullOrWhiteSpace(Label)
                ? (IsBound ? ShortProperty(Property) : null)
                : Label;

            return string.IsNullOrWhiteSpace(what) ? Kind : Kind + " - " + what;
        }

        /// <summary>Trims the long plugin prefix so lists stay readable.</summary>
        public static string ShortProperty(string property)
        {
            if (string.IsNullOrEmpty(property))
            {
                return string.Empty;
            }

            int last = property.LastIndexOf('.');
            return last >= 0 && last < property.Length - 1
                ? property.Substring(last + 1)
                : property;
        }

        /// <summary>Creates the type matching a kind name.</summary>
        public static Mk20WidgetSettings Create(string kind)
        {
            switch (kind)
            {
                case WidgetKinds.OutlineText:
                    return new Mk20OutlineTextWidget();

                case WidgetKinds.ProgressBar:
                    return new Mk20ProgressBarWidget();

                case WidgetKinds.Clock:
                    return new Mk20ClockWidget();

                default:
                    return new Mk20TextWidget();
            }
        }

        /// <summary>Carries the shared settings over when the user changes the kind.</summary>
        public void CopyCommonTo(Mk20WidgetSettings target)
        {
            target.Label = Label;
            target.Property = Property;
            target.Decimals = Decimals;
            target.X = X;
            target.Y = Y;
            target.Color = Color;
            target.Channel = Channel;

            var fromText = this as Mk20TextWidget;
            var toText = target as Mk20TextWidget;

            if (fromText != null && toText != null)
            {
                toText.Text = fromText.Text;
                toText.Unit = fromText.Unit;
                toText.FontSize = fromText.FontSize;
            }
        }
    }

    /// <summary>Plain text, static or streamed. Maps to a type 113 item.</summary>
    public class Mk20TextWidget : Mk20WidgetSettings
    {
        public override string Kind => WidgetKinds.Text;

        /// <summary>Shown when nothing is streamed into the widget.</summary>
        public string Text { get; set; } = "text";

        /// <summary>Appended to the value, e.g. "%" or " L".</summary>
        public string Unit { get; set; }

        public double FontSize { get; set; } = 20;

        public override string ValueSuffix => Unit;

        /// <summary>Rough text extent, since the device gives no measured size.</summary>
        public override double DisplayWidth
        {
            get
            {
                string sample = IsBound
                    ? new string('0', Math.Max(3, Decimals > 0 ? Decimals + 3 : 3)) + (Unit ?? string.Empty)
                    : (Text ?? string.Empty);

                return Math.Max(20, sample.Length * FontSize * 0.6);
            }
        }

        public override double DisplayHeight => FontSize * 1.35;

        public override string Describe()
        {
            if (!IsBound && string.IsNullOrWhiteSpace(Label) && !string.IsNullOrWhiteSpace(Text))
            {
                return Kind + " - " + Text;
            }

            return base.Describe();
        }
    }

    /// <summary>
    /// Text with a stroke around it, so it stays readable over artwork. Maps to a
    /// type 117 item, whose drop shadow is switched off - the outline alone is what
    /// this widget offers.
    /// </summary>
    public sealed class Mk20OutlineTextWidget : Mk20TextWidget
    {
        public override string Kind => WidgetKinds.OutlineText;

        public string OutlineColor { get; set; } = "#000000";

        public double OutlineWidth { get; set; } = 3;
    }

    /// <summary>
    /// A bar that fills between a minimum and maximum. Maps to a type 102 item, which
    /// carries its own size, border and corner radius.
    /// </summary>
    public sealed class Mk20ProgressBarWidget : Mk20WidgetSettings
    {
        public override string Kind => WidgetKinds.ProgressBar;

        public double Width { get; set; } = 160;

        public double Height { get; set; } = 22;

        public double Minimum { get; set; }

        public double Maximum { get; set; } = 100;

        public string BackColor { get; set; } = "#303a44";

        public string BorderColor { get; set; } = "#000000";

        public double BorderWidth { get; set; } = 2;

        public double CornerRadius { get; set; } = 5;

        public override double DisplayWidth => Width;

        public override double DisplayHeight => Height;
    }

    /// <summary>
    /// A clock, drawn as one item per field. The device has no clock of its own, so the
    /// plugin pushes the time like any other value.
    /// </summary>
    public sealed class Mk20ClockWidget : Mk20WidgetSettings
    {
        /// <summary>
        /// The vendor's documented reference point: 64 x 52 at 28pt is about the
        /// smallest that still reads cleanly. The box scales from this, because the
        /// digits are clipped to it - a larger font in a fixed box does nothing.
        /// </summary>
        public const double ComfortableDigitWidth = 64;

        public const double ComfortableDigitHeight = 52;

        public const double ReferenceFontSize = 28;

        public override string Kind => WidgetKinds.Clock;

        public double FontSize { get; set; } = ReferenceFontSize;

        /// <summary>Seconds are optional; hours and minutes are always drawn.</summary>
        public bool ShowSeconds { get; set; } = true;

        /// <summary>
        /// Extra pixels between one digit box and the next. Negative pulls the pairs
        /// closer, which is the only way to tighten a clock: type 111 carries just the
        /// field name, digit count, font and colours - there is no letter-spacing.
        /// </summary>
        public double Spacing { get; set; }

        public int FieldCount => ShowSeconds ? 3 : 2;

        /// <summary>
        /// The box each digit pair is drawn into, scaled from the vendor reference so
        /// the digits always fit. Floored so a tiny font cannot collapse the item.
        /// </summary>
        public double SafeDigitWidth => ScaleFromReference(ComfortableDigitWidth);

        public double SafeDigitHeight => ScaleFromReference(ComfortableDigitHeight);

        private double ScaleFromReference(double atReference)
        {
            double scaled = Math.Round(
                FontSize * (atReference / ReferenceFontSize),
                MidpointRounding.AwayFromZero);

            return Math.Max(16, scaled);
        }

        /// <summary>
        /// Distance from one box's left edge to the next. Floored so a large negative
        /// spacing cannot stack the pairs on top of each other.
        /// </summary>
        public double FieldPitch => Math.Max(20, SafeDigitWidth + Spacing);

        public override double DisplayWidth => (FieldPitch * (FieldCount - 1)) + SafeDigitWidth;

        public override double DisplayHeight => SafeDigitHeight;
    }

    /// <summary>Widget kind names. Persisted verbatim, so keep the values stable.</summary>
    public static class WidgetKinds
    {
        public const string Text = "Text";
        public const string OutlineText = "Text with outline";
        public const string ProgressBar = "Progress bar";
        public const string Clock = "Clock";

        public static readonly string[] All = { Text, OutlineText, ProgressBar, Clock };

        public static bool IsKnown(string kind)
        {
            return Array.IndexOf(All, kind) >= 0;
        }
    }

    /// <summary>
    /// Reads the right widget type back from settings. Newtonsoft cannot pick a subclass
    /// on its own, and writing type names into the file would tie the format to this
    /// assembly, so the stored "Kind" is used instead.
    /// </summary>
    public sealed class Mk20WidgetConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return typeof(Mk20WidgetSettings).IsAssignableFrom(objectType);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            JObject source = JObject.Load(reader);
            JToken kind;
            string name = source.TryGetValue("Kind", StringComparison.OrdinalIgnoreCase, out kind)
                ? (string)kind
                : WidgetKinds.Text;

            Mk20WidgetSettings widget = Mk20WidgetSettings.Create(name);

            // Populated with a plain serializer so this converter is not re-entered.
            using (JsonReader nested = source.CreateReader())
            {
                new JsonSerializer().Populate(nested, widget);
            }

            return widget;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// SimHub properties worth offering up front. The picker also lists everything the
    /// running game exposes, so this is a shortcut rather than a limit.
    /// </summary>
    public static class CommonTelemetry
    {
        public sealed class Entry
        {
            public Entry(string label, string property, string unit, double minimum, double maximum, int decimals)
            {
                Label = label;
                Property = property;
                Unit = unit;
                Minimum = minimum;
                Maximum = maximum;
                Decimals = decimals;
            }

            public string Label { get; private set; }

            public string Property { get; private set; }

            public string Unit { get; private set; }

            public double Minimum { get; private set; }

            public double Maximum { get; private set; }

            public int Decimals { get; private set; }

            public override string ToString()
            {
                return Label;
            }
        }

        private const string Game = "GameData.";

        public static readonly List<Entry> Entries = new List<Entry>
        {
            new Entry("Speed (km/h)", Game + "SpeedKmh", "", 0, 320, 0),
            new Entry("Speed (mph)", Game + "SpeedMph", "", 0, 200, 0),
            new Entry("RPM", Game + "Rpms", "", 0, 9000, 0),
            new Entry("Gear", Game + "Gear", "", 0, 8, -1),
            new Entry("Fuel left", Game + "Fuel", " L", 0, 100, 1),
            new Entry("Fuel percent", Game + "FuelPercent", "%", 0, 100, 0),
            new Entry("Laps of fuel left", Game + "EstimatedFuelRemaingLaps", "", 0, 30, 1),
            new Entry("Traction control", Game + "TCLevel", "", 0, 12, 0),
            new Entry("ABS", Game + "ABSLevel", "", 0, 12, 0),
            new Entry("Brake bias", Game + "BrakeBias", "%", 0, 100, 1),
            new Entry("Engine map", Game + "EngineMap", "", 0, 12, 0),
            new Entry("Current lap time", Game + "CurrentLapTime", "", 0, 0, -1),
            new Entry("Last lap time", Game + "LastLapTime", "", 0, 0, -1),
            new Entry("Best lap time", Game + "BestLapTime", "", 0, 0, -1),
            new Entry("Position", Game + "Position", "", 0, 30, 0),
            new Entry("Current lap", Game + "CurrentLap", "", 0, 100, 0),
            new Entry("Water temperature", Game + "WaterTemperature", "\u00b0", 0, 130, 0),
            new Entry("Oil temperature", Game + "OilTemperature", "\u00b0", 0, 150, 0),
            new Entry("Tyre temp FL", Game + "TyreTemperatureFrontLeft", "\u00b0", 0, 120, 0),
            new Entry("Tyre temp FR", Game + "TyreTemperatureFrontRight", "\u00b0", 0, 120, 0),
            new Entry("Tyre temp RL", Game + "TyreTemperatureRearLeft", "\u00b0", 0, 120, 0),
            new Entry("Tyre temp RR", Game + "TyreTemperatureRearRight", "\u00b0", 0, 120, 0),
            new Entry("Tyre pressure FL", Game + "TyrePressureFrontLeft", "", 0, 40, 1),
            new Entry("Tyre pressure FR", Game + "TyrePressureFrontRight", "", 0, 40, 1),
            new Entry("Throttle", Game + "Throttle", "%", 0, 100, 0),
            new Entry("Brake", Game + "Brake", "%", 0, 100, 0),
            new Entry("Clutch", Game + "Clutch", "%", 0, 100, 0),
            new Entry("DRS available", Game + "DRSAvailable", "", 0, 1, 0),
            new Entry("Pit limiter", Game + "PitLimiterOn", "", 0, 1, 0),
            new Entry("Session time left", Game + "SessionTimeLeft", "", 0, 0, -1),
        };
    }
}
