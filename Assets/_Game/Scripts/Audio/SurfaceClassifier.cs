using System;
using System.Collections.Generic;

namespace NorthStar.Audio
{
    /// <summary>
    /// Pure, MonoBehaviour-free mapping from a hit's physic-material name or tag to a
    /// <see cref="SurfaceType"/>. <see cref="FootstepSystem"/> does the actual
    /// <c>Physics.Raycast</c> and hands the resulting name/tag here, keeping the
    /// classification rules unit-testable in EditMode.
    ///
    /// Matching is case-insensitive and substring-based, so a physic material named
    /// "Grass (Instance)" or a tag "WetStone" both resolve. Rules are checked in the
    /// order added; the first containing match wins. Unmatched input → <see cref="SurfaceType.Unknown"/>.
    /// </summary>
    public sealed class SurfaceClassifier
    {
        private readonly List<KeyValuePair<string, SurfaceType>> _rules =
            new List<KeyValuePair<string, SurfaceType>>();

        /// <summary>
        /// Create a classifier with the default keyword rules (grass/stone/wood/water).
        /// </summary>
        public static SurfaceClassifier CreateDefault()
        {
            var c = new SurfaceClassifier();
            c.AddRule("grass", SurfaceType.Grass);
            c.AddRule("stone", SurfaceType.Stone);
            c.AddRule("rock", SurfaceType.Stone);
            c.AddRule("wood", SurfaceType.Wood);
            c.AddRule("plank", SurfaceType.Wood);
            c.AddRule("water", SurfaceType.Water);
            return c;
        }

        /// <summary>
        /// Register a keyword → surface rule. The keyword is matched as a
        /// case-insensitive substring of the physic-material name or tag.
        /// Blank keywords are ignored.
        /// </summary>
        public void AddRule(string keyword, SurfaceType surface)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;
            _rules.Add(new KeyValuePair<string, SurfaceType>(keyword.Trim(), surface));
        }

        /// <summary>Number of registered rules.</summary>
        public int RuleCount => _rules.Count;

        /// <summary>
        /// Classify a surface from a candidate string (physic-material name or tag).
        /// Returns the first rule whose keyword is contained in the input, or
        /// <see cref="SurfaceType.Unknown"/> if none match (incl. null/empty input).
        /// </summary>
        public SurfaceType Classify(string nameOrTag)
        {
            if (string.IsNullOrEmpty(nameOrTag)) return SurfaceType.Unknown;
            foreach (var rule in _rules)
            {
                if (nameOrTag.IndexOf(rule.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return rule.Value;
            }
            return SurfaceType.Unknown;
        }

        /// <summary>
        /// Classify by checking several candidates in priority order (e.g. physic-material
        /// name first, then tag). Returns the first non-<see cref="SurfaceType.Unknown"/>
        /// result, or Unknown if every candidate is unmatched.
        /// </summary>
        public SurfaceType ClassifyAny(params string[] candidates)
        {
            if (candidates == null) return SurfaceType.Unknown;
            foreach (var c in candidates)
            {
                var result = Classify(c);
                if (result != SurfaceType.Unknown) return result;
            }
            return SurfaceType.Unknown;
        }
    }
}
