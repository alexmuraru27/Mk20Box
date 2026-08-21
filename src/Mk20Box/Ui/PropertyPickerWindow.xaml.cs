using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mk20Box.Layout;

namespace Mk20Box.Ui
{
    /// <summary>
    /// Searchable list of SimHub values. The common racing ones are offered first,
    /// then everything the running game exposes.
    /// </summary>
    public partial class PropertyPickerWindow : Window
    {
        private readonly List<Choice> choices = new List<Choice>();

        public PropertyPickerWindow(IEnumerable<string> livePropertyNames)
        {
            InitializeComponent();

            foreach (CommonTelemetry.Entry entry in CommonTelemetry.Entries)
            {
                choices.Add(new Choice(entry.Label, entry.Property, entry));
            }

            if (livePropertyNames != null)
            {
                var known = new HashSet<string>(
                    choices.Select(choice => choice.Property),
                    StringComparer.OrdinalIgnoreCase);

                foreach (string name in livePropertyNames
                    .Where(name => !string.IsNullOrWhiteSpace(name) && !known.Contains(name))
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
                {
                    choices.Add(new Choice(name, name, null));
                }
            }

            Refresh(string.Empty);
        }

        /// <summary>The value the user picked, or null when cancelled.</summary>
        public string SelectedProperty { get; private set; }

        /// <summary>Preset details, when a common value was chosen.</summary>
        public CommonTelemetry.Entry SelectedEntry { get; private set; }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            Refresh(SearchBox.Text);
        }

        private void Refresh(string search)
        {
            IEnumerable<Choice> matches = choices;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                matches = choices.Where(choice =>
                    choice.Label.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0
                    || choice.Property.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0);
            }

            // Long property lists make the window sluggish, so show the best matches.
            ResultList.ItemsSource = matches.Take(400).ToList();
            ResultList.SelectedIndex = 0;
        }

        private void Result_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            Ok_Click(sender, null);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var choice = ResultList.SelectedItem as Choice;
            if (choice == null)
            {
                return;
            }

            SelectedProperty = choice.Property;
            SelectedEntry = choice.Entry;
            DialogResult = true;
        }

        private sealed class Choice
        {
            public Choice(string label, string property, CommonTelemetry.Entry entry)
            {
                Label = label;
                Property = property;
                Entry = entry;
            }

            public string Label { get; private set; }

            public string Property { get; private set; }

            public CommonTelemetry.Entry Entry { get; private set; }

            public override string ToString()
            {
                return Entry == null ? Property : Label + "   \u2014   " + Property;
            }
        }
    }
}
