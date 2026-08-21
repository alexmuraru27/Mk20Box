using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mk20Control.Protocol.Client;
using Mk20Control.Protocol.Model;
using Mk20Control.Protocol.Theme;

namespace Mk20Box
{
    /// <summary>
    /// Owns the connection to the MK20. Everything that talks to the device goes
    /// through here, so the plugin and the settings UI share one client.
    /// </summary>
    public sealed class Mk20DeviceConnection
    {
        private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(60);

        private readonly object sync = new object();
        private Mk20DeviceClient client;
        private string status = "Not connected";

        /// <summary>Raised whenever <see cref="Status"/> or <see cref="IsConnected"/> changes.</summary>
        public event EventHandler StateChanged;

        public bool IsConnected { get; private set; }

        public string PortName { get; private set; }

        public DeviceIdentity Identity { get; private set; }

        /// <summary>The live client, for callers that need to bind to its events.</summary>
        public Mk20DeviceClient Client
        {
            get { return client; }
        }

        public string Status
        {
            get { lock (sync) { return status; } }
        }

        /// <summary>Serial ports currently present, natural-sorted (COM9 before COM10).</summary>
        public static IReadOnlyList<string> AvailablePorts()
        {
            return SerialPort.GetPortNames()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PortNumber)
                .ThenBy(port => port, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<bool> ConnectAsync(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
            {
                SetStatus(false, "Select a COM port first");
                return false;
            }

            await DisconnectAsync().ConfigureAwait(false);

            Mk20DeviceClient candidate = null;
            try
            {
                SetStatus(false, $"Connecting to {portName}...");
                candidate = Mk20DeviceClient.CreateForSerialPort(portName);
                await candidate.ConnectAsync().ConfigureAwait(false);

                DeviceIdentity identity = await candidate
                    .TryPingAsync(PingTimeout)
                    .ConfigureAwait(false);

                if (identity == null)
                {
                    await SafeDisposeAsync(candidate).ConfigureAwait(false);
                    SetStatus(false, $"{portName} did not answer - is it an MK20?");
                    return false;
                }

                client = candidate;
                PortName = portName;
                Identity = identity;
                SetStatus(true, DescribeIdentity(portName, identity));
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                await SafeDisposeAsync(candidate).ConfigureAwait(false);
                SetStatus(false, $"{portName} is in use by another program");
                return false;
            }
            catch (Exception ex)
            {
                await SafeDisposeAsync(candidate).ConfigureAwait(false);
                SimHub.Logging.Current.Warn($"[MK20Box] Connect to {portName} failed: {ex}");
                SetStatus(false, $"Could not connect: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends live values the loaded theme's widgets bind to by name. Failures are
        /// reported to the caller rather than logged, since this runs continuously.
        /// </summary>
        public async Task PushSystemDataAsync(IReadOnlyDictionary<string, string> values)
        {
            Mk20DeviceClient current = client;

            if (current == null || values == null || values.Count == 0)
            {
                return;
            }

            await current.PushSystemDataAsync(values).ConfigureAwait(false);
        }

        /// <summary>Builds a theme from the layout and sends it to the device.</summary>
        public async Task<bool> UploadThemeAsync(string themeName, ThemeFile theme)
        {
            Mk20DeviceClient current = client;

            if (current == null)
            {
                SetStatus(false, "Connect to the device first");
                return false;
            }

            try
            {
                SetStatus(true, "Uploading " + themeName + "...");
                await current.UploadThemeAsync(themeName, theme, UploadTimeout).ConfigureAwait(false);
                SetStatus(true, "Uploaded " + themeName + " to " + PortName);
                return true;
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn($"[MK20Box] Upload of {themeName} failed: {ex}");
                SetStatus(true, "Upload failed: " + ex.Message);
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            Mk20DeviceClient current = Interlocked.Exchange(ref client, null);
            if (current == null)
            {
                return;
            }

            await SafeDisposeAsync(current).ConfigureAwait(false);
            PortName = null;
            Identity = null;
            SetStatus(false, "Not connected");
        }

        private static string DescribeIdentity(string portName, DeviceIdentity identity)
        {
            string model = string.IsNullOrWhiteSpace(identity.ScreenModel) ? "MK20" : identity.ScreenModel;
            string firmware = string.IsNullOrWhiteSpace(identity.Version) ? "?" : identity.Version;
            return $"{model} on {portName} - firmware {firmware}";
        }

        private static async Task SafeDisposeAsync(Mk20DeviceClient candidate)
        {
            if (candidate == null)
            {
                return;
            }

            try
            {
                await candidate.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn($"[MK20Box] Closing the device failed: {ex.Message}");
            }
        }

        private static int PortNumber(string portName)
        {
            int number;
            return portName != null
                && portName.Length > 3
                && int.TryParse(portName.Substring(3), out number)
                ? number
                : int.MaxValue;
        }

        private void SetStatus(bool connected, string message)
        {
            lock (sync)
            {
                status = message;
            }

            IsConnected = connected;
            SimHub.Logging.Current.Info($"[MK20Box] {message}");
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
