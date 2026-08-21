using System.Windows;

namespace Mk20Box.Ui
{
    /// <summary>
    /// Asks for a single line of text. Used for naming a profile, where a full
    /// dialog would be overkill and WPF has no prompt of its own.
    /// </summary>
    public partial class NamePromptWindow : Window
    {
        public NamePromptWindow(string title, string prompt, string initialValue)
        {
            InitializeComponent();

            Title = title;
            PromptText.Text = prompt;
            NameBox.Text = initialValue ?? string.Empty;

            // Selected, so typing replaces the suggestion but keeps it available.
            Loaded += (sender, args) =>
            {
                NameBox.SelectAll();
                NameBox.Focus();
            };
        }

        /// <summary>The trimmed text entered, once the dialog is accepted.</summary>
        public string EnteredName { get; private set; }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string entered = (NameBox.Text ?? string.Empty).Trim();
            if (entered.Length == 0)
            {
                return;
            }

            EnteredName = entered;
            DialogResult = true;
        }
    }
}
