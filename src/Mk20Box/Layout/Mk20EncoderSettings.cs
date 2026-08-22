using System.Collections.Generic;

namespace Mk20Box.Layout
{
    /// <summary>
    /// How an encoder is bound. The device treats these very differently, and the
    /// difference decides whether rotation direction is usable at all:
    ///
    /// * <see cref="BuiltInFunction"/> and <see cref="Keystrokes"/> run on the device
    ///   and are direction-aware.
    /// * <see cref="ReportToPlugin"/> reports a single event for clockwise,
    ///   counter-clockwise and click alike, so direction cannot be told apart
    ///   (PROTOCOL_WAVESHARE_MK20.md open item #17).
    /// </summary>
    public static class EncoderModes
    {
        public const string Unassigned = "Unassigned";
        public const string BuiltInFunction = "Device function";
        public const string Keystrokes = "Keystrokes";
        public const string ReportToPlugin = "Report to plugin";

        public static readonly string[] All =
        {
            Unassigned,
            BuiltInFunction,
            Keystrokes,
            ReportToPlugin,
        };

        /// <summary>Only these modes can tell clockwise from counter-clockwise.</summary>
        public static bool IsDirectionAware(string mode)
        {
            return mode == BuiltInFunction || mode == Keystrokes;
        }
    }

    /// <summary>Built-in encoder functions the device performs on its own.</summary>
    public static class EncoderFunctions
    {
        public const string SystemVolume = "System volume";
        public const string DeviceBrightness = "Device brightness";

        public static readonly string[] All =
        {
            SystemVolume,
            DeviceBrightness,
        };
    }

    /// <summary>One rotary encoder on a page. Encoders are configured per page.</summary>
    public sealed class Mk20EncoderSettings
    {
        public string Mode { get; set; } = EncoderModes.Unassigned;

        /// <summary>Which built-in function, when <see cref="Mode"/> is a device function.</summary>
        public string Function { get; set; } = EncoderFunctions.SystemVolume;

        public Mk20KeystrokeSettings RotateLeft { get; set; } = new Mk20KeystrokeSettings();

        public Mk20KeystrokeSettings Click { get; set; } = new Mk20KeystrokeSettings();

        public Mk20KeystrokeSettings RotateRight { get; set; } = new Mk20KeystrokeSettings();

        /// <summary>Command id reported when <see cref="Mode"/> is report-to-plugin.</summary>
        public string CommandId { get; set; }

        public string Describe()
        {
            switch (Mode)
            {
                case EncoderModes.BuiltInFunction:
                    return Function;

                case EncoderModes.Keystrokes:
                    return string.Join(" / ", new[]
                    {
                        RotateLeft?.ToString(),
                        Click?.ToString(),
                        RotateRight?.ToString(),
                    });

                case EncoderModes.ReportToPlugin:
                    return "Reports \"" + (CommandId ?? "") + "\" (no direction)";

                default:
                    return "Unassigned";
            }
        }
    }
}
