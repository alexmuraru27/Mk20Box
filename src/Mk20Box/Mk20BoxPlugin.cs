using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Windows.Media;

namespace Mk20Box
{
    [PluginDescription("SimHub button box plugin for Waveshare MK20")]
    [PluginAuthor("alexmuraru27")]
    [PluginName("MK20Box")]
    public class Mk20BoxPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        public Mk20BoxPluginSettings Settings;

        /// <summary>Current plugin manager instance (set by SimHub).</summary>
        public PluginManager PluginManager { get; set; }

        /// <summary>Left-menu icon (24x24, black/white friendly).</summary>
        public ImageSource PictureIcon => this.ToIcon(Properties.Resources.sdkmenuicon);

        /// <summary>Short title shown in SimHub's left menu.</summary>
        public string LeftMenuTitle => "MK20Box";

        /// <summary>Called once after plugin startup.</summary>
        public void Init(PluginManager pluginManager)
        {
            SimHub.Logging.Current.Info("[MK20Box] Plugin starting");

            Settings = this.ReadCommonSettings<Mk20BoxPluginSettings>("GeneralSettings", () => new Mk20BoxPluginSettings());

            // Simple heartbeat property so you can confirm the plugin is alive:
            // in SimHub, open the property list and search for "MK20Box.Status".
            this.AttachDelegate("Status", () => "loaded");
        }

        /// <summary>
        /// Called once per game-data tick. Intentionally empty for now —
        /// telemetry-to-device rendering will live here.
        /// </summary>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
        }

        /// <summary>Called at plugin stop; persist settings here.</summary>
        public void End(PluginManager pluginManager)
        {
            this.SaveCommonSettings("GeneralSettings", Settings);
        }

        /// <summary>Settings UI shown in SimHub's left menu.</summary>
        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new Mk20BoxSettingsControl(this);
        }
    }
}
