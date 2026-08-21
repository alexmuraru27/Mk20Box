using System;

namespace Mk20Box.Runtime
{
    /// <summary>
    /// Finds a SimHub property even when the stored name carries a different prefix.
    /// SimHub lists game values as "GameData.SpeedKmh", but a name typed by hand or
    /// saved by an earlier build may be fully qualified or bare.
    /// </summary>
    public static class PropertyResolver
    {
        private static readonly string[] Prefixes =
        {
            "GameData.",
            "DataCorePlugin.GameData.",
            "DataCorePlugin.",
            string.Empty,
        };

        /// <summary>Tries the name as given, then the same leaf under known prefixes.</summary>
        public static object Resolve(string name, Func<string, object> read)
        {
            if (string.IsNullOrWhiteSpace(name) || read == null)
            {
                return null;
            }

            object value = read(name);
            if (value != null)
            {
                return value;
            }

            string leaf = Leaf(name);

            foreach (string prefix in Prefixes)
            {
                string candidate = prefix + leaf;
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                {
                    continue;
                }

                value = read(candidate);
                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        public static string Leaf(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            int lastDot = name.LastIndexOf('.');
            return lastDot >= 0 && lastDot < name.Length - 1
                ? name.Substring(lastDot + 1)
                : name;
        }
    }
}
