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
        static Mk20BoxPlugin()
        {
            // Must run before any MK20Control type is resolved.
            DependencyResolver.Install();
        }

        private readonly object settingsSync = new object();
        private string activeGameName = string.Empty;
        private string activeProfileName = "Default";
        private bool activeProfileIsGlobal = true;
        private string uploadedProfileId;
        private readonly List<string> supportedGames = new List<string>();

        public Mk20BoxPluginSettings Settings;

        /// <summary>Connection to the MK20 hardware.</summary>
        public Mk20DeviceConnection Device { get; } = new Mk20DeviceConnection();

        /// <summary>Routes device presses to macros and SimHub actions.</summary>
        public Mk20Box.Runtime.CommandRouter Router { get; } = new Mk20Box.Runtime.CommandRouter();

        /// <summary>Current plugin manager instance (set by SimHub).</summary>
        public PluginManager PluginManager { get; set; }

        /// <summary>Left-menu icon (24x24, black/white friendly).</summary>
        public ImageSource PictureIcon => this.ToIcon(LoadMenuIcon());

        private static System.Drawing.Bitmap LoadMenuIcon()
        {
            using (var stream = typeof(Mk20BoxPlugin).Assembly
                .GetManifestResourceStream("Mk20Box.sdkmenuicon.png"))
            {
                return new System.Drawing.Bitmap(stream);
            }
        }

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
            this.AttachDelegate("DeviceConnected", () => Device.IsConnected);
            this.AttachDelegate("DeviceStatus", () => Device.Status);
            this.AttachDelegate("DevicePort", () => Device.PortName ?? string.Empty);

            RefreshSelectedGame();
            RefreshActiveProfile();

            Router.Bridge = new Mk20Box.Runtime.SimHubBridge(PluginManager, GetType())
            {
                InputDelayMs = Settings.InputDelayMs,
            };

            // Registers the profile's SimHub inputs so they can be mapped in
            // Controls & Events even before the device is plugged in.
            Router.SetActiveLayout(ActiveProfileLayout());

            Device.StateChanged += DeviceStateChanged;

            if (Settings.AutoConnect && !string.IsNullOrWhiteSpace(Settings.DevicePortName))
            {
                ConnectDeviceInBackground(Settings.DevicePortName);
            }
        }

        /// <summary>Rebinds the router whenever the connection comes or goes.</summary>
        private void DeviceStateChanged(object sender, EventArgs e)
        {
            if (Device.IsConnected)
            {
                Router.Attach(Device.Client);
                Router.SetActiveLayout(ActiveProfileLayout());

                // A game may already be running, so push the resolved profile now.
                RefreshActiveProfile();
            }
            else
            {
                // Force a re-upload after reconnecting.
                uploadedProfileId = null;
                Router.Detach();
            }
        }

        /// <summary>Profile that should be on the device right now.</summary>
        public Mk20ProfileSettings ActiveProfile
        {
            get
            {
                lock (settingsSync)
                {
                    return ResolveActiveProfileCore();
                }
            }
        }

        /// <summary>Profiles offered for a game: its own, plus any unscoped ones.</summary>
        public List<Mk20ProfileSettings> ProfilesForGame(string gameName)
        {
            lock (settingsSync)
            {
                return Settings.Profiles
                    .Where(profile => profile != null && profile.IsForGame(gameName))
                    .ToList();
            }
        }

        /// <summary>Points a game at a profile, creating the binding if needed.</summary>
        public void SetProfileForGame(string gameName, string profileId)
        {
            if (string.IsNullOrWhiteSpace(gameName) || string.IsNullOrWhiteSpace(profileId))
            {
                return;
            }

            lock (settingsSync)
            {
                Mk20GameProfileBindingSettings binding = Settings.FindGameProfile(gameName);
                if (binding == null)
                {
                    binding = new Mk20GameProfileBindingSettings { GameName = gameName.Trim() };
                    Settings.GameProfiles.Add(binding);
                    Settings.SortGameProfiles();
                }

                binding.ProfileId = profileId;
                SaveSettingsCore();
                RefreshActiveProfileCore();
            }
        }

        private Mk20Box.Layout.Mk20LayoutSettings ActiveProfileLayout()
        {
            lock (settingsSync)
            {
                // Must match what was uploaded, otherwise the command ids the device
                // reports will not be found and presses are silently dropped.
                Mk20ProfileSettings profile = ResolveActiveProfileCore();
                return profile?.Layout;
            }
        }

        /// <summary>
        /// The profile that should be on the device: the global one when that mode is
        /// on, otherwise the current game's, falling back to global.
        /// </summary>
        private Mk20ProfileSettings ResolveActiveProfileCore()
        {
            Mk20ProfileSettings globalProfile = Settings.FindProfileById(Settings.GlobalProfileId)
                ?? Settings.Profiles.FirstOrDefault();

            if (Settings.UseGlobalProfile)
            {
                return globalProfile;
            }

            Mk20GameProfileBindingSettings binding = Settings.FindGameProfile(activeGameName);
            Mk20ProfileSettings gameProfile = binding == null
                ? null
                : Settings.FindProfileById(binding.ProfileId);

            // With per-game profiles, an unbound or stale binding should still land on
            // this game's own profile rather than another game's.
            if (gameProfile == null && !string.IsNullOrWhiteSpace(activeGameName))
            {
                gameProfile = Settings.Profiles.FirstOrDefault(profile =>
                    string.Equals(profile.GameName, activeGameName, StringComparison.OrdinalIgnoreCase));
            }

            return gameProfile ?? globalProfile;
        }

        /// <summary>Connects without blocking SimHub's startup.</summary>
        private void ConnectDeviceInBackground(string portName)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await Device.ConnectAsync(portName).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    SimHub.Logging.Current.Warn($"[MK20Box] Auto-connect failed: {ex.Message}");
                }
            });
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
            Router.Dispose();
            Device.DisconnectAsync().GetAwaiter().GetResult();
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

        /// <summary>
        /// Restores a fresh install: every profile, layout, game binding and
        /// preference is discarded. The device connection is left alone so the user
        /// does not have to reconnect.
        /// </summary>
        public void ResetAllSettings()
        {
            lock (settingsSync)
            {
                string portName = Settings.DevicePortName;
                bool autoConnect = Settings.AutoConnect;

                Settings = new Mk20BoxPluginSettings
                {
                    DevicePortName = portName,
                    AutoConnect = autoConnect,
                };

                Settings.Normalize();
                uploadedProfileId = null;
                SaveSettingsCore();
                RefreshActiveProfileCore();
            }

            Router.SetActiveLayout(ActiveProfileLayout());
            SimHub.Logging.Current.Info("[MK20Box] Settings reset to defaults");
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
                    if (!string.Equals(binding.ProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Prefer another profile belonging to the same game; falling straight
                    // back to the global one would show another game's profile here.
                    Mk20ProfileSettings replacement = Settings.Profiles
                        .FirstOrDefault(candidate => candidate.IsForGame(binding.GameName));

                    binding.ProfileId = (replacement ?? fallback).Id;
                }

                SaveSettingsCore();
                RefreshActiveProfileCore();
                return true;
            }
        }

        /// <summary>
        /// Rebuilds a profile from scratch: a fresh layout with one empty page, all
        /// twenty keys blank and both encoders unassigned.
        /// </summary>
        public bool ResetProfileLayout(Mk20ProfileSettings profile)
        {
            if (profile == null)
            {
                return false;
            }

            lock (settingsSync)
            {
                profile.Layout = Mk20Box.Layout.Mk20LayoutSettings.CreateDefault();
                SaveSettingsCore();
                RefreshActiveProfileCore();
                return true;
            }
        }

        /// <summary>
        /// Composes a profile's layout into a theme and uploads it. The theme name is
        /// derived from the profile so each profile keeps its own on the device.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> SendProfileToDeviceAsync(Mk20ProfileSettings profile)
        {
            if (profile == null)
            {
                return false;
            }

            Mk20Box.Layout.Mk20LayoutSettings layout;
            lock (settingsSync)
            {
                layout = profile.Layout ?? Mk20Box.Layout.Mk20LayoutSettings.CreateDefault();
            }

            Mk20Control.Protocol.Theme.ThemeFile theme = Mk20Box.Layout.ThemeComposer.Compose(layout);
            SaveSettings();

            // Composition assigns any missing command ids, so reindex before uploading.
            Router.SetActiveLayout(layout);

            bool uploaded = await Device.UploadThemeAsync(ThemeNameFor(profile), theme).ConfigureAwait(false);

            if (uploaded)
            {
                // Counts as the profile now on the device, so auto-upload won't repeat it.
                uploadedProfileId = profile.Id;
            }

            return uploaded;
        }

        /// <summary>Device theme names allow a limited character set, so keep it simple.</summary>
        private static string ThemeNameFor(Mk20ProfileSettings profile)
        {
            string name = (profile.Name ?? "profile").Trim();
            var safe = new System.Text.StringBuilder();

            foreach (char character in name)
            {
                safe.Append(char.IsLetterOrDigit(character) ? character : '-');
            }

            string result = safe.ToString().Trim('-');
            return result.Length == 0 ? "mk20box" : "mk20box-" + result;
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
            Mk20ProfileSettings profile = ResolveActiveProfileCore();
            bool isGlobal = Settings.UseGlobalProfile
                || Settings.FindGameProfile(activeGameName) == null
                || profile == null;

            ApplyProfile(profile, isGlobal);
        }

        private void ApplyProfile(Mk20ProfileSettings profile, bool isGlobal)
        {
            activeProfileIsGlobal = isGlobal;
            activeProfileName = profile == null || string.IsNullOrWhiteSpace(profile.Name)
                ? "Default"
                : profile.Name.Trim();

            AutoUploadProfile(profile);
        }

        /// <summary>
        /// Pushes the resolved profile to the device when the active one changes, so
        /// starting a game swaps the keypad without any manual upload. Skipped when the
        /// same profile is already on the device.
        /// </summary>
        private void AutoUploadProfile(Mk20ProfileSettings profile)
        {
            if (profile == null || !Settings.AutoUploadProfile || !Device.IsConnected)
            {
                return;
            }

            if (string.Equals(uploadedProfileId, profile.Id, StringComparison.Ordinal))
            {
                return;
            }

            uploadedProfileId = profile.Id;

            // Off the caller's thread: this runs from DataUpdate and takes settingsSync.
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    if (await SendProfileToDeviceAsync(profile).ConfigureAwait(false))
                    {
                        SimHub.Logging.Current.Info(
                            $"[MK20Box] Loaded profile '{profile.Name}' for '{activeGameName}'");
                    }
                }
                catch (Exception ex)
                {
                    SimHub.Logging.Current.Warn(
                        $"[MK20Box] Could not load profile '{profile.Name}': {ex.Message}");

                    // Let the next change retry.
                    uploadedProfileId = null;
                }
            });
        }
    }
}
