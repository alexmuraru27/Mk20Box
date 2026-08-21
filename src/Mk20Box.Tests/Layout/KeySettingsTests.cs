using System.Linq;
using Mk20Box.Layout;
using NUnit.Framework;

namespace Mk20Box.Tests.Layout
{
    /// <summary>
    /// Covers copy, paste and reset of a single key. The command id rule is the
    /// important one: the router indexes keys by it, so a duplicate would make one
    /// key answer for another's presses.
    /// </summary>
    [TestFixture]
    public class KeySettingsTests
    {
        private static Mk20KeySettings ConfiguredKey()
        {
            var key = new Mk20KeySettings
            {
                Row = 1,
                Column = 2,
                Title = "PIT",
                TitleFontSize = 24,
                TitleColor = "#ff0000",
                TitlePosition = "top",
                MediaPath = @"C:\icons\pit.png",
                PreserveAlpha = false,
                IconFit = false,
                IconOffsetX = 0.25,
                IconOffsetY = -0.5,
                ActionType = KeyActionKinds.Macro,
                ActionTarget = "SomeAction",
                TargetPageId = "page-1",
                CommandId = "mk20.original",
            };

            key.Keystroke = new Mk20KeystrokeSettings { Ctrl = true, Key = "P" };
            key.MacroSteps.Add(new Mk20MacroStepSettings
            {
                Kind = MacroStepKinds.Text,
                Text = "hello",
            });

            return key;
        }

        [Test]
        public void ApplyFrom_CopiesAppearance()
        {
            Mk20KeySettings source = ConfiguredKey();
            var target = new Mk20KeySettings();

            target.ApplyFrom(source);

            Assert.Multiple(() =>
            {
                Assert.That(target.Title, Is.EqualTo("PIT"));
                Assert.That(target.TitleFontSize, Is.EqualTo(24));
                Assert.That(target.TitleColor, Is.EqualTo("#ff0000"));
                Assert.That(target.TitlePosition, Is.EqualTo("top"));
                Assert.That(target.MediaPath, Is.EqualTo(@"C:\icons\pit.png"));
                Assert.That(target.PreserveAlpha, Is.False);
                Assert.That(target.IconFit, Is.False);
                Assert.That(target.IconOffsetX, Is.EqualTo(0.25));
                Assert.That(target.IconOffsetY, Is.EqualTo(-0.5));
            });
        }

        [Test]
        public void ApplyFrom_CopiesAction()
        {
            Mk20KeySettings source = ConfiguredKey();
            var target = new Mk20KeySettings();

            target.ApplyFrom(source);

            Assert.Multiple(() =>
            {
                Assert.That(target.ActionType, Is.EqualTo(KeyActionKinds.Macro));
                Assert.That(target.ActionTarget, Is.EqualTo("SomeAction"));
                Assert.That(target.Keystroke.Ctrl, Is.True);
                Assert.That(target.Keystroke.Key, Is.EqualTo("P"));
                Assert.That(target.MacroSteps, Has.Count.EqualTo(1));
                Assert.That(target.MacroSteps[0].Text, Is.EqualTo("hello"));
            });
        }

        [Test]
        public void ApplyFrom_KeepsTheTargetsOwnCell()
        {
            Mk20KeySettings source = ConfiguredKey();
            var target = new Mk20KeySettings { Row = 3, Column = 4 };

            target.ApplyFrom(source);

            Assert.Multiple(() =>
            {
                Assert.That(target.Row, Is.EqualTo(3));
                Assert.That(target.Column, Is.EqualTo(4));
            });
        }

        [Test]
        public void ApplyFrom_NeverReusesTheSourceCommandId()
        {
            Mk20KeySettings source = ConfiguredKey();
            var target = new Mk20KeySettings();

            target.ApplyFrom(source);

            Assert.Multiple(() =>
            {
                Assert.That(target.CommandId, Is.Not.Null.And.Not.Empty);
                Assert.That(target.CommandId, Is.Not.EqualTo(source.CommandId));
                Assert.That(source.CommandId, Is.EqualTo("mk20.original"), "the source must be left alone");
            });
        }

