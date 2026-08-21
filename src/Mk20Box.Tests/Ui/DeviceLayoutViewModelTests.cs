using System.Linq;
using Mk20Box.Layout;
using Mk20Box.Ui;
using NUnit.Framework;

namespace Mk20Box.Tests.Ui
{
    /// <summary>
    /// Covers the layout editor: copying keys between cells, pages and folders,
    /// and the page bookkeeping the device depends on.
    /// </summary>
    [TestFixture]
    public class DeviceLayoutViewModelTests
    {
        private static DeviceLayoutViewModel NewLayout()
        {
            return new DeviceLayoutViewModel(Mk20LayoutSettings.CreateDefault());
        }

        private static DeviceKeyViewModel KeyAt(DeviceLayoutViewModel layout, int index)
        {
            return layout.SelectedPage.Keys[index];
        }

        [Test]
        public void NewLayout_OpensOnAFullPage()
        {
            DeviceLayoutViewModel layout = NewLayout();

            Assert.Multiple(() =>
            {
                Assert.That(layout.Pages, Has.Count.EqualTo(1));
                Assert.That(layout.SelectedPage.Keys, Has.Count.EqualTo(20));
                Assert.That(layout.SelectedKey, Is.SameAs(layout.SelectedPage.Keys[0]));
                Assert.That(layout.CanGoBack, Is.False);
                Assert.That(layout.IsInFolder, Is.False);
            });
        }

        [Test]
        public void PasteKey_CopiesTheLookAndAction()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel source = KeyAt(layout, 0);
            DeviceKeyViewModel target = KeyAt(layout, 1);

            source.Title = "PIT";
            source.MediaPath = @"C:\icons\pit.png";
            source.ActionType = KeyActionKinds.SimHubAction;
            source.ActionTarget = "DoThing";

            layout.CopyKey(source);
            layout.PasteKey(target);

            Assert.Multiple(() =>
            {
                Assert.That(target.Title, Is.EqualTo("PIT"));
                Assert.That(target.MediaPath, Is.EqualTo(@"C:\icons\pit.png"));
                Assert.That(target.ActionType, Is.EqualTo(KeyActionKinds.SimHubAction));
                Assert.That(target.ActionTarget, Is.EqualTo("DoThing"));
            });
        }

        [Test]
        public void PasteKey_LeavesTheTargetWhereItIs()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel source = KeyAt(layout, 0);
            DeviceKeyViewModel target = KeyAt(layout, 7);

            source.Title = "PIT";
            layout.CopyKey(source);
            layout.PasteKey(target);

