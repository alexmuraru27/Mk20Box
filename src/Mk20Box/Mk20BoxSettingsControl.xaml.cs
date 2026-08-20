using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Mk20Box
{
    public partial class Mk20BoxSettingsControl : UserControl
    {
        private readonly DispatcherTimer statusTimer;
        private bool updatingProfileSelection;
        private string profileSelectionKey;
        private bool updatingDeviceSelection;

        public Mk20BoxPlugin Plugin { get; }

        /// <summary>Backs the device layout editor for the active profile.</summary>
        public Mk20Box.Ui.DeviceLayoutViewModel Layout { get; private set; }

        public Mk20BoxSettingsControl()
        {
            InitializeComponent();
        }

        public Mk20BoxSettingsControl(Mk20BoxPlugin plugin) : this()
        {
            Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            DataContext = Plugin.Settings;

            statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            };
            statusTimer.Tick += StatusTimer_Tick;
            statusTimer.Start();

            Unloaded += SettingsControl_Unloaded;
            AutoConnectCheckBox.IsChecked = Plugin.Settings.AutoConnect;
            AutoUploadCheckBox.IsChecked = Plugin.Settings.AutoUploadProfile;
            InputDelayTextBox.Text = Plugin.Settings.InputDelayMs.ToString();
            TitleColorCombo.ItemsSource = Mk20Box.Layout.KeyTitleDefaults.Colors;
            TitleSizeCombo.ItemsSource = Mk20Box.Layout.KeyTitleDefaults.FontSizes;
            TitlePositionCombo.ItemsSource = Mk20Box.Layout.KeyTitleDefaults.Positions;
            RefreshProfileChoices();
            LoadLayoutForActiveProfile();
            Plugin.Device.StateChanged += Device_StateChanged;
            RefreshPorts();
            UpdateStatus();
        }

        private void ToggleDeviceMenu_Click(object sender, RoutedEventArgs e)
        {
            DeviceMenuPanel.Visibility = DeviceMenuPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void RefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
        }

        /// <summary>Lists present ports, keeping the saved one visible even if unplugged.</summary>
        private void RefreshPorts()
        {
            string savedPort = Plugin.Settings.DevicePortName;
            var ports = Mk20DeviceConnection.AvailablePorts().ToList();

            if (!string.IsNullOrWhiteSpace(savedPort)
                && !ports.Contains(savedPort, StringComparer.OrdinalIgnoreCase))
            {
                ports.Add(savedPort);
            }

            updatingDeviceSelection = true;
            try
            {
                DevicePortCombo.ItemsSource = ports;
                DevicePortCombo.SelectedItem = ports
                    .FirstOrDefault(port => string.Equals(port, savedPort, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                updatingDeviceSelection = false;
            }
        }

        private void DevicePortChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingDeviceSelection)
            {
                return;
            }

            Plugin.Settings.DevicePortName = DevicePortCombo.SelectedItem as string;
            Plugin.SaveSettings();
        }

        private void AutoConnect_Click(object sender, RoutedEventArgs e)
        {
            Plugin.Settings.AutoConnect = AutoConnectCheckBox.IsChecked == true;
            Plugin.SaveSettings();
        }

        private void AutoUpload_Click(object sender, RoutedEventArgs e)
        {
            Plugin.Settings.AutoUploadProfile = AutoUploadCheckBox.IsChecked == true;
            Plugin.SaveSettings();
        }

        /// <summary>Applies the delay live, so it can be tuned without a restart.</summary>
        private void InputDelay_TextChanged(object sender, TextChangedEventArgs e)
        {
            int delay;
            if (!int.TryParse(InputDelayTextBox.Text, out delay) || delay < 0)
            {
                return;
            }

            Plugin.Settings.InputDelayMs = delay;

            if (Plugin.Router.Bridge != null)
            {
                Plugin.Router.Bridge.InputDelayMs = delay;
            }

            Plugin.SaveSettings();
        }

        private async void ConnectDevice_Click(object sender, RoutedEventArgs e)
        {
            string portName = DevicePortCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(portName))
            {
                MessageBox.Show(
                    "Select the COM port the MK20 is connected to.",
                    "MK20Box",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            ConnectButton.IsEnabled = false;
            try
            {
                await Plugin.Device.ConnectAsync(portName);
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }

        private async void DisconnectDevice_Click(object sender, RoutedEventArgs e)
        {
            await Plugin.Device.DisconnectAsync();
        }

        /// <summary>Device events arrive off the UI thread.</summary>
        private void Device_StateChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(UpdateDeviceStatus));
        }

        private void UpdateDeviceStatus()
        {
            DeviceStatusText.Text = Plugin.Device.Status;
            ConnectButton.IsEnabled = !Plugin.Device.IsConnected;
            DisconnectButton.IsEnabled = Plugin.Device.IsConnected;
        }

        private void ToggleProfilesMenu_Click(object sender, RoutedEventArgs e)
        {
            ProfilesMenuPanel.Visibility = ProfilesMenuPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ToggleLayoutMenu_Click(object sender, RoutedEventArgs e)
        {
            LayoutMenuPanel.Visibility = LayoutMenuPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        /// <summary>The layout editor follows whichever profile is selected.</summary>
        private void LoadLayoutForActiveProfile()
        {
            // Always the profile that is actually in use, so the editor and the device
            // can never drift apart.
            Mk20ProfileSettings profile = Plugin.ActiveProfile;

            if (profile == null)
            {
                EditingProfileText.Text = string.Empty;
                return;
            }

            if (profile.Layout == null)
            {
                profile.Layout = new Mk20Box.Layout.Mk20LayoutSettings();
            }

            EditingProfileText.Text = " \u2014 " + DescribeEditedProfile(profile);

            Layout = new Mk20Box.Ui.DeviceLayoutViewModel(profile.Layout);
            Layout.Changed += (s, e) => Plugin.SaveSettings();
            LayoutMenuPanel.DataContext = Layout;
        }

        /// <summary>
        /// Names the profile being edited, plus what uses it, so it is obvious which
        /// game the keys on screen belong to.
        /// </summary>
        private string DescribeEditedProfile(Mk20ProfileSettings profile)
        {
            string name = string.IsNullOrWhiteSpace(profile.Name) ? "Untitled" : profile.Name.Trim();

            if (string.Equals(profile.Id, Plugin.Settings.GlobalProfileId, StringComparison.Ordinal))
            {
                return name + " (default)";
            }

            string[] games = Plugin.Settings.GameProfiles
                .Where(binding => binding != null
                    && string.Equals(binding.ProfileId, profile.Id, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(binding.GameName))
                .Select(binding => binding.GameName.Trim())
                .ToArray();

            return games.Length == 0 ? name : name + " (" + string.Join(", ", games) + ")";
        }

        /// <summary>Browses the bundled library, falling back to a file dialog.</summary>
        private void ChooseKeyMedia_Click(object sender, RoutedEventArgs e)
        {
            Mk20Box.Ui.DeviceKeyViewModel key = Layout.SelectedKey;
            if (key == null)
            {
                return;
            }

            var picker = new Mk20Box.Ui.IconPickerWindow
            {
                Owner = Window.GetWindow(this),
            };

            if (picker.ShowDialog() == true && picker.SelectedIconPath != null)
            {
                key.MediaPath = picker.SelectedIconPath;
                Plugin.SaveSettings();
            }
        }

        private void BrowseKeyMedia_Click(object sender, RoutedEventArgs e)
        {
            Mk20Box.Ui.DeviceKeyViewModel key = Layout.SelectedKey;
            if (key == null)
            {
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose an icon for " + key.Name,
                Filter = "Icons and GIFs|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
                CheckFileExists = true,
                InitialDirectory = Mk20Box.Layout.Mk20Assets.DefaultBrowseFolder,
            };

            if (dialog.ShowDialog() == true)
            {
                key.MediaPath = dialog.FileName;
                Plugin.SaveSettings();
            }
        }

        private void ClearKeyMedia_Click(object sender, RoutedEventArgs e)
        {
            if (Layout.SelectedKey != null)
            {
                Layout.SelectedKey.MediaPath = null;
            }
        }

        /// <summary>Builds the selected profile's theme and uploads it.</summary>
        private async void SendToDevice_Click(object sender, RoutedEventArgs e)
        {
            var profile = Plugin.ActiveProfile;

            if (profile == null)
            {
                return;
            }

            if (!Plugin.Device.IsConnected)
            {
                MessageBox.Show(
                    "Connect to the MK20 first.",
                    "MK20Box",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SendToDeviceButton.IsEnabled = false;
            try
            {
                await Plugin.SendProfileToDeviceAsync(profile);
            }
            finally
            {
                SendToDeviceButton.IsEnabled = true;
                UpdateDeviceStatus();
            }
        }

        private void SelectLeftEncoder_Click(object sender, RoutedEventArgs e)
        {
            Layout.SelectEncoder(Layout.SelectedPage?.LeftEncoder);
        }

        private void SelectRightEncoder_Click(object sender, RoutedEventArgs e)
        {
            Layout.SelectEncoder(Layout.SelectedPage?.RightEncoder);
        }

        private void BackToKeys_Click(object sender, RoutedEventArgs e)
        {
            Layout.SelectEncoder(null);
        }

        private void KeystrokeRecorded(object sender, RoutedEventArgs e)
        {
            Plugin.SaveSettings();
        }

        private void AddMacroKeystroke_Click(object sender, RoutedEventArgs e)
        {
            AddMacroStep(Mk20Box.Layout.MacroStepKinds.Keystroke);
        }

        private void AddMacroText_Click(object sender, RoutedEventArgs e)
        {
            AddMacroStep(Mk20Box.Layout.MacroStepKinds.Text);
        }

        private void AddMacroDelay_Click(object sender, RoutedEventArgs e)
        {
            AddMacroStep(Mk20Box.Layout.MacroStepKinds.Delay);
        }

        private void AddMacroAction_Click(object sender, RoutedEventArgs e)
        {
            AddMacroStep(Mk20Box.Layout.MacroStepKinds.SimHubAction);
        }

        private void AddMacroStep(string kind)
        {
            Mk20Box.Ui.DeviceKeyViewModel key = Layout.SelectedKey;
            if (key == null)
            {
                return;
            }

            Mk20Box.Ui.MacroStepViewModel step =
                key.AddMacroStep(new Mk20Box.Layout.Mk20MacroStepSettings { Kind = kind });

            // Select it so the editor below opens straight onto the new step.
            MacroStepList.SelectedItem = step;
            Plugin.SaveSettings();
        }

        private void RemoveMacroStep_Click(object sender, RoutedEventArgs e)
        {
            var step = MacroStepList.SelectedItem as Mk20Box.Ui.MacroStepViewModel;
            Layout.SelectedKey?.RemoveMacroStep(step);
            Plugin.SaveSettings();
        }

        private void MoveMacroStepUp_Click(object sender, RoutedEventArgs e)
        {
            MoveMacroStep(-1);
        }

        private void MoveMacroStepDown_Click(object sender, RoutedEventArgs e)
        {
            MoveMacroStep(1);
        }

        private void MoveMacroStep(int offset)
        {
            var step = MacroStepList.SelectedItem as Mk20Box.Ui.MacroStepViewModel;
            if (Layout.SelectedKey?.MoveMacroStep(step, offset) == true)
            {
                MacroStepList.SelectedItem = step;
                Plugin.SaveSettings();
            }
        }

        /// <summary>A recorded keystroke inside a macro step still needs persisting.</summary>
        private void MacroStepChanged(object sender, RoutedEventArgs e)
        {
            Plugin.SaveSettings();
        }

        /// <summary>Double-clicking a navigation key follows it, as the device would.</summary>
        private void KeyGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Layout.ActivateKey(Layout.SelectedKey))
            {
                e.Handled = true;
            }
        }

        private void CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewProfileNameTextBox.Text))
            {
                MessageBox.Show(
                    "Enter a profile name.",
                    "MK20Box",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            Mk20ProfileSettings profile = Plugin.AddProfile(NewProfileNameTextBox.Text);

            // New profiles belong to the game on screen, unless one profile is shared
            // by everything, in which case scoping it would hide it.
            if (!Plugin.Settings.UseGlobalProfile
                && !string.IsNullOrWhiteSpace(Plugin.ActiveGameName))
            {
                profile.GameName = Plugin.ActiveGameName;
                Plugin.SetProfileForGame(Plugin.ActiveGameName, profile.Id);
            }
            else
            {
                Plugin.Settings.GlobalProfileId = profile.Id;
                Plugin.SaveSettings();
            }

            NewProfileNameTextBox.Clear();
            profileSelectionKey = null;
            RefreshProfileChoices();
            LoadLayoutForActiveProfile();
            UpdateStatus();
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var profile = ActiveProfileCombo.SelectedItem as Mk20ProfileSettings;
            if (profile == null)
            {
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                "Delete profile \"" + profile.Name + "\"?",
                "MK20Box",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            if (!Plugin.DeleteProfile(profile))
            {
                MessageBox.Show(
                    "At least one profile must remain.",
                    "MK20Box",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // The deleted profile may have been the one in use, so let the next status
            // pass re-resolve rather than trusting the cached selection.
            profileSelectionKey = null;
            RefreshProfileChoices();
            LoadLayoutForActiveProfile();
            UpdateStatus();
        }

        /// <summary>
        /// Fills the profile picker with the profiles available for the game SimHub
        /// has selected, and selects the one in use.
        /// </summary>
        private void RefreshProfileChoices()
        {
            if (Plugin == null)
            {
                return;
            }

            bool global = Plugin.Settings.UseGlobalProfile;
            string game = Plugin.ActiveGameName;

            List<Mk20ProfileSettings> choices = global
                ? Plugin.Settings.Profiles.ToList()
                : Plugin.ProfilesForGame(game);

            Mk20ProfileSettings active = Plugin.ActiveProfile;

            // Only offer it here if it really belongs to this game; otherwise a
            // fallback to another game's profile would leak into this list.
            if (active != null && !choices.Contains(active)
                && (global || active.IsForGame(game)))
            {
                choices.Add(active);
            }

            ProfileForGameLabel.Text = global || string.IsNullOrWhiteSpace(game)
                ? "Profile"
                : "Profile for " + game;

            ProfileHintText.Text = global
                ? "Every game uses this profile. Untick above to give each game its own."
                : string.IsNullOrWhiteSpace(game)
                    ? "Select a game in SimHub to choose the profile it should use."
                    : "Showing profiles for " + game
                        + ". New profiles are added to this game; change the game in SimHub to edit another.";

            updatingProfileSelection = true;
            try
            {
                ActiveProfileCombo.ItemsSource = choices;
                ActiveProfileCombo.SelectedItem = active;
            }
            finally
            {
                updatingProfileSelection = false;
            }
        }

        /// <summary>Points the current game (or every game) at the chosen profile.</summary>
        private void ActiveProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Plugin == null || updatingProfileSelection)
            {
                return;
            }

            var profile = ActiveProfileCombo.SelectedItem as Mk20ProfileSettings;
            if (profile == null)
            {
                return;
            }

            if (Plugin.Settings.UseGlobalProfile || string.IsNullOrWhiteSpace(Plugin.ActiveGameName))
            {
                Plugin.Settings.GlobalProfileId = profile.Id;
                Plugin.SaveSettings();
            }
            else
            {
                Plugin.SetProfileForGame(Plugin.ActiveGameName, profile.Id);
            }

            LoadLayoutForActiveProfile();
            UpdateStatus();
        }

        /// <summary>Rebuilds the selected profile from scratch, after confirming.</summary>
        private void ResetProfile_Click(object sender, RoutedEventArgs e)
        {
            var profile = ActiveProfileCombo.SelectedItem as Mk20ProfileSettings;
            if (profile == null)
            {
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                "Reset \"" + profile.Name + "\" to default?\n\n"
                    + "Every page, folder, key and encoder in this profile will be cleared "
                    + "and the theme rebuilt from scratch. This cannot be undone.",
                "MK20Box",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            if (Plugin.ResetProfileLayout(profile))
            {
                LoadLayoutForActiveProfile();
                UpdateStatus();
            }
        }

        /// <summary>Title styling is persisted like any other key edit.</summary>
        private void KeyStyleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Plugin != null && Layout != null && Layout.SelectedKey != null)
            {
                Plugin.SaveSettings();
            }
        }

        /// <summary>
        /// Wipes all stored settings. Asked twice on purpose: the first prompt
        /// explains, the second requires typing so it cannot be dismissed by reflex.
        /// </summary>
        private void ResetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult first = MessageBox.Show(
                "Reset every MK20Box setting?\n\n"
                    + "This deletes all profiles, key layouts, macros, icons choices and "
                    + "game bindings, returning the plugin to a fresh install.\n\n"
                    + "This cannot be undone.",
                "MK20Box",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (first != MessageBoxResult.Yes)
            {
                return;
            }

            int profiles = Plugin.Settings.Profiles.Count;
            MessageBoxResult second = MessageBox.Show(
                "Last chance.\n\n"
                    + profiles + " profile(s) and every layout they contain will be "
                    + "permanently deleted.\n\nAre you absolutely sure?",
                "MK20Box - confirm reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop,
                MessageBoxResult.No);

            if (second != MessageBoxResult.Yes)
            {
                return;
            }

            Plugin.ResetAllSettings();

            DataContext = Plugin.Settings;
            UseGlobalProfileCheckBox.IsChecked = Plugin.Settings.UseGlobalProfile;
            AutoUploadCheckBox.IsChecked = Plugin.Settings.AutoUploadProfile;
            InputDelayTextBox.Text = Plugin.Settings.InputDelayMs.ToString();

            profileSelectionKey = null;
            RefreshProfileChoices();
            LoadLayoutForActiveProfile();
            UpdateStatus();

            MessageBox.Show(
                "MK20Box settings have been reset.",
                "MK20Box",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void GlobalProfileMode_Click(object sender, RoutedEventArgs e)
        {
            Plugin.Settings.UseGlobalProfile = UseGlobalProfileCheckBox.IsChecked == true;
            Plugin.SaveSettings();
            RefreshProfileChoices();
            LoadLayoutForActiveProfile();
            UpdateStatus();
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            UpdateStatus();
        }

        private void SettingsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            statusTimer?.Stop();
            if (Plugin != null)
            {
                Plugin.Device.StateChanged -= Device_StateChanged;
            }
        }

        private void UpdateStatus()
        {
            Plugin.RefreshSelectedGame();
            Plugin.RefreshActiveProfile();

            // Follow SimHub: when it switches game, show that game's profiles and load
            // the one it resolves to. Rebuilt only on change so the picker stays usable.
            Mk20ProfileSettings active = Plugin.ActiveProfile;
            string key = Plugin.ActiveGameName
                + "|" + (active == null ? string.Empty : active.Id)
                + "|" + Plugin.Settings.UseGlobalProfile;

            if (!string.Equals(key, profileSelectionKey, StringComparison.Ordinal))
            {
                profileSelectionKey = key;
                RefreshProfileChoices();
                LoadLayoutForActiveProfile();
            }

            ActiveGameText.Text = string.IsNullOrWhiteSpace(Plugin.ActiveGameName)
                ? "No game selected"
                : Plugin.ActiveGameName;
            ActiveProfileText.Text = Plugin.ActiveProfileName
                + (Plugin.ActiveProfileIsGlobal ? " (global)" : string.Empty);
            UpdateDeviceStatus();
        }
    }
}