        [Test]
        public void ApplyFrom_GivesEveryPasteADistinctCommandId()
        {
            Mk20KeySettings source = ConfiguredKey();

            string[] ids = Enumerable.Range(0, 25)
                .Select(_ =>
                {
                    var target = new Mk20KeySettings();
                    target.ApplyFrom(source);
                    return target.CommandId;
                })
                .ToArray();

            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        }

        [Test]
        public void ApplyFrom_DoesNotInheritTheFolder()
        {
            // A folder belongs to the one key that opens it; the copy gets its own.
            Mk20KeySettings source = ConfiguredKey();
            var target = new Mk20KeySettings();

            target.ApplyFrom(source);

            Assert.That(target.TargetPageId, Is.Null);
        }

        [Test]
        public void ApplyFrom_DeepCopiesTheKeystroke()
        {
            Mk20KeySettings source = ConfiguredKey();
            var target = new Mk20KeySettings();
            target.ApplyFrom(source);

            target.Keystroke.Key = "Q";

            Assert.That(source.Keystroke.Key, Is.EqualTo("P"));
        }

        [Test]
        public void ApplyFrom_DeepCopiesMacroSteps()
        {
            Mk20KeySettings source = ConfiguredKey();
            var target = new Mk20KeySettings();
            target.ApplyFrom(source);

            target.MacroSteps[0].Text = "changed";
            target.MacroSteps.Add(new Mk20MacroStepSettings());

            Assert.Multiple(() =>
            {
                Assert.That(source.MacroSteps, Has.Count.EqualTo(1));
                Assert.That(source.MacroSteps[0].Text, Is.EqualTo("hello"));
            });
        }

        [Test]
        public void ApplyFrom_IgnoresNull()
        {
            var target = new Mk20KeySettings { Title = "KEEP" };

            target.ApplyFrom(null);

            Assert.That(target.Title, Is.EqualTo("KEEP"));
        }

        [Test]
        public void Reset_ClearsEverythingButThePosition()
        {
            Mk20KeySettings key = ConfiguredKey();

            key.Reset();

            Assert.Multiple(() =>
            {
                Assert.That(key.Row, Is.EqualTo(1), "the cell is where the key is, not what it does");
                Assert.That(key.Column, Is.EqualTo(2));
                Assert.That(key.Title, Is.Null);
                Assert.That(key.MediaPath, Is.Null);
                Assert.That(key.ActionType, Is.EqualTo(KeyActionKinds.Unassigned));
                Assert.That(key.ActionTarget, Is.Null);
                Assert.That(key.TargetPageId, Is.Null);
                Assert.That(key.CommandId, Is.Null);
                Assert.That(key.MacroSteps, Is.Empty);
                Assert.That(key.Keystroke.HasKey, Is.False);
            });
        }

        [Test]
        public void Reset_RestoresTitleDefaults()
        {
            Mk20KeySettings key = ConfiguredKey();

            key.Reset();

            Assert.Multiple(() =>
            {
                Assert.That(key.TitleFontSize, Is.EqualTo(KeyTitleDefaults.FontSize));
                Assert.That(key.TitleColor, Is.EqualTo(KeyTitleDefaults.Color));
                Assert.That(key.TitlePosition, Is.EqualTo(KeyTitleDefaults.Position));
                Assert.That(key.PreserveAlpha, Is.True);
                Assert.That(key.IconFit, Is.True);
                Assert.That(key.IconOffsetX, Is.EqualTo(0));
                Assert.That(key.IconOffsetY, Is.EqualTo(0));
            });
        }

        [Test]
        public void Snapshot_IsDetachedFromTheKeyItCameFrom()
        {
            Mk20KeySettings source = ConfiguredKey();

            Mk20KeySettings clipboard = source.Snapshot();
            source.Title = "EDITED AFTER COPY";
            source.MacroSteps.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(clipboard.Title, Is.EqualTo("PIT"));
                Assert.That(clipboard.MacroSteps, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void Snapshot_KeepsTheCellItWasTakenFrom()
        {
            Mk20KeySettings source = ConfiguredKey();

            Mk20KeySettings clipboard = source.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(clipboard.Row, Is.EqualTo(1));
                Assert.That(clipboard.Column, Is.EqualTo(2));
            });
        }
    }
}
