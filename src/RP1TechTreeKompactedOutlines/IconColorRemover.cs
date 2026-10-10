using System.Collections.Generic;
using KSP.UI.Screens;
using RUI.Icons.Simple;
using UnityEngine;

namespace RP1TechTreeKompactedOutlines
{
    /// <summary>
    /// Removes the colored frame that RP-1 TechTree Kompacted's icons carry, leaving the white
    /// symbol.
    /// </summary>
    public class IconColorRemover
    {
        // A pixel is colored when its channels differ by more than this.
        private const int ColorThreshold = 40;

        private readonly Dictionary<Texture, Texture2D> cache = new Dictionary<Texture, Texture2D>();
        private readonly Dictionary<Texture, Color?> frameColors = new Dictionary<Texture, Color?>();

        // The frame's pixels, learned from the colored icons. The white era's frame cannot be
        // told from the symbol by color, so it is removed by position.
        private bool[] frame;
        private int frameWidth, frameHeight;

        public void Apply(List<RDNode> nodes)
        {
            var pixels = new Dictionary<Texture, Color32[]>();
            foreach (RDNode node in nodes)
            {
                Texture texture = node.icon?.texture;
                if (texture == null || pixels.ContainsKey(texture) || (cache.TryGetValue(texture, out Texture2D done) && done != null))
                    continue;
                Color32[] read = Read(texture);
                pixels[texture] = read;
                if (frame == null)
                    LearnFrame(read, texture.width, texture.height);
            }

            foreach (KeyValuePair<Texture, Color32[]> pair in pixels)
                cache[pair.Key] = Strip(pair.Key, pair.Value);

            foreach (RDNode node in nodes)
            {
                Texture texture = node.icon?.texture;
                if (texture != null && cache.TryGetValue(texture, out Texture2D stripped) && stripped != texture)
                    node.SetIconState(new Icon(node.icon.GetName(), stripped));
            }
        }

        /// <summary>
        /// The color of an icon's frame. False when the icon has no frame. A white frame is only
        /// recognized once <see cref="Apply"/> has seen a colored one.
        /// </summary>
        public bool TryGetFrameColor(Texture texture, out Color color)
        {
            color = default;
            if (texture == null)
                return false;
            if (!frameColors.TryGetValue(texture, out Color? found))
            {
                Color32[] pixels = Read(texture);
                found = null;
                for (int i = 0; i < pixels.Length && found == null; i++)
                {
                    if (IsColored(pixels[i]))
                        found = new Color(pixels[i].r / 255f, pixels[i].g / 255f, pixels[i].b / 255f);
                }
                if (found == null && HasWhiteFrame(texture, pixels))
                    found = Color.white;
                frameColors[texture] = found;
            }
            color = found ?? default;
            return found != null;
        }

        private static bool IsColored(Color32 c)
        {
            int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return c.a > 0 && max - min > ColorThreshold;
        }

        private void LearnFrame(Color32[] pixels, int width, int height)
        {
            var mask = new bool[pixels.Length];
            bool any = false;
            for (int i = 0; i < pixels.Length; i++)
                any |= mask[i] = IsColored(pixels[i]);
            if (!any)
                return;
            frame = mask;
            frameWidth = width;
            frameHeight = height;
        }

        // Returns the texture itself when there is nothing to remove.
        private Texture2D Strip(Texture source, Color32[] pixels)
        {
            bool changed = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (IsColored(pixels[i]))
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    changed = true;
                }
            }

            if (!changed && HasWhiteFrame(source, pixels))
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (frame[i])
                        pixels[i] = new Color32(0, 0, 0, 0);
                }
                changed = true;
            }

            if (!changed)
                return source as Texture2D;

            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            result.name = source.name;
            result.filterMode = source.filterMode;
            result.wrapMode = TextureWrapMode.Clamp;
            result.SetPixels32(pixels);
            result.Apply(false, true);
            return result;
        }

        private bool HasWhiteFrame(Texture source, Color32[] pixels)
        {
            if (frame == null || source.width != frameWidth || source.height != frameHeight)
                return false;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (frame[i] && pixels[i].a < 250)
                    return false;
            }
            return true;
        }

        // Game textures are usually not readable, so copy through a render texture.
        private static Color32[] Read(Texture source)
        {
            RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Color32[] pixels = copy.GetPixels32();
            Object.Destroy(copy);
            return pixels;
        }
    }
}
