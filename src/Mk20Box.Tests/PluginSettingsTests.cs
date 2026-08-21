using System.Linq;
using Mk20Box.Layout;
using NUnit.Framework;

namespace Mk20Box.Tests
{
    /// <summary>
    /// Covers what <see cref="Mk20BoxPluginSettings.Normalize"/> guarantees to the
    /// rest of the plugin, which then trusts it and stops re-checking.
    /// </summary>
    [TestFixture]
    public class PluginSettingsTests
    {
        private static Mk20ProfileSettings ProfileWithKeys(string name, params string[] commandIds)
        {
            var profile = new Mk20ProfileSettings
            {
                Id = Mk20BoxPluginSettings.CreateProfileId(),
                Name = name,
                Layout = Mk20LayoutSettings.CreateDefault(),
            };

            for (int index = 0; index < commandIds.Length; index++)
            {
                profile.Layout.Pages[0].Keys[index].CommandId = commandIds[index];
            }

            return profile;
        }

        private static string[] CommandIdsOf(Mk20ProfileSettings profile)
        {
            return profile.Layout.Pages
                .SelectMany(page => page.Keys)
                .Select(key => key.CommandId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToArray();
        }

        [Test]
        public void Normalize_AlwaysLeavesAtLeastOneProfile()
        {
            var settings = new Mk20BoxPluginSettings();

            settings.Normalize();

            Assert.That(settings.Profiles, Is.Not.Empty);
        }

        [Test]
        public void Normalize_PointsTheGlobalSelectionAtARealProfile()
        {
            var settings = new Mk20BoxPluginSettings { GlobalProfileId = "does-not-exist" };

            settings.Normalize();

            Assert.That(settings.FindProfileById(settings.GlobalProfileId), Is.Not.Null);
        }

        [Test]
        public void Normalize_ReissuesADuplicateCommandId()
        {
            // Two keys sharing an id would collide in the router's index, and the
            // second would answer for the first.
            var settings = new Mk20BoxPluginSettings();
            settings.Profiles.Add(ProfileWithKeys("Clash", "mk20.same", "mk20.same"));

            settings.Normalize();

            string[] ids = CommandIdsOf(settings.Profiles.Single());

            Assert.Multiple(() =>
            {
                Assert.That(ids, Has.Length.EqualTo(2));
                Assert.That(ids[0], Is.EqualTo("mk20.same"), "the first claim keeps its id");
                Assert.That(ids[1], Is.Not.EqualTo("mk20.same"));
                Assert.That(ids[1], Is.Not.Null.And.Not.Empty);
            });
        }

        [Test]
        public void Normalize_LeavesDistinctCommandIdsAlone()
        {
            var settings = new Mk20BoxPluginSettings();
            settings.Profiles.Add(ProfileWithKeys("Fine", "mk20.a", "mk20.b", "mk20.c"));

            settings.Normalize();

            Assert.That(CommandIdsOf(settings.Profiles.Single()),
                Is.EqualTo(new[] { "mk20.a", "mk20.b", "mk20.c" }));
        }

        [Test]
        public void Normalize_ResolvesEveryDuplicateNoMatterHowMany()
        {
            var settings = new Mk20BoxPluginSettings();
            settings.Profiles.Add(ProfileWithKeys("Many", "x", "x", "x", "x", "x"));

            settings.Normalize();

            string[] ids = CommandIdsOf(settings.Profiles.Single());

            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        }

        [Test]
        public void Normalize_JudgesCommandIdsOnePerProfile()
        {
            // Only one profile is ever active, so the same id in another profile is
            // not a collision and must not be disturbed.
            var settings = new Mk20BoxPluginSettings();
            settings.Profiles.Add(ProfileWithKeys("First", "mk20.shared"));
            settings.Profiles.Add(ProfileWithKeys("Second", "mk20.shared"));

            settings.Normalize();

            Assert.That(settings.Profiles.Select(p => CommandIdsOf(p).Single()),
                Is.All.EqualTo("mk20.shared"));
        }

        [Test]
        public void Normalize_ReplacesADuplicateProfileId()
        {
            var settings = new Mk20BoxPluginSettings();
            settings.Profiles.Add(new Mk20ProfileSettings { Id = "same", Name = "A" });
            settings.Profiles.Add(new Mk20ProfileSettings { Id = "same", Name = "B" });

            settings.Normalize();

            Assert.That(settings.Profiles.Select(p => p.Id).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void Normalize_SortsProfilesByName()
        {
            var settings = new Mk20BoxPluginSettings();
            settings.Profiles.Add(new Mk20ProfileSettings { Id = "1", Name = "Zulu" });
            settings.Profiles.Add(new Mk20ProfileSettings { Id = "2", Name = "Alpha" });

            settings.Normalize();

            Assert.That(settings.Profiles.Select(p => p.Name), Is.EqualTo(new[] { "Alpha", "Zulu" }));
        }

        [Test]
        public void Normalize_DropsNullProfiles()
        {
            var settings = new Mk20BoxPluginSettings();
            settings.Profiles.Add(null);
            settings.Profiles.Add(new Mk20ProfileSettings { Id = "1", Name = "Real" });

            settings.Normalize();

            Assert.That(settings.Profiles, Has.None.Null);
        }

        [Test]
        public void FindProfileByName_IgnoresCase()
        {
            var settings = new Mk20BoxPluginSettings();
            settings.Profiles.Add(new Mk20ProfileSettings { Id = "1", Name = "LMU GT3" });
            settings.Normalize();

            Assert.That(settings.FindProfileByName("lmu gt3"), Is.Not.Null);
        }

        [Test]
        public void FindProfileById_ReturnsNullForAnUnknownId()
        {
            var settings = new Mk20BoxPluginSettings();
            settings.Normalize();

            Assert.That(settings.FindProfileById("nope"), Is.Null);
        }

        [Test]
        public void CreateProfileId_IsUniquePerCall()
        {
            string[] ids = Enumerable.Range(0, 50)
                .Select(_ => Mk20BoxPluginSettings.CreateProfileId())
                .ToArray();

            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        }
    }
}
