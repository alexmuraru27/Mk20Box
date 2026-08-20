using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Mk20Box.Layout;
using Mk20Control.Protocol.Theme.Building;
using SimHub.Plugins;
using Keys = System.Windows.Forms.Keys;

namespace Mk20Box.Runtime
{
    /// <summary>
    /// The single seam between device callbacks and SimHub. Everything the plugin can
    /// ask the host to do lives here, so the rest of the runtime stays free of
    /// PluginManager calls and keyboard interop.
    /// </summary>
    public sealed class SimHubBridge
    {
        private readonly PluginManager pluginManager;
        private readonly Type pluginType;

        public SimHubBridge(PluginManager pluginManager, Type pluginType)
        {
            this.pluginManager = pluginManager;
            this.pluginType = pluginType;
        }

        /// <summary>Runs a SimHub action by name, for example "GameName.Something".</summary>
        public void TriggerAction(string actionName)
        {
            if (pluginManager == null || string.IsNullOrWhiteSpace(actionName))
            {
                return;
            }

            Guard($"action '{actionName}'", () => pluginManager.TriggerAction(actionName));
        }

        /// <summary>
        /// Makes an input known to SimHub so it shows up in Controls &amp; Events and can
        /// be mapped there. Triggering an input that was never registered does nothing,
        /// so this has to happen before the key is pressed.
        /// </summary>
        public void RegisterInput(string inputName)
        {
            if (pluginManager == null || string.IsNullOrWhiteSpace(inputName))
            {
                return;
            }

            lock (registeredInputs)
            {
                if (!registeredInputs.Add(inputName))
                {
                    return;
                }
            }

            Guard($"input registration '{inputName}'", () =>
                pluginManager.AddInput(inputName, pluginType));
        }

        private readonly HashSet<string> registeredInputs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Presses and releases a SimHub input. Inputs are level triggered, so a press
        /// on its own would leave the input stuck down.
        /// </summary>
        public void TriggerInput(string inputName)
        {
            if (pluginManager == null || string.IsNullOrWhiteSpace(inputName))
            {
                return;
            }

            // Cheap safety net if the layout changed since it was indexed.
            RegisterInput(inputName);

            Guard($"input '{inputName}'", () =>
            {
                pluginManager.TriggerInputPress(inputName, pluginType);
                pluginManager.TriggerInputRelease(inputName, pluginType);
            });
        }

        /// <summary>
        /// Presses and releases a key through SimHub's own keyboard emulator, the same
        /// one its KeyboardEmulator plugin uses, so press and release stay balanced.
        /// </summary>
        public void SendKeystroke(Mk20KeystrokeSettings keystroke)
        {
            HidKey hidKey;
            if (keystroke == null
                || !keystroke.HasKey
                || !Enum.TryParse(keystroke.Key, out hidKey))
            {
                return;
            }

            Key wpfKey;
            if (!Mk20Box.Ui.KeyMapping.TryMapBack(hidKey, out wpfKey))
            {
                return;
            }

            // Forms.Keys values are virtual key codes, so this maps straight across.
            var combination = new List<Keys>();
            if (keystroke.Ctrl) combination.Add(Keys.ControlKey);
            if (keystroke.Shift) combination.Add(Keys.ShiftKey);
            if (keystroke.Alt) combination.Add(Keys.Menu);
            if (keystroke.Win) combination.Add(Keys.LWin);

            combination.Add((Keys)KeyInterop.VirtualKeyFromKey(wpfKey));

            Guard($"keystroke '{keystroke.Key}'", () =>
            {
                // Let anything still in flight land before a new key goes down.
                Pause();
                InputManagerCS.Keyboard.ShortcutKeys(combination.ToArray(), InputDelayMs);
            });
        }

        /// <summary>
        /// Pause after every emitted event. Games and sims often poll the keyboard, so
        /// input sent faster than their poll rate is missed or seen out of order.
        /// Tunable because the right value depends entirely on the target.
        /// </summary>
        public int InputDelayMs { get; set; } = DefaultInputDelayMs;

        public const int DefaultInputDelayMs = 10;

        /// <summary>
        /// Types text as Unicode so the keyboard layout does not matter. SimHub's
        /// emulator only speaks virtual keys, so this is the one thing we send
        /// ourselves.
        /// </summary>
        public void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Guard("text", () =>
            {
                // Let anything still in flight land first, otherwise the opening
                // character can be reordered against the previous step.
                Pause();

                foreach (char character in text)
                {
                    // Down and up go in one call: a character is a single unit, and
                    // splitting it lets other input interleave mid-character.
                    var pair = new[]
                    {
                        UnicodeInput(character, false),
                        UnicodeInput(character, true),
                    };

                    SendInput((uint)pair.Length, pair, Marshal.SizeOf(typeof(INPUT)));
                    Pause();
                }
            });
        }

        /// <summary>A failed step must never take down the caller's macro.</summary>
        private static void Guard(string what, Action body)
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn($"[MK20Box] SimHub {what} failed: {ex.Message}");
            }
        }

        private void Pause()
        {
            int delay = InputDelayMs;
            if (delay > 0)
            {
                System.Threading.Thread.Sleep(delay);
            }
        }

        private static INPUT UnicodeInput(char character, bool up)
        {
            return new INPUT
            {
                Type = INPUT_KEYBOARD,
                Data = new INPUTUNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        VirtualKey = 0,
                        Scan = character,
                        Flags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0u),
                        Time = 0,
                        ExtraInfo = IntPtr.Zero,
                    },
                },
            };
        }

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int Type;
            public INPUTUNION Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public KEYBDINPUT Keyboard;

            [FieldOffset(0)]
            public MOUSEINPUT Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort VirtualKey;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        /// <summary>Present only so the union matches the size Windows expects.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint Data;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }
    }
}
