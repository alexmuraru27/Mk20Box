using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mk20Box.Layout;
using Newtonsoft.Json;

namespace Mk20Box.Runtime
{
    /// <summary>
    /// Reads and writes shareable profile files.
    ///
    /// A profile stores pictures as absolute paths, which mean nothing on someone
    /// else's machine, so an export is a zip carrying the artwork with it:
    ///
    ///   profile.json          the profile, with paths rewritten to references
    ///   media/&lt;name&gt;      every picture the profile uses
    ///
    /// Icons from the bundled library are referenced rather than copied, since the
    /// recipient already has them. That keeps a typical export small.
    /// </summary>
    public static class ProfileTransfer
    {
        public const string FileExtension = ".mk20profile";

        public const string FileFilter =
            "MK20Box profile (*.mk20profile)|*.mk20profile|All files|*.*";

        /// <summary>Bumped only if the layout of the file itself changes.</summary>
        private const int CurrentFormatVersion = 1;

        private const string ManifestEntry = "profile.json";
        private const string MediaFolder = "media/";

        /// <summary>Points at the bundled icon library instead of embedding a copy.</summary>
        private const string LibraryPrefix = "lib:";

        /// <summary>Points at a picture carried inside the file.</summary>
        private const string PackagePrefix = "pkg:";

        /// <summary>Writes one profile, with its artwork, to <paramref name="path"/>.</summary>
        public static void Export(Mk20ProfileSettings profile, string path)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            // Exported from a copy, so rewriting paths cannot disturb the live profile.
            Mk20ProfileSettings copy = Clone(profile);
            var media = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (PathSlot slot in PathsOf(copy))
            {
                slot.Value = Pack(slot.Value, media);
            }

            var manifest = new ProfileFile
            {
                FormatVersion = CurrentFormatVersion,
                ExportedUtc = DateTime.UtcNow,
                PluginVersion = typeof(ProfileTransfer).Assembly.GetName().Version.ToString(),
                Profile = copy,
            };

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Written to a temporary file first, so a failure cannot leave a
            // half-written export where a good one used to be.
            string temporary = path + ".tmp";

            try
            {
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write))
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    WriteText(archive, ManifestEntry, JsonConvert.SerializeObject(manifest, Formatting.Indented));

                    foreach (KeyValuePair<string, string> entry in media)
                    {
                        WriteFile(archive, MediaFolder + entry.Key, entry.Value);
                    }
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        /// <summary>
        /// Reads a profile file and adds it to <paramref name="settings"/>. Its
        /// artwork is unpacked to a per-import folder, so two profiles sharing a
        /// picture name cannot overwrite each other.
        /// </summary>
        public static Mk20ProfileSettings Import(string path, Mk20BoxPluginSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            ProfileFile manifest;
            string mediaDirectory;

            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
            {
                ZipArchiveEntry entry = archive.GetEntry(ManifestEntry);
                if (entry == null)
                {
                    throw new InvalidDataException(
                        "This is not an MK20Box profile file: it has no " + ManifestEntry + ".");
                }

                manifest = JsonConvert.DeserializeObject<ProfileFile>(ReadText(entry));

                if (manifest == null || manifest.Profile == null)
                {
                    throw new InvalidDataException("The profile file is empty or corrupt.");
                }

                if (manifest.FormatVersion > CurrentFormatVersion)
                {
                    throw new InvalidDataException(
                        "This profile was exported by a newer version of MK20Box. Update the plugin and try again.");
                }

                mediaDirectory = UnpackMedia(archive);
            }

            Mk20ProfileSettings profile = manifest.Profile;

            // A fresh id, so importing the same file twice gives two profiles rather
            // than silently replacing one.
            profile.Id = Mk20BoxPluginSettings.CreateProfileId();
            profile.Name = UniqueName(profile.Name, settings);

            foreach (PathSlot slot in PathsOf(profile))
            {
                slot.Value = Unpack(slot.Value, mediaDirectory);
            }

            settings.Profiles.Add(profile);
            settings.SortProfiles();

            return profile;
        }

