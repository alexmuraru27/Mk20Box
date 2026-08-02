using System.Windows.Controls;

namespace Mk20Box
{
    /// <summary>
    /// Minimal settings panel shown in SimHub's left menu.
    /// </summary>
    public partial class Mk20BoxSettingsControl : UserControl
    {
        public Mk20BoxPlugin Plugin { get; }

        public Mk20BoxSettingsControl()
        {
            InitializeComponent();
        }

        public Mk20BoxSettingsControl(Mk20BoxPlugin plugin) : this()
        {
            this.Plugin = plugin;
        }
    }
}
