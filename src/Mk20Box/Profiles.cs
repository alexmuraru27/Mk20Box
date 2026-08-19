using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Mk20Box
{
    public partial class Mk20BoxPluginSettings
    {
        public void Normalize()
        {
            if (Profiles == null)
            {
                Profiles = new ObservableCollection<Mk20ProfileSettings>();
            }

            if (GameProfiles == null)
            {
                GameProfiles = new ObservableCollection<Mk20GameProfileBindingSettings>();
            }

            NormalizeProfiles();
            SortProfiles();
            MigrateGameBindings();
            SortGameProfiles();

            Mk20ProfileSettings globalProfile = FindProfileById(GlobalProfileId);
            if (globalProfile == null)
            {
                globalProfile = FindProfileByName(DefaultProfileName) ?? Profiles[0];
                GlobalProfileId = globalProfile.Id;
            }

            DefaultProfileName = globalProfile.Name;
        }

        public Mk20ProfileSettings FindProfileById(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || Profiles == null)
            {
                return null;
            }

            foreach (Mk20ProfileSettings profile in Profiles)
            {
                if (profile != null
                    && string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            return null;
        }

        public Mk20ProfileSettings FindProfileByName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName) || Profiles == null)
            {
                return null;
            }

            foreach (Mk20ProfileSettings profile in Profiles)
            {
                if (profile != null
                    && string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            return null;
        }

        public Mk20GameProfileBindingSettings FindGameProfile(string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName) || GameProfiles == null)
            {
                return null;
            }

            foreach (Mk20GameProfileBindingSettings binding in GameProfiles)
            {
                if (binding != null
                    && string.Equals(binding.GameName, gameName, StringComparison.OrdinalIgnoreCase))
                {
                    return binding;
                }
            }

            return null;
        }

        public static string CreateProfileId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public void SortProfiles()
        {
            Mk20ProfileSettings[] sortedProfiles = Profiles
                .OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            for (int targetIndex = 0; targetIndex < sortedProfiles.Length; targetIndex++)
            {
                int currentIndex = Profiles.IndexOf(sortedProfiles[targetIndex]);
                if (currentIndex != targetIndex)
                {
                    Profiles.Move(currentIndex, targetIndex);
                }
            }
        }

        public void SortGameProfiles()
        {
            Mk20GameProfileBindingSettings[] sortedBindings = GameProfiles
                .OrderBy(binding => binding.GameName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            for (int targetIndex = 0; targetIndex < sortedBindings.Length; targetIndex++)
            {
                int currentIndex = GameProfiles.IndexOf(sortedBindings[targetIndex]);
                if (currentIndex != targetIndex)
                {
                    GameProfiles.Move(currentIndex, targetIndex);
                }
            }
        }

        private void NormalizeProfiles()
        {
            for (int index = Profiles.Count - 1; index >= 0; index--)
            {
                if (Profiles[index] == null)
                {
                    Profiles.RemoveAt(index);
                }
            }

            if (Profiles.Count == 0)
            {
                Profiles.Add(new Mk20ProfileSettings
                {
                    Id = CreateProfileId(),
                    Name = string.IsNullOrWhiteSpace(DefaultProfileName)
                        ? "Default"
                        : DefaultProfileName.Trim(),
                });
            }

            foreach (Mk20ProfileSettings profile in Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Id)
                    || HasDuplicateProfileId(profile))
                {
                    profile.Id = CreateProfileId();
                }

                profile.Name = string.IsNullOrWhiteSpace(profile.Name)
                    ? "Profile"
                    : profile.Name.Trim();
            }
        }

        private void MigrateGameBindings()
        {
            for (int index = GameProfiles.Count - 1; index >= 0; index--)
            {
                Mk20GameProfileBindingSettings binding = GameProfiles[index];
                if (binding == null || string.IsNullOrWhiteSpace(binding.GameName))
                {
                    GameProfiles.RemoveAt(index);
                    continue;
                }

                binding.GameName = binding.GameName.Trim();
                if (FindProfileById(binding.ProfileId) != null)
                {
                    continue;
                }

                Mk20ProfileSettings profile = FindProfileByName(binding.ProfileName);
                if (profile == null && !string.IsNullOrWhiteSpace(binding.ProfileName))
                {
                    profile = new Mk20ProfileSettings
                    {
                        Id = CreateProfileId(),
                        Name = binding.ProfileName.Trim(),
                    };
                    Profiles.Add(profile);
                }

                binding.ProfileId = (profile ?? Profiles[0]).Id;
            }
        }

        private bool HasDuplicateProfileId(Mk20ProfileSettings candidate)
        {
            int matches = 0;
            foreach (Mk20ProfileSettings profile in Profiles)
            {
                if (profile != null
                    && string.Equals(profile.Id, candidate.Id, StringComparison.OrdinalIgnoreCase)
                    && ++matches > 1)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class Mk20ProfileSettings : NotifyPropertyChangedBase
    {
        private string id;
        private string name;

        public string Id
        {
            get => id;
            set => SetField(ref id, value);
        }

        public string Name
        {
            get => name;
            set => SetField(ref name, value);
        }
    }

    public class Mk20GameProfileBindingSettings : NotifyPropertyChangedBase
    {
        private string gameName;
        private string profileId;

        public string GameName
        {
            get => gameName;
            set => SetField(ref gameName, value);
        }

        public string ProfileId
        {
            get => profileId;
            set => SetField(ref profileId, value);
        }

        // Legacy field used only when migrating settings saved by older builds.
        public string ProfileName { get; set; }
    }

    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
