using Mk20Box.Runtime;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Mk20Box.Tests.Runtime
{
    /// <summary>
    /// Covers how a stored widget property name is matched against what SimHub
    /// actually exposes. Names saved by an earlier build, or typed by hand, may
    /// carry a different prefix from the one SimHub reports.
    /// </summary>
    [TestFixture]
    public class PropertyResolverTests
    {
        /// <summary>A stand-in for SimHub that only knows the names it is given.</summary>
        private static Func<string, object> Knows(params string[] names)
        {
            var known = new HashSet<string>(names, StringComparer.Ordinal);
            return name => known.Contains(name) ? "value" : null;
        }

        [Test]
        public void Resolve_UsesAnExactMatchFirst()
        {
            var asked = new List<string>();

            object value = PropertyResolver.Resolve("GameData.SpeedKmh", name =>
            {
                asked.Add(name);
                return "value";
            });

            Assert.Multiple(() =>
            {
                Assert.That(value, Is.EqualTo("value"));
                Assert.That(asked, Is.EqualTo(new[] { "GameData.SpeedKmh" }));
            });
        }

        [Test]
        public void Resolve_FindsABareNameUnderAKnownPrefix()
        {
            object value = PropertyResolver.Resolve("SpeedKmh", Knows("GameData.SpeedKmh"));

            Assert.That(value, Is.EqualTo("value"));
        }

        [Test]
        public void Resolve_FindsALeafWhenTheStoredPrefixIsWrong()
        {
            object value = PropertyResolver.Resolve(
                "DataCorePlugin.GameData.SpeedKmh",
                Knows("GameData.SpeedKmh"));

            Assert.That(value, Is.EqualTo("value"));
        }

        [Test]
        public void Resolve_FallsBackToTheBareLeaf()
        {
            object value = PropertyResolver.Resolve("GameData.SpeedKmh", Knows("SpeedKmh"));

            Assert.That(value, Is.EqualTo("value"));
        }

        [Test]
        public void Resolve_ReturnsNullWhenNothingMatches()
        {
            object value = PropertyResolver.Resolve("GameData.Missing", Knows("GameData.SpeedKmh"));

            Assert.That(value, Is.Null);
        }

        [Test]
        public void Resolve_DoesNotAskForTheSameNameTwice()
        {
            var asked = new List<string>();

            PropertyResolver.Resolve("SpeedKmh", name =>
            {
                asked.Add(name);
                return null;
            });

            Assert.That(asked, Is.Unique);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Resolve_IgnoresAnEmptyName(string name)
        {
            Assert.That(PropertyResolver.Resolve(name, _ => "value"), Is.Null);
        }

        [Test]
        public void Resolve_IgnoresAMissingReader()
        {
            Assert.That(PropertyResolver.Resolve("GameData.SpeedKmh", null), Is.Null);
        }

        [TestCase("DataCorePlugin.GameData.SpeedKmh", "SpeedKmh")]
        [TestCase("GameData.SpeedKmh", "SpeedKmh")]
        [TestCase("SpeedKmh", "SpeedKmh")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void Leaf_TakesTheLastSegment(string input, string expected)
        {
            Assert.That(PropertyResolver.Leaf(input), Is.EqualTo(expected));
        }

        [Test]
        public void Leaf_KeepsATrailingDotAsPartOfTheName()
        {
            // Nothing follows the dot, so there is no leaf to take.
            Assert.That(PropertyResolver.Leaf("GameData."), Is.EqualTo("GameData."));
        }
    }
}
