using System.Linq;
using Mk20Box.Layout;
using NUnit.Framework;

namespace Mk20Box.Tests.Layout
{
    /// <summary>
    /// Covers the pieces a macro is built from. Cloning matters most: a copied key
    /// must not share steps with the key it came from.
    /// </summary>
    [TestFixture]
    public class MacroSettingsTests
    {
        [Test]
        public void Keystroke_HasKey_FollowsWhetherAKeyIsSet()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new Mk20KeystrokeSettings().HasKey, Is.False);
                Assert.That(new Mk20KeystrokeSettings { Key = "A" }.HasKey, Is.True);
            });
        }

        [Test]
        public void Keystroke_ToString_ListsModifiersBeforeTheKey()
        {
            var keystroke = new Mk20KeystrokeSettings
            {
                Ctrl = true,
                Shift = true,
                Key = "C",
            };

            Assert.That(keystroke.ToString(), Is.EqualTo("Ctrl + Shift + C"));
        }

        [Test]
        public void Keystroke_Clone_CopiesEveryField()
        {
            var source = new Mk20KeystrokeSettings
            {
                Ctrl = true,
                Shift = true,
                Alt = true,
                Win = true,
                Key = "F1",
            };

            Mk20KeystrokeSettings copy = source.Clone();

            Assert.Multiple(() =>
            {
                Assert.That(copy.Ctrl, Is.True);
                Assert.That(copy.Shift, Is.True);
                Assert.That(copy.Alt, Is.True);
                Assert.That(copy.Win, Is.True);
                Assert.That(copy.Key, Is.EqualTo("F1"));
            });
        }

        [Test]
        public void Keystroke_Clone_SharesNothingWithTheOriginal()
        {
            var source = new Mk20KeystrokeSettings { Key = "A" };

            Mk20KeystrokeSettings copy = source.Clone();
            copy.Key = "B";
            copy.Ctrl = true;

            Assert.Multiple(() =>
            {
                Assert.That(source.Key, Is.EqualTo("A"));
                Assert.That(source.Ctrl, Is.False);
            });
        }

        [Test]
        public void Step_Clone_CopiesEveryField()
        {
            var source = new Mk20MacroStepSettings
            {
                Kind = MacroStepKinds.SimHubAction,
                Text = "typed",
                DelayMs = 250,
                ActionName = "DoThing",
                Keystroke = new Mk20KeystrokeSettings { Alt = true, Key = "X" },
            };

            Mk20MacroStepSettings copy = source.Clone();

            Assert.Multiple(() =>
            {
                Assert.That(copy.Kind, Is.EqualTo(MacroStepKinds.SimHubAction));
                Assert.That(copy.Text, Is.EqualTo("typed"));
                Assert.That(copy.DelayMs, Is.EqualTo(250));
                Assert.That(copy.ActionName, Is.EqualTo("DoThing"));
                Assert.That(copy.Keystroke.Alt, Is.True);
                Assert.That(copy.Keystroke.Key, Is.EqualTo("X"));
            });
        }

        [Test]
        public void Step_Clone_DeepCopiesTheKeystroke()
        {
            var source = new Mk20MacroStepSettings
            {
                Keystroke = new Mk20KeystrokeSettings { Key = "A" },
            };

            Mk20MacroStepSettings copy = source.Clone();
            copy.Keystroke.Key = "B";

            Assert.That(source.Keystroke.Key, Is.EqualTo("A"));
        }

        [Test]
        public void Step_Clone_SurvivesAMissingKeystroke()
        {
            var source = new Mk20MacroStepSettings { Keystroke = null };

            Mk20MacroStepSettings copy = source.Clone();

            Assert.That(copy.Keystroke, Is.Not.Null);
        }

        [Test]
        public void Step_Describe_MentionsWhatTheStepDoes()
        {
            var text = new Mk20MacroStepSettings { Kind = MacroStepKinds.Text, Text = "hello" };
            var delay = new Mk20MacroStepSettings { Kind = MacroStepKinds.Delay, DelayMs = 500 };
            var action = new Mk20MacroStepSettings
            {
                Kind = MacroStepKinds.SimHubAction,
                ActionName = "DoThing",
            };

            Assert.Multiple(() =>
            {
                Assert.That(text.Describe(), Does.Contain("hello"));
                Assert.That(delay.Describe(), Does.Contain("500"));
                Assert.That(action.Describe(), Does.Contain("DoThing"));
            });
        }

        [Test]
        public void StepKinds_AreAllDistinct()
        {
            Assert.That(MacroStepKinds.All.Distinct().Count(), Is.EqualTo(MacroStepKinds.All.Length));
        }
    }

    /// <summary>
    /// Covers encoder configuration. Only some modes can tell rotation direction
    /// apart, and the editor relies on that to steer the user.
    /// </summary>
    [TestFixture]
    public class EncoderSettingsTests
    {
        [TestCase(EncoderModes.BuiltInFunction)]
        [TestCase(EncoderModes.Keystrokes)]
        public void DirectionAwareModes_RunOnTheDevice(string mode)
        {
            Assert.That(EncoderModes.IsDirectionAware(mode), Is.True);
        }

        [TestCase(EncoderModes.ReportToPlugin)]
        [TestCase(EncoderModes.Unassigned)]
        public void OtherModes_CannotTellDirectionApart(string mode)
        {
            Assert.That(EncoderModes.IsDirectionAware(mode), Is.False);
        }

        [Test]
        public void NewEncoder_StartsUnassigned()
        {
            var encoder = new Mk20EncoderSettings();

            Assert.Multiple(() =>
            {
                Assert.That(encoder.Mode, Is.EqualTo(EncoderModes.Unassigned));
                Assert.That(encoder.Describe(), Is.EqualTo("Unassigned"));
                Assert.That(encoder.RotateLeft, Is.Not.Null);
                Assert.That(encoder.Click, Is.Not.Null);
                Assert.That(encoder.RotateRight, Is.Not.Null);
            });
        }

        [Test]
        public void Describe_NamesTheBuiltInFunction()
        {
            var encoder = new Mk20EncoderSettings
            {
                Mode = EncoderModes.BuiltInFunction,
                Function = EncoderFunctions.DeviceBrightness,
            };

            Assert.That(encoder.Describe(), Is.EqualTo(EncoderFunctions.DeviceBrightness));
        }

        [Test]
        public void Describe_ListsAllThreeKeystrokes()
        {
            var encoder = new Mk20EncoderSettings
            {
                Mode = EncoderModes.Keystrokes,
                RotateLeft = new Mk20KeystrokeSettings { Key = "Left" },
                Click = new Mk20KeystrokeSettings { Key = "Enter" },
                RotateRight = new Mk20KeystrokeSettings { Key = "Right" },
            };

            Assert.That(encoder.Describe(), Is.EqualTo("Left / Enter / Right"));
        }

        [Test]
        public void Describe_WarnsThatReportingLosesDirection()
        {
            var encoder = new Mk20EncoderSettings
            {
                Mode = EncoderModes.ReportToPlugin,
                CommandId = "mk20.enc",
            };

            Assert.Multiple(() =>
            {
                Assert.That(encoder.Describe(), Does.Contain("mk20.enc"));
                Assert.That(encoder.Describe(), Does.Contain("no direction"));
            });
        }

        [Test]
        public void Modes_AreAllOffered()
        {
            Assert.Multiple(() =>
            {
                Assert.That(EncoderModes.All, Does.Contain(EncoderModes.Unassigned));
                Assert.That(EncoderModes.All.Distinct().Count(), Is.EqualTo(EncoderModes.All.Length));
                Assert.That(EncoderFunctions.All.Distinct().Count(), Is.EqualTo(EncoderFunctions.All.Length));
            });
        }

        [Test]
        public void Functions_OfferOnlyTheTwoThatAreUseful()
        {
            Assert.That(EncoderFunctions.All,
                Is.EqualTo(new[] { EncoderFunctions.SystemVolume, EncoderFunctions.DeviceBrightness }));
        }
    }
}
