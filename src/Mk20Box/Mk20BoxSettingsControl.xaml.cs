using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Mk20Box
{
    public partial class Mk20BoxSettingsControl : UserControl
    {
        private readonly DispatcherTimer statusTimer;
        private bool updatingProfileSelection;

        public Mk20BoxPlugin Plugin { get; }

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
            SupportedGamesCombo.ItemsSource = Plugin.SupportedGames;
            SupportedGamesCombo.SelectedItem = Plugin.SupportedGames
                .FirstOrDefault(gameName => string.Equals(
                    gameName,
                    Plugin.ActiveGameName,
                    StringComparison.OrdinalIgnoreCase))
                ?? Plugin.SupportedGames.FirstOrDefault();
            ProfileLibraryCombo.SelectedItem = Plugin.Settings.FindProfileById(Plugin.Settings.GlobalProfileId);
            UpdateStatus();
        }

        private void ToggleProfilesMenu_Click(object sender, RoutedEventArgs e)
        {
            ProfilesMenuPanel.Visibility = ProfilesMenuPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
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
            ProfileLibraryCombo.SelectedItem = profile;
            NewProfileNameTextBox.Clear();
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var profile = ProfileLibraryCombo.SelectedItem as Mk20ProfileSettings;
            if (profile == null)
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

            ProfileLibraryCombo.SelectedItem = Plugin.Settings.FindProfileById(Plugin.Settings.GlobalProfileId);
            UpdateStatus();
        }

        private void AddGame_Click(object sender, RoutedEventArgs e)
        {
            string gameName = SupportedGamesCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(gameName))
            {
                MessageBox.Show(
                    "Select a supported game from the dropdown.",
                    "MK20Box",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            Mk20GameProfileBindingSettings binding = Plugin.AddGameProfile(gameName);
            GameBindingsList.SelectedItem = binding;
            GameBindingsList.ScrollIntoView(binding);
            UpdateStatus();
        }

        private void RemoveGame_Click(object sender, RoutedEventArgs e)
        {
            var binding = GameBindingsList.SelectedItem as Mk20GameProfileBindingSettings;
            if (binding == null)
            {
                return;
            }

            Plugin.RemoveGameProfile(binding);
            UpdateStatus();
        }

        private void GlobalProfileMode_Click(object sender, RoutedEventArgs e)
        {
            Plugin.Settings.UseGlobalProfile = UseGlobalProfileCheckBox.IsChecked == true;
            Plugin.SaveSettings();
            UpdateStatus();
        }

        private void ProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Plugin == null || updatingProfileSelection)
            {
                return;
            }

            var comboBox = sender as ComboBox;
            string profileId = comboBox?.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return;
            }

            if (ReferenceEquals(comboBox, GlobalProfileCombo))
            {
                Plugin.Settings.GlobalProfileId = profileId;
            }
            else if (ReferenceEquals(comboBox, GameProfileCombo))
            {
                var binding = GameBindingsList.SelectedItem as Mk20GameProfileBindingSettings;
                if (binding == null)
                {
                    return;
                }

                binding.ProfileId = profileId;
            }

            updatingProfileSelection = true;
            try
            {
                ProfileLibraryCombo.SelectedItem = Plugin.Settings.FindProfileById(profileId);
                Plugin.SaveSettings();
                UpdateStatus();
            }
            finally
            {
                updatingProfileSelection = false;
            }
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            UpdateStatus();
        }

        private void SettingsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            statusTimer?.Stop();
        }

        private void UpdateStatus()
        {
            Plugin.RefreshSelectedGame();
            Plugin.RefreshActiveProfile();
            ActiveGameText.Text = string.IsNullOrWhiteSpace(Plugin.ActiveGameName)
                ? "No game selected"
                : Plugin.ActiveGameName;
            ActiveProfileText.Text = Plugin.ActiveProfileName
                + (Plugin.ActiveProfileIsGlobal ? " (global)" : string.Empty);
        }
    }
}
