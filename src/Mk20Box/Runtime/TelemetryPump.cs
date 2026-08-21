using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mk20Box.Layout;

namespace Mk20Box.Runtime
{
    /// <summary>
    /// Pushes the values the active page's widgets are bound to. The device holds the
    /// last value it received, so this keeps running while a page with widgets is on
    /// screen and stops when there is nothing to send.
    /// </summary>
    public sealed class TelemetryPump : IDisposable
    {
        private readonly object sync = new object();
        private readonly Mk20DeviceConnection device;
        private readonly Func<string, object> readProperty;

        private List<Mk20WidgetSettings> widgets = new List<Mk20WidgetSettings>();
        private CancellationTokenSource cancellation;
        private bool needsClock;
        private bool reportedFirstPush;

        public TelemetryPump(Mk20DeviceConnection device, Func<string, object> readProperty)
        {
            this.device = device;
            this.readProperty = readProperty;
        }

        /// <summary>How often values are sent. The clock only needs one per second.</summary>
        public int IntervalMs { get; set; } = 250;

        /// <summary>Points the pump at the widgets of the profile now on the device.</summary>
        public void SetActiveLayout(Mk20LayoutSettings layout)
        {
            List<Mk20WidgetSettings> active = layout == null
                ? new List<Mk20WidgetSettings>()
                : layout.Pages
                    .Where(page => page.Widgets != null)
                    .SelectMany(page => page.Widgets)
                    .ToList();

            lock (sync)
            {
                widgets = active;
                needsClock = active.Any(widget => widget.Kind == WidgetKinds.Clock);
            }

            if (active.Count == 0)
            {
                Stop();
            }
            else
            {
                Start();
            }
        }

        public void Start()
        {
            lock (sync)
            {
                if (cancellation != null)
                {
                    return;
                }

                cancellation = new CancellationTokenSource();
            }

            Task.Run(() => PumpAsync(cancellation.Token));
        }

        public void Stop()
        {
            CancellationTokenSource running;

            lock (sync)
            {
                running = cancellation;
                cancellation = null;
            }

            if (running != null)
            {
                running.Cancel();
                running.Dispose();
            }
        }

        private async Task PumpAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (device.IsConnected)
                    {
                        Dictionary<string, string> values = Collect();

                        if (values.Count > 0)
                        {
                            await device.PushSystemDataAsync(values).ConfigureAwait(false);

                            if (!reportedFirstPush)
                            {
                                reportedFirstPush = true;
                                SimHub.Logging.Current.Info(
                                    $"[MK20Box] Streaming {values.Count} widget value(s) to the device");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SimHub.Logging.Current.Warn($"[MK20Box] Telemetry push failed: {ex.Message}");
                }

                try
                {
                    await Task.Delay(IntervalMs, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        private Dictionary<string, string> Collect()
        {
            List<Mk20WidgetSettings> snapshot;
            bool clock;

            lock (sync)
            {
                snapshot = widgets;
                clock = needsClock;
            }

            var values = new Dictionary<string, string>();

            foreach (Mk20WidgetSettings widget in snapshot)
            {
                if (!widget.IsBound || string.IsNullOrEmpty(widget.Channel))
                {
                    continue;
                }

                string value = Format(widget);

                // Pushing an empty string blanks the widget, so an unavailable value
                // leaves whatever was last shown instead.
                if (!string.IsNullOrEmpty(value))
                {
                    values[widget.Channel] = value;
                }
            }

            if (clock)
            {
                // The device has no clock of its own, so the host drives it.
                DateTime now = DateTime.Now;
                values["hour"] = now.Hour.ToString(CultureInfo.InvariantCulture);
                values["minute"] = now.Minute.ToString(CultureInfo.InvariantCulture);
                values["second"] = now.Second.ToString(CultureInfo.InvariantCulture);
            }

            return values;
        }

        /// <summary>
        /// Values go over the wire as strings. Gauges still need a bare number, so the
        /// unit is only appended when nothing is reading it as a number.
        /// </summary>
        private string Format(Mk20WidgetSettings widget)
        {
            object raw;

            try
            {
                raw = readProperty(widget.Property);
            }
            catch (Exception)
            {
                return string.Empty;
            }

            if (raw == null)
            {
                return string.Empty;
            }

            string text;

            if (widget.Decimals >= 0 && IsNumber(raw))
            {
                double number = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                text = number.ToString("F" + widget.Decimals, CultureInfo.InvariantCulture);
            }
            else
            {
                text = Convert.ToString(raw, CultureInfo.InvariantCulture);
            }

            // Bars need a bare number; a unit suffix would break their range parsing.
            string suffix = widget.ValueSuffix;
            return string.IsNullOrEmpty(suffix) ? text : text + suffix;
        }

        private static bool IsNumber(object value)
        {
            return value is double
                || value is float
                || value is int
                || value is long
                || value is short
                || value is decimal
                || value is byte;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
