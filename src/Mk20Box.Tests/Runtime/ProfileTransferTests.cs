using System.IO;
using System.Linq;
using Mk20Box.Layout;
using Mk20Box.Runtime;
using NUnit.Framework;

namespace Mk20Box.Tests.Runtime
{
    /// <summary>
    /// Covers sharing a profile as a file. Export and import are the only place a
    /// layout leaves the machine, so a round trip has to come back intact.
    /// </summary>
    [TestFixture]
    public class ProfileTransferTests
    {
        private string workingFolder;

        [SetUp]
        public void CreateWorkingFolder()
        {
            workingFolder = Path.Combine(Path.GetTempPath(), "Mk20BoxTests-" + Path.GetRandomFileName());
            Directory.CreateDirectory(workingFolder);
        }

        [TearDown]
        public void RemoveWorkingFolder()
        {
            try
            {
                if (Directory.Exists(workingFolder))
                {
                    Directory.Delete(workingFolder, true);
                }
            }
            catch (IOException)
            {
                // A leftover temp folder is not worth failing a test over.
            }
        }

        private string PathFor(string name)
        {
            return Path.Combine(workingFolder, name + ProfileTransfer.FileExtension);
        }

        private static Mk20ProfileSettings SampleProfile(string name)
        {
            var profile = new Mk20ProfileSettings
            {
                Id = Mk20BoxPluginSettings.CreateProfileId(),
                Name = name,
                Layout = Mk20LayoutSettings.CreateDefault(),
            };

            Mk20KeySettings key = profile.Layout.Pages[0].Keys[0];
            key.Title = "PIT";
            key.ActionType = KeyActionKinds.Macro;
            key.MacroSteps.Add(new Mk20MacroStepSettings
            {
                Kind = MacroStepKinds.Text,
                Text = "hello",
            });

            return profile;
        }

        private static Mk20BoxPluginSettings EmptySettings()
        {
            var settings = new Mk20BoxPluginSettings();
            settings.Normalize();
            return settings;
        }

        [Test]
        public void Export_WritesAFile()
        {
            string path = PathFor("exported");

            ProfileTransfer.Export(SampleProfile("LMU GT3"), path);

            Assert.That(File.Exists(path), Is.True);
        }

        [Test]
        public void RoundTrip_KeepsTheLayout()
        {
            string path = PathFor("roundtrip");
            ProfileTransfer.Export(SampleProfile("LMU GT3"), path);

            Mk20BoxPluginSettings settings = EmptySettings();
            Mk20ProfileSettings imported = ProfileTransfer.Import(path, settings);

            Mk20KeySettings key = imported.Layout.Pages[0].Keys[0];

            Assert.Multiple(() =>
            {
                Assert.That(imported.Layout.Pages, Has.Count.EqualTo(1));
                Assert.That(imported.Layout.Pages[0].Keys, Has.Count.EqualTo(20));
                Assert.That(key.Title, Is.EqualTo("PIT"));
                Assert.That(key.ActionType, Is.EqualTo(KeyActionKinds.Macro));
                Assert.That(key.MacroSteps, Has.Count.EqualTo(1));
                Assert.That(key.MacroSteps[0].Text, Is.EqualTo("hello"));
            });
        }

        [Test]
        public void Import_AddsTheProfileToTheSettings()
        {
            string path = PathFor("added");
            ProfileTransfer.Export(SampleProfile("Imported"), path);

            Mk20BoxPluginSettings settings = EmptySettings();
            int before = settings.Profiles.Count;

            ProfileTransfer.Import(path, settings);

            Assert.That(settings.Profiles.Count, Is.EqualTo(before + 1));
        }

        [Test]
        public void Import_NeverOverwritesAnExistingProfile()
        {
            // Importing the same file twice must give two profiles, not one edited.
            string path = PathFor("twice");
            ProfileTransfer.Export(SampleProfile("Shared"), path);

            Mk20BoxPluginSettings settings = EmptySettings();
            Mk20ProfileSettings first = ProfileTransfer.Import(path, settings);
            Mk20ProfileSettings second = ProfileTransfer.Import(path, settings);

            Assert.Multiple(() =>
            {
                Assert.That(first.Id, Is.Not.EqualTo(second.Id));
                Assert.That(first.Name, Is.Not.EqualTo(second.Name));
                Assert.That(settings.Profiles.Select(p => p.Name).Distinct().Count(),
                    Is.EqualTo(settings.Profiles.Count));
            });
        }

        [Test]
        public void Import_GivesTheProfileAFreshId()
        {
            Mk20ProfileSettings source = SampleProfile("Original");
            string path = PathFor("freshid");
            ProfileTransfer.Export(source, path);

            Mk20ProfileSettings imported = ProfileTransfer.Import(path, EmptySettings());

            Assert.That(imported.Id, Is.Not.EqualTo(source.Id));
        }

        [Test]
        public void PeekName_ReadsTheNameWithoutImporting()
        {
            string path = PathFor("peek");
            ProfileTransfer.Export(SampleProfile("Le Mans"), path);

            Mk20BoxPluginSettings settings = EmptySettings();
            int before = settings.Profiles.Count;

            string name = ProfileTransfer.PeekName(path);

            Assert.Multiple(() =>
            {
                Assert.That(name, Is.EqualTo("Le Mans"));
                Assert.That(settings.Profiles.Count, Is.EqualTo(before));
            });
        }

        [Test]
        public void SuggestFileName_UsesTheProfileName()
        {
            string suggested = ProfileTransfer.SuggestFileName(SampleProfile("LMU GT3"));

            Assert.Multiple(() =>
            {
                Assert.That(suggested, Does.StartWith("LMU GT3"));
                Assert.That(suggested, Does.EndWith(ProfileTransfer.FileExtension));
            });
        }

        [Test]
        public void SuggestFileName_RemovesCharactersAPathCannotHold()
        {
            string suggested = ProfileTransfer.SuggestFileName(SampleProfile("bad/name:here"));

            Assert.That(suggested.IndexOfAny(Path.GetInvalidFileNameChars()), Is.EqualTo(-1));
        }

        [Test]
        public void ExportedProfile_CarriesNoAbsolutePathToTheBundledLibrary()
        {
            // Library icons are referenced, not copied, so the file stays small and
            // works on a machine where SimHub lives somewhere else.
            Mk20ProfileSettings profile = SampleProfile("Referenced");
            string path = PathFor("small");

            ProfileTransfer.Export(profile, path);

            Assert.That(new FileInfo(path).Length, Is.LessThan(64 * 1024));
        }
    }
}
