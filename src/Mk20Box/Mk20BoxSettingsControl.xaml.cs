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

        /// <summary>Clicking any key brings the panel back to the key editor.</summary>
        private void KeyGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Layout != null)
            {
                Layout.SelectKeyInspector();
            }
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

            // A new profile is available to every game; it only becomes the one in
            // use for the game on screen.
            if (!Plugin.Settings.UseGlobalProfile
                && !string.IsNullOrWhiteSpace(Plugin.ActiveGameName))
            {
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

        /// <summary>
        /// Writes the selected profile, with its pictures, to a file others can import.
        /// </summary>
        private void ExportProfile_Click(object sender, RoutedEventArgs e)
        {
            var profile = ActiveProfileCombo.SelectedItem as Mk20ProfileSettings;
            if (profile == null)
            {
                MessageBox.Show(
                    "Select a profile to export.",
                    "MK20Box",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export profile",
                Filter = Mk20Box.Runtime.ProfileTransfer.FileFilter,
                FileName = Mk20Box.Runtime.ProfileTransfer.SuggestFileName(profile),
                DefaultExt = Mk20Box.Runtime.ProfileTransfer.FileExtension,
                AddExtension = true,
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            try
            {
                Mk20Box.Runtime.ProfileTransfer.Export(profile, dialog.FileName);
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    "Could not export the profile." + Environment.NewLine + Environment.NewLine + error.Message,
                    "MK20Box",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var exported = new System.IO.FileInfo(dialog.FileName);

            MessageBox.Show(
                "Exported \"" + profile.Name + "\" (" + (exported.Length / 1024) + " KB)."
                    + Environment.NewLine + Environment.NewLine
                    + "The file carries its pictures, so it works on another machine.",
                "MK20Box",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>Adds a profile from a shared file, alongside the existing ones.</summary>
        private void ImportProfile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import profile",
                Filter = Mk20Box.Runtime.ProfileTransfer.FileFilter,
                CheckFileExists = true,
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            Mk20ProfileSettings imported;

            try
            {
                imported = Mk20Box.Runtime.ProfileTransfer.Import(dialog.FileName, Plugin.Settings);
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    "Could not import the profile." + Environment.NewLine + Environment.NewLine + error.Message,
                    "MK20Box",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Plugin.SaveSettings();

            profileSelectionKey = null;
            RefreshProfileChoices();
            LoadLayoutForActiveProfile();
            UpdateStatus();

            MessageBox.Show(
                "Imported \"" + imported.Name + "\"."
                    + Environment.NewLine + Environment.NewLine
                    + "Select it above to use it, then press Send to device.",
                "MK20Box",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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

            // Artwork that came in with an imported profile is now orphaned.
            try
            {
                Mk20Box.Runtime.ProfileTransfer.RemoveUnusedMedia(Plugin.Settings);
            }
            catch (Exception)
            {
                // Tidying up is never worth failing a delete over.
            }

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

            // Every profile suits every game, so the same list is offered either way.
            List<Mk20ProfileSettings> choices = Plugin.Settings.Profiles.ToList();

            Mk20ProfileSettings active = Plugin.ActiveProfile;

            if (active != null && !choices.Contains(active))
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
                    : "Choosing the profile " + game + " will use. Any profile can be used by any game.";

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

        /// <summary>Picks the artwork that fills the secondary strip.</summary>
        private void ChooseSecondaryBackground_Click(object sender, RoutedEventArgs e)
        {
            Mk20Box.Ui.ThemePageViewModel page = Layout == null ? null : Layout.SecondaryPage;
            if (page == null)
            {
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose a secondary screen background",
                Filter = "Pictures and GIFs|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
                CheckFileExists = true,
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                page.SecondaryBackgroundPath = dialog.FileName;
                Plugin.SaveSettings();
            }
        }

        private void ClearSecondaryBackground_Click(object sender, RoutedEventArgs e)
        {
            Mk20Box.Ui.ThemePageViewModel page = Layout == null ? null : Layout.SecondaryPage;
            if (page == null)
            {
                return;
            }

            page.SecondaryBackgroundPath = null;
            page.SecondaryBackgroundOffsetX = 0;
            page.SecondaryBackgroundOffsetY = 0;
            Plugin.SaveSettings();
        }

        /// <summary>
        /// Dragging the strip pans the crop, moving the picture with the pointer. A
        /// full-width drag covers the whole range, so it feels like moving the photo.
        /// </summary>
        private void SecondaryBackground_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Layout != null)
            {
                Layout.SelectSecondaryScreen();
            }

            Mk20Box.Ui.ThemePageViewModel page = Layout == null ? null : Layout.SecondaryPage;
            if (page == null || !page.HasSecondaryBackground || page.SecondaryBackgroundFit)
            {
                return;
            }

            // Double-click re-centres, which is quicker than dragging back by eye.
            if (e.ClickCount >= 2)
            {
                CentreSecondaryBackground_Click(sender, null);
                e.Handled = true;
                return;
            }

            secondaryDragOrigin = e.GetPosition(SecondaryScreenSurface);
            secondaryDragging = true;
            SecondaryScreenSurface.CaptureMouse();
            SecondaryScreenSurface.Cursor = System.Windows.Input.Cursors.ScrollAll;
            e.Handled = true;
        }

        private void SecondaryBackground_MouseMove(object sender, MouseEventArgs e)
        {
            if (!secondaryDragging)
            {
                return;
            }

            Mk20Box.Ui.ThemePageViewModel page = Layout == null ? null : Layout.SecondaryPage;
            if (page == null)
            {
                return;
            }

            Point current = e.GetPosition(SecondaryScreenSurface);
            double width = SecondaryScreenSurface.ActualWidth;
            double height = SecondaryScreenSurface.ActualHeight;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            // Dragging right should reveal what is to the left, so the offset falls.
            page.PanSecondaryBackground(
                -(current.X - secondaryDragOrigin.X) / width,
                -(current.Y - secondaryDragOrigin.Y) / height);

            secondaryDragOrigin = current;
        }

        private void SecondaryBackground_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!secondaryDragging)
            {
                return;
            }

            secondaryDragging = false;
            SecondaryScreenSurface.ReleaseMouseCapture();
            SecondaryScreenSurface.Cursor = null;

            // Saved once at the end rather than on every mouse move.
            Plugin.SaveSettings();
        }

        /// <summary>
        /// Right-click selects the key under the pointer first, so the menu always
        /// acts on the key the user aimed at rather than the previous selection.
        /// </summary>
        private void KeyGrid_RightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;

            while (source != null && !(source is ListBoxItem))
            {
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            var item = source as ListBoxItem;
            if (item != null)
            {
                item.IsSelected = true;
            }
        }

        private void KeyIconFitMode_Click(object sender, RoutedEventArgs e)
        {
            if (Plugin != null && Layout != null && Layout.SelectedKey != null)
            {
                Plugin.SaveSettings();
            }
        }

        private void SecondaryFitMode_Click(object sender, RoutedEventArgs e)
        {
            if (Plugin != null && Layout != null && Layout.SecondaryPage != null)
            {
                Plugin.SaveSettings();
            }
        }

        /// <summary>
        /// Switches the strip between per-page and shared. The editor follows it, so
        /// what is on screen is what every page will get.
        /// </summary>
        private void GlobalSecondaryScreen_Click(object sender, RoutedEventArgs e)
        {
            if (Plugin == null || Layout == null)
            {
                return;
            }

            WidgetList.SelectedItem = null;
            Plugin.SaveSettings();
        }

        private void CentreSecondaryBackground_Click(object sender, RoutedEventArgs e)
        {
            Mk20Box.Ui.ThemePageViewModel page = Layout == null ? null : Layout.SecondaryPage;
            if (page == null || !page.HasSecondaryBackground)
            {
                return;
            }

            page.SecondaryBackgroundOffsetX = 0;
            page.SecondaryBackgroundOffsetY = 0;
            Plugin.SaveSettings();
        }

        private bool secondaryDragging;
        private Point secondaryDragOrigin;

        /// <summary>Adds a widget to the page on screen and selects it for editing.</summary>
        private void AddWidget_Click(object sender, RoutedEventArgs e)
        {
            Mk20Box.Ui.ThemePageViewModel page = Layout == null ? null : Layout.SecondaryPage;
            if (page == null)
            {
                return;
            }

            // Stacked slightly so a second widget does not land on the first.
            int existing = page.Widgets.Count;

            var widget = new Mk20Box.Layout.Mk20TextWidget
            {
                X = 20,
                Y = 16 + ((existing % 4) * 30),
                Text = "text",
            };

            Mk20Box.Ui.WidgetViewModel added = page.AddWidget(widget);
            WidgetList.SelectedItem = added;
            WidgetList.ScrollIntoView(added);
            Plugin.SaveSettings();
        }

        private void RemoveWidget_Click(object sender, RoutedEventArgs e)
        {
            Mk20Box.Ui.ThemePageViewModel page = Layout == null ? null : Layout.SecondaryPage;
            var widget = WidgetList.SelectedItem as Mk20Box.Ui.WidgetViewModel;

            if (page == null || widget == null)
            {
                MessageBox.Show(
                    "Select a widget from the list first.",
                    "MK20Box",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            page.RemoveWidget(widget);
            Plugin.SaveSettings();
        }

        private void WidgetSelected(object sender, SelectionChangedEventArgs e)
        {
            // The type list is filled once the first widget is shown.
            if (WidgetKindCombo != null && WidgetKindCombo.ItemsSource == null)
            {
                WidgetKindCombo.ItemsSource = Mk20Box.Layout.WidgetKinds.All;
            }

            HighlightSelectedWidget();
        }

        /// <summary>Marks the edited widget on the screen preview and clears the rest.</summary>
        private void HighlightSelectedWidget()
        {
            if (Layout == null)
            {
                return;
            }

            var chosen = WidgetList == null ? null : WidgetList.SelectedItem as Mk20Box.Ui.WidgetViewModel;

            // Every page, so a highlight cannot linger on one left behind.
            foreach (Mk20Box.Ui.ThemePageViewModel page in Layout.Pages)
            {
                foreach (Mk20Box.Ui.WidgetViewModel widget in page.Widgets)
                {
                    widget.IsSelected = ReferenceEquals(widget, chosen);
                }
            }
        }

        private void WidgetColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            WidgetFieldChanged(sender, e);
        }

        private void KeyColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (Plugin != null && Layout != null && Layout.SelectedKey != null)
            {
                Plugin.SaveSettings();
            }
        }

        private void WidgetFieldChanged(object sender, RoutedEventArgs e)
        {
            if (Plugin != null && WidgetList != null && WidgetList.SelectedItem != null)
            {
                Plugin.SaveSettings();
            }
        }

        private void WidgetChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Plugin != null && WidgetList != null && WidgetList.SelectedItem != null)
            {
                Plugin.SaveSettings();
            }
        }

        /// <summary>Picks the SimHub value this widget streams.</summary>
        private void ChooseProperty_Click(object sender, RoutedEventArgs e)
        {
            var widget = WidgetList.SelectedItem as Mk20Box.Ui.WidgetViewModel;
            if (widget == null)
            {
                return;
            }

            var picker = new Mk20Box.Ui.PropertyPickerWindow(Plugin.AvailableProperties())
            {
                Owner = Window.GetWindow(this),
            };

            if (picker.ShowDialog() == true && picker.SelectedProperty != null)
            {
                if (picker.SelectedEntry != null)
                {
                    // A known value brings its unit, decimals and a sensible range.
                    widget.Apply(picker.SelectedEntry);
                }
                else
                {
                    widget.Property = picker.SelectedProperty;
                }

                Plugin.SaveSettings();
            }
        }

        private void ClearProperty_Click(object sender, RoutedEventArgs e)
        {
            var widget = WidgetList.SelectedItem as Mk20Box.Ui.WidgetViewModel;
            if (widget != null)
            {
                widget.Property = null;
                Plugin.SaveSettings();
            }
        }

        /// <summary>
        /// Widgets are dragged straight on the screen preview. The canvas is the real
        /// 428 x 142, so pointer positions are already in device coordinates.
        /// </summary>
        private void Widget_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            var widget = element == null ? null : element.DataContext as Mk20Box.Ui.WidgetViewModel;

            if (widget == null)
            {
                return;
            }

            if (Layout != null)
            {
                Layout.SelectSecondaryScreen();
            }

            WidgetList.SelectedItem = widget;

            draggedWidget = widget;
            widgetDragOffset = e.GetPosition(element);
            element.CaptureMouse();
            element.Cursor = System.Windows.Input.Cursors.SizeAll;
            e.Handled = true;
        }

        private void Widget_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedWidget == null)
            {
                return;
            }

            Point position = e.GetPosition(WidgetOverlay);
            draggedWidget.MoveTo(
                position.X - widgetDragOffset.X,
                position.Y - widgetDragOffset.Y);
        }

        private void Widget_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (draggedWidget == null)
            {
                return;
            }

            var element = sender as FrameworkElement;
            if (element != null)
            {
                element.ReleaseMouseCapture();
                element.Cursor = null;
            }

            draggedWidget = null;

            // Saved once at the end rather than on every mouse move.
            Plugin.SaveSettings();
        }

        private Mk20Box.Ui.WidgetViewModel draggedWidget;
        private Point widgetDragOffset;

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
