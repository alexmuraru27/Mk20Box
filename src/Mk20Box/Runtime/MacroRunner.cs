using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mk20Box.Layout;

namespace Mk20Box.Runtime
{
    /// <summary>
    /// Replays macro steps in order. The device only reports the press for these keys,
    /// so all the work happens on the host. Every effect goes through the bridge.
    /// </summary>
    public static class MacroRunner
    {
        public static async Task RunAsync(
            IEnumerable<Mk20MacroStepSettings> steps,
            SimHubBridge bridge)
        {
            if (steps == null || bridge == null)
            {
                return;
            }

            foreach (Mk20MacroStepSettings step in steps)
            {
                try
                {
                    await RunStepAsync(step, bridge).ConfigureAwait(false);

                    // Let the target application consume the events before the next
                    // step; without this, fast sequences can be dropped or reordered.
                    await Task.Delay(StepGapMs).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    SimHub.Logging.Current.Warn(
                        $"[MK20Box] Macro step '{step.Describe()}' failed: {ex.Message}");
                }
            }
        }

        /// <summary>Breathing room between steps so nothing is swallowed.</summary>
        private const int StepGapMs = 15;

        private static async Task RunStepAsync(Mk20MacroStepSettings step, SimHubBridge bridge)
        {
            switch (step.Kind)
            {
                case MacroStepKinds.Keystroke:
                    bridge.SendKeystroke(step.Keystroke);
                    break;

                case MacroStepKinds.Text:
                    bridge.TypeText(step.Text);
                    break;

                case MacroStepKinds.Delay:
                    await Task.Delay(Math.Max(0, step.DelayMs)).ConfigureAwait(false);
                    break;

                case MacroStepKinds.SimHubAction:
                    bridge.TriggerAction(step.ActionName);
                    break;
            }
        }
    }
}
