using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RP1TechTreeKompactedOutlines
{
    /// <summary>Draws a closed path as a line of constant thickness with soft edges.</summary>
    public class OutlineGraphic : MaskableGraphic
    {
        // Width of the fade on each side of the line, in local units.
        private const float Feather = 0.75f;

        /// <summary>The path, in this transform's local space.</summary>
        public List<Vector2> path = new List<Vector2>();

        public float thickness = 2f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int count = path.Count;
            if (count < 3)
                return;

            Color32 solid = color;
            Color32 clear = new Color(color.r, color.g, color.b, 0f);
            float half = thickness / 2f;
            float[] offsets = { -half - Feather, -half, half, half + Feather };
            Color32[] colors = { clear, solid, solid, clear };

            for (int i = 0; i < count; i++)
            {
                Vector2 point = path[i];
                Vector2 incoming = (point - path[(i + count - 1) % count]).normalized;
                Vector2 outgoing = (path[(i + 1) % count] - point).normalized;
                // Offsetting along the miter keeps the line's thickness constant through a bend.
                Vector2 miter = (Perpendicular(incoming) + Perpendicular(outgoing)).normalized;
                float scale = 1f / Mathf.Max(Vector2.Dot(miter, Perpendicular(incoming)), 0.5f);
                for (int j = 0; j < offsets.Length; j++)
                    vh.AddVert(point + miter * (offsets[j] * scale), colors[j], Vector2.zero);
            }

            int across = offsets.Length;
            for (int i = 0; i < count; i++)
            {
                int a = i * across;
                int b = (i + 1) % count * across;
                for (int j = 0; j < across - 1; j++)
                {
                    vh.AddTriangle(a + j, b + j, b + j + 1);
                    vh.AddTriangle(a + j, b + j + 1, a + j + 1);
                }
            }
        }

        private static Vector2 Perpendicular(Vector2 v)
        {
            return new Vector2(-v.y, v.x);
        }
    }
}
