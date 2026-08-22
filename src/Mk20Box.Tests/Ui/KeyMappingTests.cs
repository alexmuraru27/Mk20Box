using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Mk20Box.Ui;
using Mk20Control.Protocol.Theme.Building;
using NUnit.Framework;

namespace Mk20Box.Tests.Ui
{
    /// <summary>
    /// Covers the WPF-to-HID key map. The device speaks standard HID usage codes, so
    /// anything it can send should be recordable in the editor; anything missing here
    /// shows up as "Unsupported key" and cannot be bound at all.
    /// </summary>
    [TestFixture]
    public class KeyMappingTests
    {
        private static HidKey Map(Key key)
        {
            HidKey hidKey;
            Assert.That(KeyMapping.TryMap(key, out hidKey), Is.True, $"{key} should be recordable");
            return hidKey;
        }

        [TestCase(Key.NumPad0, HidKey.Keypad0)]
        [TestCase(Key.NumPad1, HidKey.Keypad1)]
        [TestCase(Key.NumPad2, HidKey.Keypad2)]
        [TestCase(Key.NumPad3, HidKey.Keypad3)]
        [TestCase(Key.NumPad4, HidKey.Keypad4)]
        [TestCase(Key.NumPad5, HidKey.Keypad5)]
        [TestCase(Key.NumPad6, HidKey.Keypad6)]
        [TestCase(Key.NumPad7, HidKey.Keypad7)]
        [TestCase(Key.NumPad8, HidKey.Keypad8)]
        [TestCase(Key.NumPad9, HidKey.Keypad9)]
        public void NumpadDigits_AreRecordable(Key key, HidKey expected)
        {
            Assert.That(Map(key), Is.EqualTo(expected));
        }

        [TestCase(Key.Divide, HidKey.KeypadDivide)]
        [TestCase(Key.Multiply, HidKey.KeypadMultiply)]
        [TestCase(Key.Subtract, HidKey.KeypadMinus)]
        [TestCase(Key.Add, HidKey.KeypadPlus)]
        [TestCase(Key.Decimal, HidKey.KeypadPeriod)]
        [TestCase(Key.NumLock, HidKey.NumLock)]
        public void NumpadOperators_AreRecordable(Key key, HidKey expected)
        {
            Assert.That(Map(key), Is.EqualTo(expected));
        }

        [Test]
        public void NumpadDigits_AreDistinctFromTheDigitRow()
        {
            // A game binds Numpad1 and 1 separately, so conflating them would fire the
            // wrong control.
            var pairs = new[]
            {
                new { Pad = Key.NumPad0, Row = Key.D0 },
                new { Pad = Key.NumPad1, Row = Key.D1 },
                new { Pad = Key.NumPad5, Row = Key.D5 },
                new { Pad = Key.NumPad9, Row = Key.D9 },
            };

            Assert.Multiple(() =>
            {
                foreach (var pair in pairs)
                {
                    Assert.That(Map(pair.Pad), Is.Not.EqualTo(Map(pair.Row)),
                        $"{pair.Pad} must not send the same code as {pair.Row}");
                }
            });
        }

        [TestCase(Key.Up, HidKey.UpArrow)]
        [TestCase(Key.Down, HidKey.DownArrow)]
        [TestCase(Key.Left, HidKey.LeftArrow)]
        [TestCase(Key.Right, HidKey.RightArrow)]
        public void ArrowKeys_AreRecordable(Key key, HidKey expected)
        {
            Assert.That(Map(key), Is.EqualTo(expected));
        }

        [TestCase(Key.Delete, HidKey.Delete)]
        [TestCase(Key.End, HidKey.End)]
        [TestCase(Key.PageDown, HidKey.PageDown)]
        [TestCase(Key.PageUp, HidKey.PageUp)]
        [TestCase(Key.Home, HidKey.Home)]
        [TestCase(Key.Insert, HidKey.Insert)]
        public void NavigationCluster_IsRecordable(Key key, HidKey expected)
        {
            Assert.That(Map(key), Is.EqualTo(expected));
        }

        [TestCase(Key.A, HidKey.A)]
        [TestCase(Key.Z, HidKey.Z)]
        [TestCase(Key.D0, HidKey.Digit0)]
        [TestCase(Key.D9, HidKey.Digit9)]
        [TestCase(Key.F1, HidKey.F1)]
        [TestCase(Key.F12, HidKey.F12)]
        [TestCase(Key.Space, HidKey.Space)]
        [TestCase(Key.Enter, HidKey.Enter)]
        [TestCase(Key.Escape, HidKey.Escape)]
        [TestCase(Key.Tab, HidKey.Tab)]
        public void CommonKeys_AreRecordable(Key key, HidKey expected)
        {
            Assert.That(Map(key), Is.EqualTo(expected));
        }

