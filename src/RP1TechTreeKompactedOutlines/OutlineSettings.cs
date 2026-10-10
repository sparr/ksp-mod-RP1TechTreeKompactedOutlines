using UnityEngine;

namespace RP1TechTreeKompactedOutlines
{
    /// <summary>The contents of the RP1_TECHTREE_KOMPACTED_OUTLINES config node.</summary>
    public class OutlineSettings
    {
        public const string NodeName = "RP1_TECHTREE_KOMPACTED_OUTLINES";

        public float thickness = 2f;
        public float padding = 7f;
        public float cornerRadius = 10f;
        public float lineAlpha = 0.9f;
        public float cutoutGap = 5f;
        public float bumpPadding = 2f;

        /// <summary>Returns null when no RP1_TECHTREE_KOMPACTED_OUTLINES node is loaded.</summary>
        public static OutlineSettings Load()
        {
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(NodeName);
            if (nodes.Length == 0)
                return null;

            var settings = new OutlineSettings();
            foreach (ConfigNode node in nodes)
            {
                node.TryGetValue("thickness", ref settings.thickness);
                node.TryGetValue("padding", ref settings.padding);
                node.TryGetValue("cornerRadius", ref settings.cornerRadius);
                node.TryGetValue("lineAlpha", ref settings.lineAlpha);
                node.TryGetValue("cutoutGap", ref settings.cutoutGap);
                node.TryGetValue("bumpPadding", ref settings.bumpPadding);
            }

            settings.thickness = Mathf.Max(settings.thickness, 1f);
            settings.cornerRadius = Mathf.Max(settings.cornerRadius, settings.thickness);
            settings.lineAlpha = Mathf.Clamp01(settings.lineAlpha);
            return settings;
        }
    }
}
