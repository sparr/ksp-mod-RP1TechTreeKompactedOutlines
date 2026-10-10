using System.Collections.Generic;
using UnityEngine;

namespace RP1TechTreeKompactedOutlines
{
    /// <summary>Builds the closed path of one outline.</summary>
    public static class OutlineShape
    {
        private const int ArcSegments = 6;

        /// <summary>
        /// A rectangle with a notch cut into its left side for each cutout, so the outline
        /// passes each cutout on the right, and a tab reaching out of its right side around
        /// each bump. Cutouts that would leave less than <paramref name="minPassage"/> of
        /// width, or that reach the top or bottom, are ignored. Bumps must lie to the right of
        /// the box and within its height, and must not overlap each other vertically.
        /// </summary>
        public static List<Vector2> Corners(Rect box, List<Rect> cutouts, List<Rect> bumps, float minPassage)
        {
            var notches = new List<Rect>();
            foreach (Rect cutout in cutouts)
            {
                if (cutout.xMax > box.xMax - minPassage || cutout.yMin <= box.yMin || cutout.yMax >= box.yMax)
                    continue;
                notches.Add(cutout);
            }
            // Bottom to top, merging any that overlap vertically.
            notches.Sort((a, b) => a.yMin.CompareTo(b.yMin));
            for (int i = notches.Count - 1; i > 0; i--)
            {
                if (notches[i].yMin <= notches[i - 1].yMax)
                {
                    notches[i - 1] = Rect.MinMaxRect(0f, notches[i - 1].yMin,
                        Mathf.Max(notches[i - 1].xMax, notches[i].xMax),
                        Mathf.Max(notches[i - 1].yMax, notches[i].yMax));
                    notches.RemoveAt(i);
                }
            }

            var tabs = new List<Rect>(bumps);
            // Top to bottom.
            tabs.Sort((a, b) => b.yMax.CompareTo(a.yMax));

            var corners = new List<Vector2>
            {
                new Vector2(box.xMin, box.yMax),
                new Vector2(box.xMax, box.yMax),
            };
            foreach (Rect tab in tabs)
            {
                corners.Add(new Vector2(box.xMax, tab.yMax));
                corners.Add(new Vector2(tab.xMax, tab.yMax));
                corners.Add(new Vector2(tab.xMax, tab.yMin));
                corners.Add(new Vector2(box.xMax, tab.yMin));
            }
            corners.Add(new Vector2(box.xMax, box.yMin));
            corners.Add(new Vector2(box.xMin, box.yMin));
            foreach (Rect notch in notches)
            {
                corners.Add(new Vector2(box.xMin, notch.yMin));
                corners.Add(new Vector2(notch.xMax, notch.yMin));
                corners.Add(new Vector2(notch.xMax, notch.yMax));
                corners.Add(new Vector2(box.xMin, notch.yMax));
            }
            return corners;
        }

        /// <summary>Replaces each corner of a closed path with an arc.</summary>
        public static List<Vector2> Round(List<Vector2> corners, float radius)
        {
            var path = new List<Vector2>();
            int count = corners.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 corner = corners[i];
                Vector2 toPrevious = corners[(i + count - 1) % count] - corner;
                Vector2 toNext = corners[(i + 1) % count] - corner;
                // Leave room for the arcs at both ends of each edge.
                float r = Mathf.Min(radius, toPrevious.magnitude / 2f, toNext.magnitude / 2f);
                Vector2 a = toPrevious.normalized * r;
                Vector2 b = toNext.normalized * r;
                Vector2 center = corner + a + b;
                for (int step = 0; step <= ArcSegments; step++)
                {
                    float angle = step * Mathf.PI / 2f / ArcSegments;
                    path.Add(center - b * Mathf.Cos(angle) - a * Mathf.Sin(angle));
                }
            }
            return path;
        }
    }
}