        [TestCase(Key.LeftCtrl, HidKey.LeftCtrl)]
        [TestCase(Key.RightShift, HidKey.RightShift)]
        [TestCase(Key.LeftAlt, HidKey.LeftAlt)]
        [TestCase(Key.LWin, HidKey.LeftWin)]
        public void ModifiersAlone_AreRecordable(Key key, HidKey expected)
        {
            // Some games bind a bare Shift or Ctrl, so these must map as keys too.
            Assert.That(Map(key), Is.EqualTo(expected));
        }

        [Test]
        public void EveryKeyTheDeviceSupports_CanBeRecorded()
        {
            // The whole point of the map: if the hardware can send it, the editor must
            // be able to capture it.
            var mapped = new HashSet<HidKey>();
            foreach (Key key in Enum.GetValues(typeof(Key)).Cast<Key>())
            {
                HidKey hidKey;
                if (KeyMapping.TryMap(key, out hidKey))
                {
                    mapped.Add(hidKey);
                }
            }

            // Windows reports the keypad Enter as Key.Return, exactly like the main
            // one, so the recorder separates them by the extended-key flag rather than
            // the map. It is the one code that cannot come from a WPF key alone.
            mapped.Add(HidKey.KeypadEnter);

            HidKey[] unreachable = Enum.GetValues(typeof(HidKey))
                .Cast<HidKey>()
                .Where(hidKey => !mapped.Contains(hidKey))
                .ToArray();

            Assert.That(unreachable, Is.Empty,
                "these HID keys cannot be recorded: " + string.Join(", ", unreachable));
        }

        [Test]
        public void NoTwoKeysShareAHidCode()
        {
            // Two distinct keys sending one code would make the recorder ambiguous.
            // Enum.GetValues repeats aliases such as Key.Enter and Key.Return, which
            // are the same value, so the keys are de-duplicated first.
            var byCode = new Dictionary<HidKey, HashSet<Key>>();
            foreach (Key key in Enum.GetValues(typeof(Key)).Cast<Key>().Distinct())
            {
                HidKey hidKey;
                if (!KeyMapping.TryMap(key, out hidKey))
                {
                    continue;
                }

                if (!byCode.ContainsKey(hidKey))
                {
                    byCode[hidKey] = new HashSet<Key>();
                }

                byCode[hidKey].Add(key);
            }

            // Backslash is deliberate: the same physical key is Oem5 on some layouts
            // and OemBackslash on others, and both should record.
            string[] clashes = byCode
                .Where(pair => pair.Value.Count > 1 && pair.Key != HidKey.Backslash)
                .Select(pair => pair.Key + " <- " + string.Join(", ", pair.Value))
                .ToArray();

            Assert.That(clashes, Is.Empty, string.Join(" | ", clashes));
        }

        [Test]
        public void MapBack_ReversesEveryMapping()
        {
            // Macros replay through the reverse lookup, so a key that records but does
            // not map back would bind fine and then do nothing.
            var failures = new List<string>();

            foreach (Key key in Enum.GetValues(typeof(Key)).Cast<Key>())
            {
                HidKey hidKey;
                if (!KeyMapping.TryMap(key, out hidKey))
                {
                    continue;
                }

                Key back;
                if (!KeyMapping.TryMapBack(hidKey, out back))
                {
                    failures.Add(key + " -> " + hidKey + " -> nothing");
                }
            }

            Assert.That(failures, Is.Empty, string.Join(", ", failures));
        }

        [Test]
        public void MapBack_ReturnsTheNumpadKeyNotTheDigitRow()
        {
            Key back;
            KeyMapping.TryMapBack(HidKey.Keypad7, out back);

            Assert.That(back, Is.EqualTo(Key.NumPad7));
        }

        [TestCase(Key.LeftCtrl)]
        [TestCase(Key.RightCtrl)]
        [TestCase(Key.LeftShift)]
        [TestCase(Key.RightShift)]
        [TestCase(Key.LeftAlt)]
        [TestCase(Key.RightAlt)]
        [TestCase(Key.LWin)]
        [TestCase(Key.RWin)]
        [TestCase(Key.System)]
        public void IsModifier_SpotsModifiers(Key key)
        {
            Assert.That(KeyMapping.IsModifier(key), Is.True);
        }

        [TestCase(Key.A)]
        [TestCase(Key.NumPad1)]
        [TestCase(Key.F5)]
        [TestCase(Key.Space)]
        public void IsModifier_LeavesOrdinaryKeysAlone(Key key)
        {
            Assert.That(KeyMapping.IsModifier(key), Is.False);
        }

        [Test]
        public void TryMap_RejectsAKeyTheDeviceCannotSend()
        {
            HidKey hidKey;

            Assert.That(KeyMapping.TryMap(Key.None, out hidKey), Is.False);
        }
    }
}
