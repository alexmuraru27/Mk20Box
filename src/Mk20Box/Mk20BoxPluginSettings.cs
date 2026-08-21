using System.Collections.ObjectModel;

namespace Mk20Box
{
    /// <summary>
    /// Persisted plugin settings. Must remain JSON-serializable (JSON.NET).
    /// </summary>
    public partial class Mk20BoxPluginSettings
    {
        /// <summary>Serial port the MK20 is attached to, e.g. "COM7".</summary>
        public string DevicePortName { get; set; }

        /// <summary>Connect to the device automatically when SimHub starts.</summary>
        public bool AutoConnect { get; set; } = true;

        /// <summary>
        /// Pause between emitted keystrokes and typed characters. Games that poll the
        /// keyboard miss input sent faster than their poll rate; raise this if a macro
        /// loses or reorders characters.
        /// </summary>
        public int InputDelayMs { get; set; } = Mk20Box.Runtime.SimHubBridge.DefaultInputDelayMs;

        /// <summary>
        /// Upload the resolved profile to the device whenever it changes, for example
        /// when a game starts. Turn off to keep uploads manual.
        /// </summary>
        public bool AutoUploadProfile { get; set; } = true;

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
