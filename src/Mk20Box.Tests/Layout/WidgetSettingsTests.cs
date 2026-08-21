using Mk20Box.Layout;
using NUnit.Framework;

namespace Mk20Box.Tests.Layout
{
    /// <summary>
    /// Covers the widget model: picking the type for a kind, and what survives when
    /// the user changes a widget's kind after configuring it.
    /// </summary>
    [TestFixture]
    public class WidgetSettingsTests
    {
        [TestCase(WidgetKinds.Text, typeof(Mk20TextWidget))]
        [TestCase(WidgetKinds.OutlineText, typeof(Mk20OutlineTextWidget))]
        [TestCase(WidgetKinds.ProgressBar, typeof(Mk20ProgressBarWidget))]
        [TestCase(WidgetKinds.Clock, typeof(Mk20ClockWidget))]
        public void Create_BuildsTheTypeForTheKind(string kind, System.Type expected)
        {
            Mk20WidgetSettings widget = Mk20WidgetSettings.Create(kind);

            Assert.Multiple(() =>
            {
                Assert.That(widget, Is.InstanceOf(expected));
                Assert.That(widget.Kind, Is.EqualTo(kind));
            });
        }

        [Test]
        public void Create_FallsBackToTextForAnUnknownKind()
        {
            Assert.That(Mk20WidgetSettings.Create("nonsense"), Is.InstanceOf<Mk20TextWidget>());
        }

        [Test]
        public void CopyCommonTo_CarriesTheSharedSettingsOver()
        {
            var source = new Mk20TextWidget
            {
                Label = "Speed",
                Property = "GameData.SpeedKmh",
                Decimals = 1,
                X = 40,
                Y = 60,
                Color = "#00ff00",
                Channel = "chan-1",
            };

            var target = new Mk20ProgressBarWidget();
            source.CopyCommonTo(target);

            Assert.Multiple(() =>
            {
                Assert.That(target.Label, Is.EqualTo("Speed"));
                Assert.That(target.Property, Is.EqualTo("GameData.SpeedKmh"));
                Assert.That(target.Decimals, Is.EqualTo(1));
                Assert.That(target.X, Is.EqualTo(40));
                Assert.That(target.Y, Is.EqualTo(60));
                Assert.That(target.Color, Is.EqualTo("#00ff00"));
                Assert.That(target.Channel, Is.EqualTo("chan-1"),
                    "the channel must survive, or the device stops receiving the value");
            });
        }

        [Test]
        public void CopyCommonTo_CarriesTextSettingsBetweenTextWidgets()
        {
            var source = new Mk20TextWidget { Text = "LAP", Unit = "km/h", FontSize = 28 };
            var target = new Mk20OutlineTextWidget();

            source.CopyCommonTo(target);

            Assert.Multiple(() =>
            {
                Assert.That(target.Text, Is.EqualTo("LAP"));
                Assert.That(target.Unit, Is.EqualTo("km/h"));
                Assert.That(target.FontSize, Is.EqualTo(28));
            });
        }

        [Test]
        public void IsBound_FollowsWhetherAPropertyIsSet()
        {
            var widget = new Mk20TextWidget();

            Assert.That(widget.IsBound, Is.False);

            widget.Property = "GameData.SpeedKmh";

            Assert.That(widget.IsBound, Is.True);
        }

        [TestCase("   ")]
        [TestCase("")]
        [TestCase(null)]
        public void IsBound_TreatsBlankAsUnbound(string property)
        {
            Assert.That(new Mk20TextWidget { Property = property }.IsBound, Is.False);
        }

        [TestCase("DataCorePlugin.GameData.SpeedKmh", "SpeedKmh")]
        [TestCase("SpeedKmh", "SpeedKmh")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void ShortProperty_TrimsThePluginPrefix(string input, string expected)
        {
            Assert.That(Mk20WidgetSettings.ShortProperty(input), Is.EqualTo(expected));
        }

        [Test]
        public void Describe_PrefersTheLabel()
        {
            var widget = new Mk20TextWidget { Label = "Speed", Property = "GameData.SpeedKmh" };

            Assert.That(widget.Describe(), Does.Contain("Speed"));
        }

        [Test]
        public void Describe_FallsBackToThePropertyLeaf()
        {
            var widget = new Mk20TextWidget { Property = "DataCorePlugin.GameData.SpeedKmh" };

            Assert.That(widget.Describe(), Does.Contain("SpeedKmh"));
        }

        [Test]
        public void Describe_NamesTheKindWhenThereIsNothingElse()
        {
            // A text widget starts with placeholder text, so the bare case is a kind
            // that carries none.
            Assert.That(new Mk20ProgressBarWidget().Describe(), Is.EqualTo(WidgetKinds.ProgressBar));
        }

        [Test]
        public void ProgressBar_SendsABareNumber()
        {
            // A suffix would stop the device parsing the value as a number.
            Assert.That(new Mk20ProgressBarWidget().ValueSuffix, Is.Null);
        }

        [Test]
        public void Clock_CountsItsFields()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new Mk20ClockWidget { ShowSeconds = false }.FieldCount, Is.EqualTo(2));
                Assert.That(new Mk20ClockWidget { ShowSeconds = true }.FieldCount, Is.EqualTo(3));
            });
        }

        [Test]
        public void IsKnown_AcceptsEveryOfferedKind()
        {
            Assert.That(WidgetKinds.All, Has.All.Matches<string>(WidgetKinds.IsKnown));
        }

        [Test]
        public void IsKnown_RejectsAnythingElse()
        {
            Assert.That(WidgetKinds.IsKnown("nonsense"), Is.False);
        }
    }
}
