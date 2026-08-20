using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mk20Box.Layout;

namespace Mk20Box.Ui
{
    /// <summary>Where navigation came from, so Back can retrace it.</summary>
    internal sealed class NavigationStep
    {
        public ThemePageViewModel Page { get; set; }

        /// <summary>The key used to descend, which is how the folder is identified.</summary>
        public DeviceKeyViewModel Key { get; set; }
    }

    /// <summary>
    /// Backs the device layout editor. Pages and folders are never named: top-level
    /// pages are numbered and a folder is known by the key that opens it, which is
    /// exactly how the device itself works.
    /// </summary>
    public sealed class DeviceLayoutViewModel : ViewModelBase
    {
        private readonly Mk20LayoutSettings settings;
        private readonly Stack<NavigationStep> navigationStack = new Stack<NavigationStep>();

        private ThemePageViewModel selectedPage;
        private DeviceKeyViewModel selectedKey;
        private EncoderViewModel selectedEncoder;

        public DeviceLayoutViewModel(Mk20LayoutSettings settings)
        {
            this.settings = settings ?? new Mk20LayoutSettings();

            if (this.settings.Pages.Count == 0)
            {
                this.settings.Pages.Add(Mk20LayoutSettings.CreateEmptyPage(null));
            }

            Pages = new ObservableCollection<ThemePageViewModel>(
                this.settings.Pages.Select(page => new ThemePageViewModel(page)));

            ActionTypes = KeyActionKinds.All;
            KeyboardKeys = Enum.GetNames(typeof(Mk20Control.Protocol.Theme.Building.HidKey));

            selectedPage = Pages[0];
            selectedKey = selectedPage.Keys[0];

            // Watch every key, not just the selected one: the first key is selected
            // before any subscription would otherwise happen.
            foreach (ThemePageViewModel page in Pages)
            {
                WatchKeys(page);
            }

            PreviousPageCommand = new RelayCommand(() => StepPage(-1));
            NextPageCommand = new RelayCommand(() => StepPage(1));
            AddPageCommand = new RelayCommand(AddPage);
            DeletePageCommand = new RelayCommand(DeleteCurrentPage);
            GoBackCommand = new RelayCommand(() => GoBack());
        }

        /// <summary>Raised when the layout changes and should be persisted.</summary>
        public event EventHandler Changed;

        public ObservableCollection<ThemePageViewModel> Pages { get; }

        public IReadOnlyList<string> ActionTypes { get; }

        public IReadOnlyList<string> KeyboardKeys { get; }

        public RelayCommand PreviousPageCommand { get; }

        public RelayCommand NextPageCommand { get; }

        public RelayCommand AddPageCommand { get; }

        public RelayCommand DeletePageCommand { get; }

        public RelayCommand GoBackCommand { get; }

        /// <summary>Pages the previous/next keys cycle through.</summary>
        public IList<ThemePageViewModel> TopLevelPages
        {
            get { return Pages.Where(page => !page.IsFolder).ToList(); }
        }

        public bool CanGoBack => navigationStack.Count > 0;

        public bool IsInFolder => selectedPage != null && selectedPage.IsFolder;

        /// <summary>"Page 2 of 3", or simply "Folder" when inside one.</summary>
        public string PageIndicator
        {
            get
            {
                if (IsInFolder)
                {
                    return "Folder";
                }

                IList<ThemePageViewModel> ring = TopLevelPages;
                int position = ring.IndexOf(selectedPage) + 1;
                return string.Format("Page {0} of {1}", Math.Max(position, 1), Math.Max(ring.Count, 1));
            }
        }

        /// <summary>How the current screen was reached, using key labels for folders.</summary>
        public string BreadcrumbText
        {
            get
            {
                var parts = new List<string>();

                foreach (NavigationStep step in navigationStack.Reverse())
                {
                    parts.Add(DescribePage(step.Page));
                    parts.Add(DescribeKey(step.Key));
                }

                if (navigationStack.Count == 0)
                {
                    parts.Add(DescribePage(selectedPage));
                }

                return string.Join("  \u203A  ", parts);
            }
        }

        public ThemePageViewModel SelectedPage
        {
            get { return selectedPage; }
            set
            {
                if (value == null || !SetField(ref selectedPage, value))
                {
                    return;
                }

                SelectedKey = value.Keys[0];
                RaiseNavigationChanged();
            }
        }

        public DeviceKeyViewModel SelectedKey
        {
            get { return selectedKey; }
            set
            {
                if (SetField(ref selectedKey, value))
                {
                    if (selectedKey != null)
                    {
                        SelectedEncoder = null;
                    }

                    OnPropertyChanged(nameof(ShowsKeyInspector));
                }
            }
        }

        /// <summary>Keeps folder creation working no matter which key is edited.</summary>
        private void WatchKeys(ThemePageViewModel page)
        {
            foreach (DeviceKeyViewModel key in page.Keys)
            {
                key.PropertyChanged -= KeyPropertyChanged;
                key.PropertyChanged += KeyPropertyChanged;
            }
        }

        private void KeyPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var key = sender as DeviceKeyViewModel;

            if (e.PropertyName == nameof(DeviceKeyViewModel.ActionType))
            {
                EnsureFolderForKey(key);
                NotifyChanged();
            }
            else if (e.PropertyName == nameof(DeviceKeyViewModel.Title))
            {
                OnPropertyChanged(nameof(BreadcrumbText));
            }
        }

        public EncoderViewModel SelectedEncoder
        {
            get { return selectedEncoder; }
            set
            {
                if (SetField(ref selectedEncoder, value))
                {
                    OnPropertyChanged(nameof(ShowsKeyInspector));
                    OnPropertyChanged(nameof(ShowsEncoderInspector));
                }
            }
        }

