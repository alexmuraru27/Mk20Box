using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mk20Box.Ui
{
    /// <summary>One icon file in the bundled library.</summary>
    public sealed class IconEntry
    {
        public string Path { get; set; }

        public string Name { get; set; }

        public string Category { get; set; }
    }

    /// <summary>Reads the bundled icon library off disk, grouped by category folder.</summary>
    public static class IconLibrary
    {
        private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        public const string AllCategories = "All icons";

        /// <summary>Every icon that ships with the plugin, or an empty list if none are deployed.</summary>
        public static IReadOnlyList<IconEntry> Load()
        {
            string root = Mk20Box.Layout.Mk20Assets.RootPath;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return new List<IconEntry>();
            }

            try
            {
                return Directory
                    .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                    .Where(path => Extensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()))
                    .Select(path => new IconEntry
                    {
                        Path = path,
                        Name = System.IO.Path.GetFileNameWithoutExtension(path),
                        Category = DescribeCategory(root, path),
                    })
                    .OrderBy(icon => icon.Category, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(icon => icon.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (IOException)
            {
                return new List<IconEntry>();
            }
            catch (UnauthorizedAccessException)
            {
                return new List<IconEntry>();
            }
        }

        /// <summary>Category names, with an "all" entry first.</summary>
        public static IReadOnlyList<string> Categories(IEnumerable<IconEntry> icons)
        {
            var categories = new List<string> { AllCategories };
            categories.AddRange(icons
                .Select(icon => icon.Category)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));

            return categories;
        }

        /// <summary>Folder path below the library root, e.g. "SimRacingIcons / 02-Driver-Aids".</summary>
        private static string DescribeCategory(string root, string path)
        {
            string relative = path.Substring(root.Length).TrimStart(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);

            string folder = System.IO.Path.GetDirectoryName(relative);
            return string.IsNullOrEmpty(folder)
                ? "Uncategorised"
                : folder.Replace(System.IO.Path.DirectorySeparatorChar.ToString(), " / ");
        }
    }
}
