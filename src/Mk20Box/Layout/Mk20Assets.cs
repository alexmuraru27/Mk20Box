using System;
using System.IO;
using System.Reflection;

namespace Mk20Box.Layout
{
    /// <summary>
    /// Locates the icon library that ships with the plugin. It is deployed to
    /// <c>&lt;SimHub&gt;\Mk20Box\Mk20Assets\</c>, beside the plugin's own dependencies.
    /// </summary>
    public static class Mk20Assets
    {
        public const string FolderName = "Mk20Assets";

        /// <summary>Full path to the icon library, or null when it is not deployed.</summary>
        public static string RootPath
        {
            get
            {
                foreach (string candidate in CandidateRoots())
                {
                    if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                return null;
            }
        }

        /// <summary>Where an icon picker should open. Falls back to Pictures.</summary>
        public static string DefaultBrowseFolder
        {
            get
            {
                return RootPath
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }
        }

        private static string[] CandidateRoots()
        {
            string pluginDir = Path.GetDirectoryName(typeof(Mk20Assets).Assembly.Location) ?? string.Empty;

            return new[]
            {
                // Beside the plugin's private dependencies.
                Path.Combine(pluginDir, "Mk20Box", FolderName),

                // Beside the assembly itself (build output, or a flat install).
                Path.Combine(pluginDir, FolderName),
            };
        }
    }
}