        public bool ShowsKeyInspector => selectedEncoder == null;

        public bool ShowsEncoderInspector => selectedEncoder != null;

        public void SelectEncoder(EncoderViewModel encoder)
        {
            SelectedEncoder = encoder;
        }

        /// <summary>Runs a key the way the device would. Returns true when it navigated.</summary>
        public bool ActivateKey(DeviceKeyViewModel key)
        {
            if (key == null)
            {
                return false;
            }

            switch (key.ActionType)
            {
                case KeyActionKinds.OpenFolder:
                    return EnterFolder(key);

                case KeyActionKinds.OneLevelUp:
                    return GoBack();

                case KeyActionKinds.PreviousPage:
                    return StepPage(-1);

                case KeyActionKinds.NextPage:
                    return StepPage(1);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Gives a key its own folder the moment it is set to open one. The folder
        /// declares this page as its parent, which the device requires for the return
        /// key to work.
        /// </summary>
        public void EnsureFolderForKey(DeviceKeyViewModel key)
        {
            if (key == null
                || key.ActionType != KeyActionKinds.OpenFolder
                || !string.IsNullOrEmpty(key.TargetPageId))
            {
                return;
            }

            Mk20PageSettings model = Mk20LayoutSettings.CreateEmptyPage(selectedPage?.Id);
            settings.Pages.Add(model);

            var folder = new ThemePageViewModel(model);
            Pages.Add(folder);
            WatchKeys(folder);

            key.TargetPageId = model.Id;
            NotifyChanged();
        }

        private bool EnterFolder(DeviceKeyViewModel key)
        {
            ThemePageViewModel target = Pages.FirstOrDefault(page => page.Id == key.TargetPageId);
            if (target == null || ReferenceEquals(target, selectedPage))
            {
                return false;
            }

            navigationStack.Push(new NavigationStep { Page = selectedPage, Key = key });
            SelectedPage = target;
            return true;
        }

        private bool GoBack()
        {
            if (navigationStack.Count == 0)
            {
                return false;
            }

            NavigationStep step = navigationStack.Pop();
            SelectedPage = step.Page;
            return true;
        }

        private void AddPage()
        {
            Mk20PageSettings model = Mk20LayoutSettings.CreateEmptyPage(null);
            settings.Pages.Add(model);

            var page = new ThemePageViewModel(model);
            Pages.Add(page);
            WatchKeys(page);

            navigationStack.Clear();
            SelectedPage = page;
            NotifyChanged();
        }

        /// <summary>Removes the current page along with every folder hanging off it.</summary>
        private void DeleteCurrentPage()
        {
            if (selectedPage == null || selectedPage.IsFolder || TopLevelPages.Count <= 1)
            {
                return;
            }

            ThemePageViewModel doomed = selectedPage;
            List<ThemePageViewModel> removed = Pages.Where(page => IsDescendantOf(page, doomed)).ToList();
            removed.Add(doomed);

            foreach (ThemePageViewModel page in removed)
            {
                Pages.Remove(page);
                settings.Pages.Remove(page.Model);
            }

            UnbindKeysTargeting(removed);

            navigationStack.Clear();
            SelectedPage = TopLevelPages.First();
            NotifyChanged();
        }

        private void UnbindKeysTargeting(ICollection<ThemePageViewModel> removed)
        {
            var removedIds = new HashSet<string>(removed.Select(page => page.Id));

            foreach (DeviceKeyViewModel key in Pages.SelectMany(page => page.Keys))
            {
                if (key.TargetPageId != null && removedIds.Contains(key.TargetPageId))
                {
                    key.ActionType = KeyActionKinds.Unassigned;
                }
            }
        }

        private bool IsDescendantOf(ThemePageViewModel page, ThemePageViewModel ancestor)
        {
            string parentId = page.ParentPageId;

            while (!string.IsNullOrEmpty(parentId))
            {
                if (parentId == ancestor.Id)
                {
                    return true;
                }

                ThemePageViewModel parent = Pages.FirstOrDefault(candidate => candidate.Id == parentId);
                if (parent == null)
                {
                    return false;
                }

                parentId = parent.ParentPageId;
            }

            return false;
        }

        /// <summary>
        /// Top-level pages form a ring, as on the device. Paging does nothing inside a
        /// folder, which is also how the hardware behaves.
        /// </summary>
        private bool StepPage(int offset)
        {
            if (selectedPage == null || selectedPage.IsFolder)
            {
                return false;
            }

            IList<ThemePageViewModel> ring = TopLevelPages;
            int current = ring.IndexOf(selectedPage);

            if (current < 0 || ring.Count < 2)
            {
                return false;
            }

            int target = ((current + offset) % ring.Count + ring.Count) % ring.Count;
            SelectedPage = ring[target];
            return true;
        }

        private string DescribePage(ThemePageViewModel page)
        {
            if (page == null)
            {
                return string.Empty;
            }

            int position = TopLevelPages.IndexOf(page) + 1;
            return position > 0 ? "Page " + position.ToString() : "Folder";
        }

        /// <summary>A folder is known by the label on the key that opens it.</summary>
        private static string DescribeKey(DeviceKeyViewModel key)
        {
            if (key == null)
            {
                return "Folder";
            }

            return string.IsNullOrWhiteSpace(key.Title) ? key.Label : key.Title;
        }

        private void RaiseNavigationChanged()
        {
            OnPropertyChanged(nameof(BreadcrumbText));
            OnPropertyChanged(nameof(PageIndicator));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(IsInFolder));
        }

        private void NotifyChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
