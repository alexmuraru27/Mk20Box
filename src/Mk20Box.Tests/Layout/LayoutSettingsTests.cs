using System.Linq;
using Mk20Box.Layout;
using NUnit.Framework;

namespace Mk20Box.Tests.Layout
{
    /// <summary>
    /// Covers the shape a page is created with. The device addresses keys by cell,
    /// so a page must always hand over the full grid rather than create cells lazily.
    /// </summary>
    [TestFixture]
    public class LayoutSettingsTests
    {
        [Test]
        public void CreateEmptyPage_FillsTheWholeGrid()
        {
            Mk20PageSettings page = Mk20LayoutSettings.CreateEmptyPage(null);

            Assert.That(page.Keys, Has.Count.EqualTo(20));
        }

        [Test]
        public void CreateEmptyPage_AddressesEveryCellExactlyOnce()
        {
            Mk20PageSettings page = Mk20LayoutSettings.CreateEmptyPage(null);

            var cells = page.Keys.Select(key => key.Row + "," + key.Column).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(cells.Distinct().Count(), Is.EqualTo(20));
                Assert.That(page.Keys.Select(key => key.Row), Is.All.InRange(0, 3));
                Assert.That(page.Keys.Select(key => key.Column), Is.All.InRange(0, 4));
            });
        }

        [Test]
        public void CreateEmptyPage_LeavesATopLevelPageUnassigned()
        {
            Mk20PageSettings page = Mk20LayoutSettings.CreateEmptyPage(null);

            Assert.Multiple(() =>
            {
                Assert.That(page.ParentPageId, Is.Null);
                Assert.That(page.Keys, Has.All.Property(nameof(Mk20KeySettings.ActionType))
                    .EqualTo(KeyActionKinds.Unassigned));
            });
        }

        [Test]
        public void CreateEmptyPage_GivesAFolderItsReturnKey()
        {
            // Without this a folder cannot be left, which is how the device behaves.
            Mk20PageSettings folder = Mk20LayoutSettings.CreateEmptyPage("parent-page");

            Mk20KeySettings bottomRight = folder.Keys.Last();

            Assert.Multiple(() =>
            {
                Assert.That(folder.ParentPageId, Is.EqualTo("parent-page"));
                Assert.That(bottomRight.Row, Is.EqualTo(3));
                Assert.That(bottomRight.Column, Is.EqualTo(4));
                Assert.That(bottomRight.ActionType, Is.EqualTo(KeyActionKinds.OneLevelUp));
            });
        }

        [Test]
        public void CreateEmptyPage_GivesEachPageItsOwnId()
        {
            string first = Mk20LayoutSettings.CreateEmptyPage(null).Id;
            string second = Mk20LayoutSettings.CreateEmptyPage(null).Id;

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void CreateDefault_StartsWithOneEmptyPage()
        {
            Mk20LayoutSettings layout = Mk20LayoutSettings.CreateDefault();

            Assert.Multiple(() =>
            {
                Assert.That(layout.Pages, Has.Count.EqualTo(1));
                Assert.That(layout.Pages[0].ParentPageId, Is.Null);
                Assert.That(layout.Pages[0].Keys, Has.Count.EqualTo(20));
            });
        }
    }

    /// <summary>
    /// Covers how an action is classified. Getting these wrong would send a device
    /// action to the plugin, or leave a plugin action unrouted.
    /// </summary>
    [TestFixture]
    public class KeyActionKindsTests
    {
        [TestCase(KeyActionKinds.OpenFolder)]
        [TestCase(KeyActionKinds.OneLevelUp)]
        [TestCase(KeyActionKinds.PreviousPage)]
        [TestCase(KeyActionKinds.NextPage)]
        public void Navigation_IsHandledByTheDevice(string action)
        {
            Assert.Multiple(() =>
            {
                Assert.That(KeyActionKinds.IsNavigation(action), Is.True);
                Assert.That(KeyActionKinds.RunsOnDevice(action), Is.True);
                Assert.That(KeyActionKinds.IsHostRouted(action), Is.False);
            });
        }

        [TestCase(KeyActionKinds.Macro)]
        [TestCase(KeyActionKinds.SimHubAction)]
        [TestCase(KeyActionKinds.SimHubInput)]
        public void PluginActions_AreHostRouted(string action)
        {
            Assert.Multiple(() =>
            {
                Assert.That(KeyActionKinds.IsHostRouted(action), Is.True);
                Assert.That(KeyActionKinds.RunsOnDevice(action), Is.False);
            });
        }

        [Test]
        public void Keystroke_RunsOnTheDeviceButIsNotNavigation()
        {
            // This is what keeps a keystroke working with SimHub closed.
            Assert.Multiple(() =>
            {
                Assert.That(KeyActionKinds.RunsOnDevice(KeyActionKinds.KeyboardKey), Is.True);
                Assert.That(KeyActionKinds.IsNavigation(KeyActionKinds.KeyboardKey), Is.False);
                Assert.That(KeyActionKinds.IsHostRouted(KeyActionKinds.KeyboardKey), Is.False);
            });
        }

        [Test]
        public void Unassigned_DoesNothingAnywhere()
        {
            Assert.Multiple(() =>
            {
                Assert.That(KeyActionKinds.IsNavigation(KeyActionKinds.Unassigned), Is.False);
                Assert.That(KeyActionKinds.IsHostRouted(KeyActionKinds.Unassigned), Is.False);
                Assert.That(KeyActionKinds.RunsOnDevice(KeyActionKinds.Unassigned), Is.False);
            });
        }

        [Test]
        public void All_ListsEveryKindOffered()
        {
            Assert.That(KeyActionKinds.All, Does.Contain(KeyActionKinds.Unassigned));
            Assert.That(KeyActionKinds.All.Distinct().Count(), Is.EqualTo(KeyActionKinds.All.Length));
        }

        [Test]
        public void GlyphFor_MarksNavigationKeysOnly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(KeyActionKinds.GlyphFor(KeyActionKinds.OpenFolder), Is.Not.Null);
                Assert.That(KeyActionKinds.GlyphFor(KeyActionKinds.Macro), Is.Null);
            });
        }
    }
}
