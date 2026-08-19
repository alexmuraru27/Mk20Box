using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace Mk20Box
{
    [PluginDescription("SimHub button box plugin for Waveshare MK20")]
    [PluginAuthor("alexmuraru27")]
    [PluginName("MK20Box")]
    public class Mk20BoxPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        private readonly object settingsSync = new object();
        private string activeGameName = string.Empty;
        private string activeProfileName = "Default";
        private bool activeProfileIsGlobal = true;
        private readonly List<string> supportedGames = new List<string>();

        public Mk20BoxPluginSettings Settings;

        /// <summary>Current plugin manager instance (set by SimHub).</summary>
        public PluginManager PluginManager { get; set; }

        /// <summary>Left-menu icon (24x24, black/white friendly).</summary>
        public ImageSource PictureIcon => this.ToIcon(Properties.Resources.sdkmenuicon);

        /// <summary>Short title shown in SimHub's left menu.</summary>
        public string LeftMenuTitle => "MK20Box";

        public string ActiveGameName => activeGameName;

        public string ActiveProfileName => activeProfileName;

        public bool ActiveProfileIsGlobal => activeProfileIsGlobal;

        public IReadOnlyList<string> SupportedGames => supportedGames;

        /// <summary>Called once after plugin startup.</summary>
        public void Init(PluginManager pluginManager)
        {
            SimHub.Logging.Current.Info("[MK20Box] Plugin starting");
            PluginManager = pluginManager;

            Settings = this.ReadCommonSettings<Mk20BoxPluginSettings>("GeneralSettings", () => new Mk20BoxPluginSettings());
            Settings.Normalize();
            LoadSupportedGames();

            this.AttachDelegate("Status", () => "loaded");
            this.AttachDelegate("ActiveGame", () => ActiveGameName);
            this.AttachDelegate("ActiveProfile", () => ActiveProfileName);
            this.AttachDelegate("ActiveProfileIsGlobal", () => ActiveProfileIsGlobal);

            RefreshSelectedGame();
            RefreshActiveProfile();
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            UpdateSelectedGame(pluginManager.GameName);
        }

        public void RefreshSelectedGame()
        {
            UpdateSelectedGame(PluginManager?.GameName);
        }

        private void UpdateSelectedGame(string gameName)
        {
            string selectedGame = gameName ?? string.Empty;
            if (!string.Equals(activeGameName, selectedGame, StringComparison.Ordinal))
            {
                activeGameName = selectedGame;
                RefreshActiveProfile();
            }
        }

        private void LoadSupportedGames()
        {
            supportedGames.Clear();
            if (SimHub.Plugins.Configuration.Games != null)
            {
                supportedGames.AddRange(
                    SimHub.Plugins.Configuration.Games
                    .Where(game => game != null && !string.IsNullOrWhiteSpace(game.Name))
                    .Select(game => game.Name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(gameName => gameName, StringComparer.CurrentCultureIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(PluginManager?.GameName)
                && !supportedGames.Contains(PluginManager.GameName, StringComparer.OrdinalIgnoreCase))
            {
                supportedGames.Add(PluginManager.GameName);
                supportedGames.Sort(StringComparer.CurrentCultureIgnoreCase);
            }
        }

        /// <summary>Called at plugin stop; persist settings here.</summary>
        public void End(PluginManager pluginManager)
        {
            SaveSettings();
        }

        /// <summary>Settings UI shown in SimHub's left menu.</summary>
        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new Mk20BoxSettingsControl(this);
        }

        public Mk20ProfileSettings AddProfile(string profileName)
        {
            string normalizedProfileName = (profileName ?? string.Empty).Trim();
            if (normalizedProfileName.Length == 0)
            {
                throw new ArgumentException("A profile name is required.", nameof(profileName));
            }

            lock (settingsSync)
            {
                Mk20ProfileSettings existing = Settings.FindProfileByName(normalizedProfileName);
                if (existing != null)
                {
                    return existing;
                }

                var profile = new Mk20ProfileSettings
                {
                    Id = Mk20BoxPluginSettings.CreateProfileId(),
                    Name = normalizedProfileName,
                };

                Settings.Profiles.Add(profile);
                Settings.SortProfiles();
                SaveSettingsCore();
                return profile;
            }
        }

        public bool DeleteProfile(Mk20ProfileSettings profile)
        {
            if (profile == null)
            {
                return false;
            }

            lock (settingsSync)
            {
                if (Settings.Profiles.Count <= 1 || !Settings.Profiles.Contains(profile))
                {
                    return false;
                }

                Settings.Profiles.Remove(profile);
                Mk20ProfileSettings fallback = Settings.Profiles[0];

                if (string.Equals(Settings.GlobalProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
                {
                    Settings.GlobalProfileId = fallback.Id;
                }

                foreach (Mk20GameProfileBindingSettings binding in Settings.GameProfiles)
                {
                    if (string.Equals(binding.ProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        binding.ProfileId = Settings.GlobalProfileId;
                    }
                }

                SaveSettingsCore();
                RefreshActiveProfileCore();
                return true;
            }
        }

        public Mk20GameProfileBindingSettings AddGameProfile(string gameName)
        {
            string normalizedGameName = (gameName ?? string.Empty).Trim();
            if (normalizedGameName.Length == 0)
            {
                throw new ArgumentException("A game name is required.", nameof(gameName));
            }

            lock (settingsSync)
            {
                Mk20GameProfileBindingSettings existing = Settings.FindGameProfile(normalizedGameName);
                if (existing != null)
                {
                    return existing;
                }

                var binding = new Mk20GameProfileBindingSettings
                {
                    GameName = normalizedGameName,
                    ProfileId = Settings.GlobalProfileId,
                };

                Settings.GameProfiles.Add(binding);
                Settings.SortGameProfiles();
                SaveSettingsCore();
                RefreshActiveProfileCore();
                return binding;
            }
        }

        public void RemoveGameProfile(Mk20GameProfileBindingSettings binding)
        {
            if (binding == null)
            {
                return;
            }

            lock (settingsSync)
            {
                Settings.GameProfiles.Remove(binding);
                SaveSettingsCore();
                RefreshActiveProfileCore();
            }
        }

        public void SaveSettings()
        {
            lock (settingsSync)
            {
                Settings.Normalize();
                SaveSettingsCore();
                RefreshActiveProfileCore();
            }
        }

        public void RefreshActiveProfile()
        {
            lock (settingsSync)
            {
                RefreshActiveProfileCore();
            }
        }

        private void SaveSettingsCore()
        {
            this.SaveCommonSettings("GeneralSettings", Settings);
        }

        private void RefreshActiveProfileCore()
        {
            if (Settings.UseGlobalProfile)
            {
                ApplyProfile(Settings.FindProfileById(Settings.GlobalProfileId), true);
                return;
            }

            Mk20GameProfileBindingSettings binding = Settings.FindGameProfile(activeGameName);
            Mk20ProfileSettings profile = binding == null
                ? null
                : Settings.FindProfileById(binding.ProfileId);
            if (profile == null)
            {
                ApplyProfile(Settings.FindProfileById(Settings.GlobalProfileId), true);
                return;
            }

            ApplyProfile(profile, false);
        }

        private void ApplyProfile(Mk20ProfileSettings profile, bool isGlobal)
        {
            activeProfileIsGlobal = isGlobal;
            activeProfileName = profile == null || string.IsNullOrWhiteSpace(profile.Name)
                ? "Default"
                : profile.Name.Trim();
        }
    }
}