            Assert.Multiple(() =>
            {
                Assert.That(target.Row, Is.EqualTo(1));
                Assert.That(target.Column, Is.EqualTo(2));
                Assert.That(target.Number, Is.EqualTo(8));
            });
        }

        [Test]
        public void PasteKey_GivesTheCopyItsOwnCommandId()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel source = KeyAt(layout, 0);
            DeviceKeyViewModel target = KeyAt(layout, 1);

            source.ActionType = KeyActionKinds.Macro;
            source.Model.CommandId = "mk20.original";

            layout.CopyKey(source);
            layout.PasteKey(target);

            Assert.Multiple(() =>
            {
                Assert.That(target.Model.CommandId, Is.Not.Null.And.Not.Empty);
                Assert.That(target.Model.CommandId, Is.Not.EqualTo("mk20.original"));
                Assert.That(source.Model.CommandId, Is.EqualTo("mk20.original"));
            });
        }

        [Test]
        public void PasteKey_NeverLeavesTwoKeysSharingACommandId()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel source = KeyAt(layout, 0);
            source.ActionType = KeyActionKinds.Macro;

            layout.CopyKey(source);

            for (int index = 1; index < 20; index++)
            {
                layout.PasteKey(KeyAt(layout, index));
            }

            string[] ids = layout.Pages
                .SelectMany(page => page.Keys)
                .Select(key => key.Model.CommandId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToArray();

            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        }

        [Test]
        public void PasteKey_GivesAPastedFolderKeyItsOwnFolder()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel source = KeyAt(layout, 0);
            DeviceKeyViewModel target = KeyAt(layout, 1);

            source.ActionType = KeyActionKinds.OpenFolder;
            string sourceFolder = source.TargetPageId;

            layout.CopyKey(source);
            layout.PasteKey(target);

            Assert.Multiple(() =>
            {
                Assert.That(sourceFolder, Is.Not.Null.And.Not.Empty);
                Assert.That(target.TargetPageId, Is.Not.Null.And.Not.Empty);
                Assert.That(target.TargetPageId, Is.Not.EqualTo(sourceFolder),
                    "two keys must not open the same folder");
            });
        }

        [Test]
        public void PasteKey_CanCarryAKeyIntoAFolder()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel source = KeyAt(layout, 0);
            source.Title = "CARRIED";
            layout.CopyKey(source);

            DeviceKeyViewModel opener = KeyAt(layout, 1);
            opener.ActionType = KeyActionKinds.OpenFolder;
            layout.ActivateKey(opener);

            layout.PasteKey(KeyAt(layout, 0));

            Assert.Multiple(() =>
            {
                Assert.That(layout.IsInFolder, Is.True);
                Assert.That(layout.SelectedPage.Keys[0].Title, Is.EqualTo("CARRIED"));
            });
        }

        [Test]
        public void CopyKey_TakesASnapshotRatherThanALiveReference()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel source = KeyAt(layout, 0);
            source.Title = "AT COPY TIME";

            layout.CopyKey(source);
            source.Title = "EDITED LATER";

            DeviceKeyViewModel target = KeyAt(layout, 1);
            layout.PasteKey(target);

            Assert.That(target.Title, Is.EqualTo("AT COPY TIME"));
        }

        [Test]
        public void PasteKey_DoesNothingWithoutAKey()
        {
            DeviceLayoutViewModel layout = NewLayout();
            layout.CopyKey(KeyAt(layout, 0));

            Assert.That(layout.PasteKey(null), Is.False);
        }

        [Test]
        public void ResetKey_ClearsTheKey()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel key = KeyAt(layout, 0);

            key.Title = "PIT";
            key.MediaPath = @"C:\icons\pit.png";
            key.ActionType = KeyActionKinds.SimHubAction;

            layout.ResetKey(key);

            Assert.Multiple(() =>
            {
                Assert.That(key.Title, Is.Null);
                Assert.That(key.HasMedia, Is.False);
                Assert.That(key.ActionType, Is.EqualTo(KeyActionKinds.Unassigned));
                Assert.That(key.HasAction, Is.False);
            });
        }

        [Test]
        public void ResetKey_LeavesTheCellAlone()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel key = KeyAt(layout, 12);

            layout.ResetKey(key);

            Assert.That(key.Number, Is.EqualTo(13));
        }

        [Test]
        public void ResetKey_DoesNothingWithoutAKey()
        {
            Assert.That(NewLayout().ResetKey(null), Is.False);
        }

        [Test]
        public void FolderHasContent_IgnoresTheReturnKeyAFolderIsBornWith()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel opener = KeyAt(layout, 0);
            opener.ActionType = KeyActionKinds.OpenFolder;

            Assert.That(layout.FolderHasContent(opener), Is.False);
        }

        [Test]
        public void FolderHasContent_SpotsAConfiguredFolder()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel opener = KeyAt(layout, 0);
            opener.ActionType = KeyActionKinds.OpenFolder;

            layout.ActivateKey(opener);
            layout.SelectedPage.Keys[0].Title = "SOMETHING";

            Assert.That(layout.FolderHasContent(opener), Is.True);
        }

        [Test]
        public void FolderHasContent_IsFalseForAKeyWithoutAFolder()
        {
            DeviceLayoutViewModel layout = NewLayout();

            Assert.Multiple(() =>
            {
                Assert.That(layout.FolderHasContent(KeyAt(layout, 0)), Is.False);
                Assert.That(layout.FolderHasContent(null), Is.False);
            });
        }

        [Test]
        public void OpeningAFolder_CreatesItWithAReturnKey()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel opener = KeyAt(layout, 0);

            opener.ActionType = KeyActionKinds.OpenFolder;

            ThemePageViewModel folder = layout.Pages.Single(page => page.Id == opener.TargetPageId);

            Assert.Multiple(() =>
            {
                Assert.That(layout.Pages, Has.Count.EqualTo(2));
                Assert.That(folder.IsFolder, Is.True);
                Assert.That(folder.Keys.Last().ActionType, Is.EqualTo(KeyActionKinds.OneLevelUp));
            });
        }

        [Test]
        public void EnteringAndLeavingAFolder_ReturnsWhereItStarted()
        {
            DeviceLayoutViewModel layout = NewLayout();
            ThemePageViewModel start = layout.SelectedPage;
            DeviceKeyViewModel opener = KeyAt(layout, 0);
            opener.ActionType = KeyActionKinds.OpenFolder;

            layout.ActivateKey(opener);
            Assert.That(layout.IsInFolder, Is.True);

            layout.ActivateKey(layout.SelectedPage.Keys.Last());

            Assert.Multiple(() =>
            {
                Assert.That(layout.SelectedPage, Is.SameAs(start));
                Assert.That(layout.CanGoBack, Is.False);
            });
        }

        [Test]
        public void ChangingTheActionAwayFromAFolder_DropsTheLink()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel key = KeyAt(layout, 0);

            key.ActionType = KeyActionKinds.OpenFolder;
            key.ActionType = KeyActionKinds.Unassigned;

            Assert.That(key.TargetPageId, Is.Null);
        }

        [Test]
        public void AddPage_JoinsTheTopLevelRing()
        {
            DeviceLayoutViewModel layout = NewLayout();

            layout.AddPageCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(layout.TopLevelPages, Has.Count.EqualTo(2));
                Assert.That(layout.PageIndicator, Is.EqualTo("Page 2 of 2"));
            });
        }

        [Test]
        public void NextPage_WrapsAround()
        {
            DeviceLayoutViewModel layout = NewLayout();
            layout.AddPageCommand.Execute(null);

            layout.NextPageCommand.Execute(null);

            Assert.That(layout.PageIndicator, Is.EqualTo("Page 1 of 2"));
        }

        [Test]
        public void DeletePage_RefusesToRemoveTheLastOne()
        {
            DeviceLayoutViewModel layout = NewLayout();

            layout.DeletePageCommand.Execute(null);

            Assert.That(layout.TopLevelPages, Has.Count.EqualTo(1));
        }

        [Test]
        public void DeletePage_TakesItsFoldersWithIt()
        {
            DeviceLayoutViewModel layout = NewLayout();
            layout.AddPageCommand.Execute(null);

            DeviceKeyViewModel opener = KeyAt(layout, 0);
            opener.ActionType = KeyActionKinds.OpenFolder;
            Assert.That(layout.Pages, Has.Count.EqualTo(3));

            layout.DeletePageCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(layout.TopLevelPages, Has.Count.EqualTo(1));
                Assert.That(layout.Pages, Has.Count.EqualTo(1), "the folder must go with its page");
            });
        }

        [Test]
        public void PageIndicator_SaysFolderWhileInsideOne()
        {
            DeviceLayoutViewModel layout = NewLayout();
            DeviceKeyViewModel opener = KeyAt(layout, 0);
            opener.ActionType = KeyActionKinds.OpenFolder;
            layout.ActivateKey(opener);

            Assert.That(layout.PageIndicator, Is.EqualTo("Folder"));
        }

        [Test]
        public void Changed_IsRaisedSoTheEditorCanSave()
        {
            DeviceLayoutViewModel layout = NewLayout();
            int raised = 0;
            layout.Changed += (sender, args) => raised++;

            layout.CopyKey(KeyAt(layout, 0));
            layout.PasteKey(KeyAt(layout, 1));
            layout.ResetKey(KeyAt(layout, 2));

            Assert.That(raised, Is.GreaterThanOrEqualTo(2));
        }
    }
}