        /// <summary>
        /// Deletes imported artwork no profile refers to any more, which is what a
        /// deleted or re-imported profile leaves behind. Only folders this class
        /// created are considered, so pictures of your own are never touched.
        /// </summary>
        public static int RemoveUnusedMedia(Mk20BoxPluginSettings settings)
        {
            if (settings?.Profiles == null || !Directory.Exists(SharedMediaRoot))
            {
                return 0;
            }

            var inUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Mk20ProfileSettings profile in settings.Profiles)
            {
                if (profile == null)
                {
                    continue;
                }

                foreach (PathSlot slot in PathsOf(profile))
                {
                    string value = slot.Value;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        inUse.Add(Path.GetFullPath(value));
                    }
                }
            }

            int removed = 0;

            foreach (string folder in Directory.GetDirectories(SharedMediaRoot))
            {
                bool used = Directory
                    .GetFiles(folder)
                    .Any(file => inUse.Contains(Path.GetFullPath(file)));

                if (used)
                {
                    continue;
                }

                try
                {
                    Directory.Delete(folder, true);
                    removed++;
                }
                catch (IOException)
                {
                    // Locked by a preview; it will be collected next time.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return removed;
        }

        /// <summary>Reads just the name, so a caller can describe a file before importing it.</summary>
        public static string PeekName(string path)
        {
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry entry = archive.GetEntry(ManifestEntry);
                    if (entry == null)
                    {
                        return null;
                    }

                    ProfileFile manifest = JsonConvert.DeserializeObject<ProfileFile>(ReadText(entry));
                    return manifest?.Profile?.Name;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>A file name that is safe on disk and recognisable afterwards.</summary>
        public static string SuggestFileName(Mk20ProfileSettings profile)
        {
            string name = profile == null || string.IsNullOrWhiteSpace(profile.Name)
                ? "profile"
                : profile.Name.Trim();

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '-');
            }

            return name + FileExtension;
        }

        // ---- paths ---------------------------------------------------------------

        /// <summary>
        /// Turns an absolute path into something portable, collecting anything that
        /// has to travel with the file.
        /// </summary>
        private static string Pack(string path, IDictionary<string, string> media)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                // A picture that has already gone missing locally; nothing to carry.
                return null;
            }

            string library = Mk20Assets.RootPath;

            if (!string.IsNullOrEmpty(library) && IsUnder(path, library))
            {
                string relative = path.Substring(library.Length).TrimStart('\\', '/');
                return LibraryPrefix + relative.Replace('\\', '/');
            }

            string entryName = MediaEntryName(path);
            if (!media.ContainsKey(entryName))
            {
                media[entryName] = path;
            }

            return PackagePrefix + entryName;
        }

        /// <summary>Turns a portable reference back into a path on this machine.</summary>
        private static string Unpack(string reference, string mediaDirectory)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            if (reference.StartsWith(LibraryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string library = Mk20Assets.RootPath;
                if (string.IsNullOrEmpty(library))
                {
                    return null;
                }

                string relative = reference.Substring(LibraryPrefix.Length).Replace('/', '\\');
                string resolved = Path.Combine(library, relative);

                return File.Exists(resolved) ? resolved : null;
            }

            if (reference.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (mediaDirectory == null)
                {
                    return null;
                }

                string resolved = Path.Combine(
                    mediaDirectory,
                    reference.Substring(PackagePrefix.Length).Replace('/', '\\'));

                return File.Exists(resolved) ? resolved : null;
            }

            // An older or hand-edited file holding a plain path.
            return File.Exists(reference) ? reference : null;
        }

        /// <summary>Every picture path in a profile, as assignable slots.</summary>
        private static IEnumerable<PathSlot> PathsOf(Mk20ProfileSettings profile)
        {
            Mk20LayoutSettings layout = profile.Layout;
            if (layout?.Pages == null)
            {
                yield break;
            }

            foreach (Mk20PageSettings page in layout.Pages)
            {
                if (page == null)
                {
                    continue;
                }

                Mk20PageSettings owner = page;

                yield return new PathSlot(
                    () => owner.BackgroundPath,
                    value => owner.BackgroundPath = value);

                yield return new PathSlot(
                    () => owner.SecondaryBackgroundPath,
                    value => owner.SecondaryBackgroundPath = value);

                if (page.Keys == null)
                {
                    continue;
                }

                foreach (Mk20KeySettings key in page.Keys)
                {
                    if (key == null)
                    {
                        continue;
                    }

                    Mk20KeySettings keyOwner = key;

                    yield return new PathSlot(
                        () => keyOwner.MediaPath,
                        value => keyOwner.MediaPath = value);
                }
            }
        }

        // ---- storage -------------------------------------------------------------

        /// <summary>
        /// Imported artwork lives under the user's own profile rather than beside the
        /// plugin: the SimHub folder is usually in Program Files and not writable.
        /// </summary>
        private static string SharedMediaRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Mk20Box",
                    "SharedMedia");
            }
        }

        private static string UnpackMedia(ZipArchive archive)
        {
            ZipArchiveEntry[] entries = archive.Entries
                .Where(entry => entry.FullName.StartsWith(MediaFolder, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(entry.Name))
                .ToArray();

            if (entries.Length == 0)
            {
                return null;
            }

            string directory = Path.Combine(
                SharedMediaRoot,
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));

            Directory.CreateDirectory(directory);

            foreach (ZipArchiveEntry entry in entries)
            {
                // Only the file name is used, so a crafted archive cannot write
                // outside this folder.
                string target = Path.Combine(directory, Path.GetFileName(entry.Name));
                entry.ExtractToFile(target, true);
            }

            return directory;
        }

        /// <summary>
        /// Named by content, so the same picture used on twenty keys is stored once
        /// and two different pictures never collide.
        /// </summary>
        private static string MediaEntryName(string path)
        {
            using (var sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                string hash = BitConverter
                    .ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .Substring(0, 16)
                    .ToLowerInvariant();

                return hash + Path.GetExtension(path).ToLowerInvariant();
            }
        }

        private static bool IsUnder(string path, string folder)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(folder).TrimEnd('\\') + "\\";

            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private static string UniqueName(string name, Mk20BoxPluginSettings settings)
        {
            string candidate = string.IsNullOrWhiteSpace(name) ? "Imported profile" : name.Trim();

            if (settings.FindProfileByName(candidate) == null)
            {
                return candidate;
            }

            for (int suffix = 2; ; suffix++)
            {
                string attempt = candidate + " (" + suffix + ")";
                if (settings.FindProfileByName(attempt) == null)
                {
                    return attempt;
                }
            }
        }

        private static Mk20ProfileSettings Clone(Mk20ProfileSettings profile)
        {
            return JsonConvert.DeserializeObject<Mk20ProfileSettings>(
                JsonConvert.SerializeObject(profile));
        }

        private static void WriteText(ZipArchive archive, string entryName, string text)
        {
            using (Stream stream = archive.CreateEntry(entryName).Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(text);
            }
        }

        private static void WriteFile(ZipArchive archive, string entryName, string sourcePath)
        {
            using (Stream stream = archive.CreateEntry(entryName).Open())
            using (FileStream source = File.OpenRead(sourcePath))
            {
                source.CopyTo(stream);
            }
        }

        private static string ReadText(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>An assignable picture path somewhere in a profile.</summary>
        private sealed class PathSlot
        {
            private readonly Func<string> read;
            private readonly Action<string> write;

            public PathSlot(Func<string> read, Action<string> write)
            {
                this.read = read;
                this.write = write;
            }

            public string Value
            {
                get { return read(); }
                set { write(value); }
            }
        }

        /// <summary>What a .mk20profile file contains.</summary>
        private sealed class ProfileFile
        {
            public int FormatVersion { get; set; }

            public DateTime ExportedUtc { get; set; }

            public string PluginVersion { get; set; }

            public Mk20ProfileSettings Profile { get; set; }
        }
    }
}
