using System.Collections.Generic;
using System.Text;

namespace Mk20Box.Layout
{
    /// <summary>
    /// A keystroke the device sends itself. The MK20 supports one key plus modifiers
    /// (a real Ctrl+Alt+Del was captured from the vendor app), so anything longer has
    /// to be a macro the plugin runs.
    /// </summary>
    public sealed class Mk20KeystrokeSettings
    {
        public bool Ctrl { get; set; }

        public bool Shift { get; set; }

        public bool Alt { get; set; }

        public bool Win { get; set; }

        /// <summary>Name of a <c>HidKey</c> value, e.g. "C" or "F1".</summary>
        public string Key { get; set; }

        public bool HasKey => !string.IsNullOrEmpty(Key);

        /// <summary>Human form, e.g. "Ctrl + Shift + C".</summary>
        public override string ToString()
        {
            if (!HasKey)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Shift) parts.Add("Shift");
            if (Alt) parts.Add("Alt");
            if (Win) parts.Add("Win");
            parts.Add(Key);

            return string.Join(" + ", parts);
        }

        public Mk20KeystrokeSettings Clone()
        {
            return new Mk20KeystrokeSettings
            {
                Ctrl = Ctrl,
                Shift = Shift,
                Alt = Alt,
                Win = Win,
                Key = Key,
            };
        }
    }

    /// <summary>What a single macro step does.</summary>
    public static class MacroStepKinds
    {
        public const string Keystroke = "Keystroke";
        public const string Text = "Type text";
        public const string Delay = "Wait";
        public const string SimHubAction = "SimHub action";

        public static readonly string[] All = { Keystroke, Text, Delay, SimHubAction };
    }

    /// <summary>
    /// One step of a macro. Macros are run by the plugin, not the device: the key
    /// carries a command id, the device reports the press, and the plugin replays
    /// these steps.
    /// </summary>
    public sealed class Mk20MacroStepSettings
    {
        public string Kind { get; set; } = MacroStepKinds.Keystroke;

        public Mk20KeystrokeSettings Keystroke { get; set; } = new Mk20KeystrokeSettings();

        /// <summary>Text to type for <see cref="MacroStepKinds.Text"/>.</summary>
        public string Text { get; set; }

        /// <summary>Pause in milliseconds for <see cref="MacroStepKinds.Delay"/>.</summary>
        public int DelayMs { get; set; } = 100;

        /// <summary>SimHub action name for <see cref="MacroStepKinds.SimHubAction"/>.</summary>
        public string ActionName { get; set; }

        /// <summary>Short description shown in the step list.</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case MacroStepKinds.Keystroke:
                    return Keystroke != null && Keystroke.HasKey
                        ? "Press " + Keystroke
                        : "Press (not set)";

                case MacroStepKinds.Text:
                    return string.IsNullOrEmpty(Text) ? "Type (empty)" : "Type \"" + Text + "\"";

                case MacroStepKinds.Delay:
                    return "Wait " + DelayMs.ToString() + " ms";

                case MacroStepKinds.SimHubAction:
                    return string.IsNullOrEmpty(ActionName)
                        ? "SimHub action (not set)"
                        : "SimHub: " + ActionName;

                default:
                    return Kind;
            }
        }
    }
}
