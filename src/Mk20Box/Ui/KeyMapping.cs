using System.Collections.Generic;
using System.Windows.Input;
using Mk20Control.Protocol.Theme.Building;

namespace Mk20Box.Ui
{
    /// <summary>Maps WPF keys onto the HID codes the device sends.</summary>
    public static class KeyMapping
    {
        private static readonly Dictionary<Key, HidKey> Map = BuildMap();

        /// <summary>True when the key is only a modifier, so recording should keep waiting.</summary>
        public static bool IsModifier(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl
                || key == Key.LeftShift || key == Key.RightShift
                || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LWin || key == Key.RWin
                || key == Key.System;
        }

        public static bool TryMap(Key key, out HidKey hidKey)
        {
            return Map.TryGetValue(key, out hidKey);
        }

        /// <summary>Reverse lookup, used when the plugin replays a keystroke.</summary>
        public static bool TryMapBack(HidKey hidKey, out Key key)
        {
            foreach (KeyValuePair<Key, HidKey> pair in Map)
            {
                if (pair.Value == hidKey)
                {
                    key = pair.Key;
                    return true;
                }
            }

            key = Key.None;
            return false;
        }

        private static Dictionary<Key, HidKey> BuildMap()
        {
            var map = new Dictionary<Key, HidKey>
            {
                // Modifiers as plain keys, so a game bound to bare Shift or Ctrl can be
                // driven. A modifier held while another key is pressed is separate.
                { Key.LeftCtrl, HidKey.LeftCtrl },
                { Key.RightCtrl, HidKey.RightCtrl },
                { Key.LeftShift, HidKey.LeftShift },
                { Key.RightShift, HidKey.RightShift },
                { Key.LeftAlt, HidKey.LeftAlt },
                { Key.RightAlt, HidKey.RightAlt },
                { Key.LWin, HidKey.LeftWin },
                { Key.RWin, HidKey.RightWin },

                { Key.Enter, HidKey.Enter },
                { Key.Escape, HidKey.Escape },
                { Key.Back, HidKey.Backspace },
                { Key.Tab, HidKey.Tab },
                { Key.Space, HidKey.Space },
                { Key.OemMinus, HidKey.Minus },
                { Key.OemPlus, HidKey.Equals },
                { Key.OemOpenBrackets, HidKey.LeftBracket },
                { Key.OemCloseBrackets, HidKey.RightBracket },
                { Key.OemBackslash, HidKey.Backslash },
                { Key.OemSemicolon, HidKey.Semicolon },
                { Key.OemQuotes, HidKey.Apostrophe },
                { Key.OemTilde, HidKey.GraveAccent },
                { Key.OemComma, HidKey.Comma },
                { Key.OemPeriod, HidKey.Period },
                { Key.OemQuestion, HidKey.Slash },
                { Key.CapsLock, HidKey.CapsLock },
                { Key.PrintScreen, HidKey.PrintScreen },
                { Key.Scroll, HidKey.ScrollLock },
                { Key.Pause, HidKey.Pause },
                { Key.Insert, HidKey.Insert },
                { Key.Home, HidKey.Home },
                { Key.PageUp, HidKey.PageUp },
            };

            AddRange(map, Key.A, Key.Z, HidKey.A);
            AddRange(map, Key.F1, Key.F12, HidKey.F1);

            // Digits are not contiguous with the letters, so map them explicitly.
            map[Key.D1] = HidKey.Digit1;
            map[Key.D2] = HidKey.Digit2;
            map[Key.D3] = HidKey.Digit3;
            map[Key.D4] = HidKey.Digit4;
            map[Key.D5] = HidKey.Digit5;
            map[Key.D6] = HidKey.Digit6;
            map[Key.D7] = HidKey.Digit7;
            map[Key.D8] = HidKey.Digit8;
            map[Key.D9] = HidKey.Digit9;
            map[Key.D0] = HidKey.Digit0;

            AddOptional(map, "Delete", Key.Delete);
            AddOptional(map, "End", Key.End);
            AddOptional(map, "PageDown", Key.PageDown);
            AddOptional(map, "Right", Key.Right);
            AddOptional(map, "Left", Key.Left);
            AddOptional(map, "Down", Key.Down);
            AddOptional(map, "Up", Key.Up);

            return map;
        }

        private static void AddRange(Dictionary<Key, HidKey> map, Key first, Key last, HidKey firstHid)
        {
            for (int offset = 0; offset <= last - first; offset++)
            {
                map[first + offset] = firstHid + offset;
            }
        }

        /// <summary>Adds a key only if the library defines a matching HID name.</summary>
        private static void AddOptional(Dictionary<Key, HidKey> map, string hidName, Key key)
        {
            HidKey parsed;
            if (System.Enum.TryParse(hidName, out parsed))
            {
                map[key] = parsed;
            }
        }
    }
}
