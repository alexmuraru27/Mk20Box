using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Mk20Box.Ui
{
    /// <summary>
    /// Visual browser for the bundled icon library. Far easier than a file dialog
    /// when there are several hundred icons spread across categories.
    /// </summary>
    public partial class IconPickerWindow : Window
    {
        private readonly IReadOnlyList<IconEntry> icons;

        public IconPickerWindow()
        {
            InitializeComponent();

            icons = IconLibrary.Load();
            CategoryList.ItemsSource = IconLibrary.Categories(icons);
            CategoryList.SelectedIndex = 0;
            ApplyFilter();
        }

        /// <summary>Full path of the chosen icon, or null when cancelled.</summary>
        public string SelectedIconPath { get; private set; }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (IconList == null)
            {
                return;
            }

            string category = CategoryList.SelectedItem as string;
            string search = SearchBox?.Text?.Trim();

            IEnumerable<IconEntry> filtered = icons;

            if (!string.IsNullOrEmpty(category) && category != IconLibrary.AllCategories)
            {
                filtered = filtered.Where(icon => icon.Category == category);
            }

            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(icon =>
                    icon.Name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var results = filtered.ToList();
            IconList.ItemsSource = results;
            CountText.Text = results.Count == 1
                ? "1 icon"
                : results.Count.ToString() + " icons";
        }

        private void IconList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Choose();
        }

        private void Choose_Click(object sender, RoutedEventArgs e)
        {
            Choose();
        }

        private void Choose()
        {
            var selected = IconList.SelectedItem as IconEntry;
            if (selected == null)
            {
                return;
            }

            SelectedIconPath = selected.Path;
            DialogResult = true;
        }
    }
}
