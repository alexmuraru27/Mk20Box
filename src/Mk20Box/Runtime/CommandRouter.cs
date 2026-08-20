using System;
using System.Collections.Generic;
using System.Linq;
using Mk20Box.Layout;
using Mk20Control.Protocol.Client;
using Mk20Control.Protocol.Host;
using Mk20Control.Protocol.Theme.Actions;

namespace Mk20Box.Runtime
{
    /// <summary>
    /// Routes key presses reported by the device to whatever the active profile says
    /// they should do. Only host-routed keys arrive here: keystrokes and navigation
    /// are performed by the device itself and never reported with an id.
    /// </summary>
    public sealed class CommandRouter : IDisposable
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, Mk20KeySettings> keysByCommandId =
            new Dictionary<string, Mk20KeySettings>(StringComparer.OrdinalIgnoreCase);

        private KeyBindings bindings;

        /// <summary>Everything the router can ask SimHub or the host to do.</summary>
        public SimHubBridge Bridge { get; set; }

        /// <summary>Starts listening on a freshly connected device.</summary>
        public void Attach(Mk20DeviceClient client)
        {
            Detach();

            if (client == null)
            {
                return;
            }

            lock (sync)
            {
                bindings = new KeyBindings(client);
                bindings.Unbound += OnKeyEvent;
            }
        }

        public void Detach()
        {
            lock (sync)
            {
                if (bindings == null)
                {
                    return;
                }

                bindings.Unbound -= OnKeyEvent;
                bindings.Dispose();
                bindings = null;
            }
        }

        /// <summary>Indexes the layout so a reported id can be resolved to its key.</summary>
        public void SetActiveLayout(Mk20LayoutSettings layout)
        {
            var inputNames = new List<string>();

            lock (sync)
            {
                keysByCommandId.Clear();

                if (layout == null)
                {
                    return;
                }

                foreach (Mk20KeySettings key in layout.Pages.SelectMany(page => page.Keys))
                {
                    if (!string.IsNullOrEmpty(key.CommandId))
                    {
                        keysByCommandId[key.CommandId] = key;
                    }

                    if (key.ActionType == KeyActionKinds.SimHubInput
                        && !string.IsNullOrWhiteSpace(key.ActionTarget))
                    {
                        inputNames.Add(key.ActionTarget);
                    }
                }
            }

            // Outside the lock: registration calls into SimHub.
            SimHubBridge bridge = Bridge;
            if (bridge != null)
            {
                foreach (string inputName in inputNames)
                {
                    bridge.RegisterInput(inputName);
                }
            }
        }

        private void OnKeyEvent(object sender, KeyEventContext context)
        {
            if (!context.IsPressed)
            {
                return;
            }

            string commandId = context.CommandId
                ?? (context.Action as TextInputAction)?.InputText;

            if (string.IsNullOrEmpty(commandId))
            {
                return;
            }

            Mk20KeySettings key;
            lock (sync)
            {
                if (!keysByCommandId.TryGetValue(commandId, out key))
                {
                    // Usually means the indexed layout is not the one on the device.
                    SimHub.Logging.Current.Info(
                        $"[MK20Box] Key '{commandId}' is not in the active profile");
                    return;
                }
            }

            Run(key);
        }

        private void Run(Mk20KeySettings key)
        {
            SimHubBridge bridge = Bridge;
            if (bridge == null)
            {
                return;
            }

            switch (key.ActionType)
            {
                case KeyActionKinds.Macro:
                    // Fire and forget: a macro may contain waits.
                    System.Threading.Tasks.Task.Run(() =>
                        MacroRunner.RunAsync(key.MacroSteps, bridge));
                    break;

                case KeyActionKinds.SimHubAction:
                    bridge.TriggerAction(key.ActionTarget);
                    break;

                case KeyActionKinds.SimHubInput:
                    bridge.TriggerInput(key.ActionTarget);
                    break;
            }
        }

        public void Dispose()
        {
            Detach();
        }
    }
}
