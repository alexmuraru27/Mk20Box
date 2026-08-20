using System;
using System.IO;
using System.Reflection;

namespace Mk20Box
{
    /// <summary>
    /// Resolves the plugin's private dependencies from its own subfolder.
    ///
    /// SimHub ships older versions of several assemblies this plugin depends on
    /// (for example Microsoft.Extensions.Logging.Abstractions 2.2.0), and its
    /// binding redirects only cover those older ranges. Overwriting SimHub's own
    /// files is not acceptable, so the plugin keeps its dependencies in
    /// <c>&lt;SimHub&gt;\Mk20Box\</c> and binds them on demand.
    /// </summary>
    internal static class DependencyResolver
    {
        private const string DependencyFolderName = "Mk20Box";

        private static readonly object SyncRoot = new object();
        private static bool installed;

        public static void Install()
        {
            lock (SyncRoot)
            {
                if (installed)
                {
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += ResolveFromPluginFolder;
                installed = true;
            }
        }

        private static Assembly ResolveFromPluginFolder(object sender, ResolveEventArgs args)
        {
            string simpleName = new AssemblyName(args.Name).Name;
            string candidate = Path.Combine(DependencyFolder, simpleName + ".dll");

            if (!File.Exists(candidate))
            {
                return null;
            }

            try
            {
                return Assembly.LoadFrom(candidate);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Warn($"[MK20Box] Could not load {simpleName}: {ex.Message}");
                return null;
            }
        }

        private static string DependencyFolder =>
            Path.Combine(
                Path.GetDirectoryName(typeof(DependencyResolver).Assembly.Location) ?? string.Empty,
                DependencyFolderName);
    }
}
