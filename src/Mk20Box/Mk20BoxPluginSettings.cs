using System.Collections.ObjectModel;

namespace Mk20Box
{
    /// <summary>
    /// Persisted plugin settings. Must remain JSON-serializable (JSON.NET).
    /// </summary>
    public partial class Mk20BoxPluginSettings
    {
        public bool UseGlobalProfile { get; set; } = true;

        public string GlobalProfileId { get; set; }

        public ObservableCollection<Mk20ProfileSettings> Profiles { get; set; }
            = new ObservableCollection<Mk20ProfileSettings>();

        public ObservableCollection<Mk20GameProfileBindingSettings> GameProfiles { get; set; }
            = new ObservableCollection<Mk20GameProfileBindingSettings>();

        // Kept for migration from the initial profile implementation.
        public string DefaultProfileName { get; set; } = "Default";
    }
}
