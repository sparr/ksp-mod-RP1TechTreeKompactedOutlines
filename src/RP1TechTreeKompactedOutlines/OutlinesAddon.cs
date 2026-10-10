using System.Collections;
using System.Collections.Generic;
using KSP.UI.Screens;
using UnityEngine;

namespace RP1TechTreeKompactedOutlines
{
    /// <summary>Draws the configured outlines behind the tech tree each time it is built.</summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class OutlinesAddon : MonoBehaviour
    {
        private const string LogPrefix = "[RP1TechTreeKompactedOutlines] ";
        private const string OutlineObjectName = "TechTreeOutline";

        // Frames to wait for the nodes to build their graphics before giving up.
        private const int MaxSetupFrames = 30;

        // Slack, in tech tree units, when comparing node positions.
        private const float Tolerance = 1f;

        // RP-1's config node mapping tech ids to types, and the types that mark a blue sky tech.
        private const string TechTypesNodeName = "KCT_TECH_NODE_TYPES";
        private static readonly HashSet<string> BlueSkyTechTypes = new HashSet<string> { "Materials", "Electronics", "Bluesky" };

        private OutlineSettings settings;
        private HashSet<string> blueSkyTechIDs;
        private readonly IconColorRemover iconColorRemover = new IconColorRemover();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            settings = OutlineSettings.Load();
            if (settings == null)
            {
                Debug.Log(LogPrefix + "No " + OutlineSettings.NodeName + " config found. Nothing will be drawn.");
                return;
            }
            blueSkyTechIDs = LoadBlueSkyTechIDs();
            if (blueSkyTechIDs.Count == 0)
            {
                Debug.Log(LogPrefix + "No blue sky techs found in " + TechTypesNodeName + ". Nothing will be drawn.");
                return;
            }
            RDTechTree.OnTechTreeSpawn.Add(OnTechTreeSpawn);
        }

        // RP-1 gives each tech a type. The blue sky techs are the materials science and
        // electronics research ones.
        private static HashSet<string> LoadBlueSkyTechIDs()
        {
            var techIDs = new HashSet<string>();
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(TechTypesNodeName))
            {
                foreach (ConfigNode.Value value in node.values)
                {
                    foreach (string type in value.value.Split(',', '|'))
                    {
                        if (BlueSkyTechTypes.Contains(type.Trim()))
                            techIDs.Add(value.name);
                    }
                }
            }
            return techIDs;
        }

        private void OnDestroy()
        {
            RDTechTree.OnTechTreeSpawn.Remove(OnTechTreeSpawn);
        }

        private void OnTechTreeSpawn(RDTechTree tree)
        {
            StartCoroutine(DrawWhenReady(tree));
        }

        // OnTechTreeSpawn fires before the nodes have run Start, which is where they are placed
        // in the grid and given their graphics.
        private IEnumerator DrawWhenReady(RDTechTree tree)
        {
            for (int frame = 0; frame < MaxSetupFrames; frame++)
            {
                yield return null;
                if (tree == null || tree.controller == null)
                    yield break;
                if (NodesReady(tree.controller))
                {
                    Draw(tree.controller);
                    yield break;
                }
            }
            Debug.LogWarning(LogPrefix + "Tech tree nodes were not ready after " + MaxSetupFrames + " frames.");
        }

        private static bool NodesReady(RDController controller)
        {
            if (controller.nodes == null || controller.nodes.Count == 0)
                return false;
            foreach (RDNode node in controller.nodes)
            {
                if (node == null || node.graphics == null || node.transform.parent != controller.RDNodeRoot)
                    return false;
            }
            return true;
        }

        private void Draw(RDController controller)
        {
            RectTransform grid = controller.gridArea.gridBackground;

            for (int i = grid.childCount - 1; i >= 0; i--)
            {
                Transform child = grid.GetChild(i);
                if (child.name == OutlineObjectName)
                    Destroy(child.gameObject);
            }

            var rects = new Dictionary<RDNode, Rect>();
            var byTechID = new Dictionary<string, RDNode>();
            foreach (RDNode node in controller.nodes)
            {
                rects[node] = NodeRect(node, grid);
                if (node.tech != null)
                    byTechID[node.tech.techID] = node;
            }

            var children = new Dictionary<RDNode, List<RDNode>>();
            foreach (RDNode node in controller.nodes)
            {
                if (node.parents == null)
                    continue;
                foreach (RDNode.Parent link in node.parents)
                {
                    RDNode parent = link?.parent?.node;
                    if (parent == null)
                        continue;
                    if (!children.TryGetValue(parent, out List<RDNode> list))
                        children[parent] = list = new List<RDNode>();
                    list.Add(node);
                }
            }

            var blueSky = new HashSet<RDNode>();
            foreach (string techID in blueSkyTechIDs)
            {
                if (byTechID.TryGetValue(techID, out RDNode owner))
                    blueSky.Add(owner);
            }
            // Only the blue sky techs keep their era color.
            var others = new List<RDNode>();
            foreach (RDNode node in controller.nodes)
            {
                if (!blueSky.Contains(node))
                    others.Add(node);
            }
            iconColorRemover.Apply(others);

            // Blue sky techs in the same column open one era, in the color of their icons'
            // frames. An era starts at the left edge of that column and runs to the start of
            // the next era.
            var eras = new List<Era>();
            var uncolored = new List<RDNode>();
            foreach (RDNode owner in blueSky)
            {
                if (!iconColorRemover.TryGetFrameColor(owner.icon?.texture, out Color color))
                {
                    uncolored.Add(owner);
                    continue;
                }
                float start = rects[owner].xMin;
                Era era = eras.Find(e => Mathf.Abs(e.start - start) < Tolerance);
                if (era == null)
                    eras.Add(era = new Era { color = color, start = start });
                era.owners.Add(owner);
            }
            eras.Sort((x, y) => x.start.CompareTo(y.start));

            // A blue sky tech whose icon has no frame joins the era whose columns it sits in.
            foreach (RDNode owner in uncolored)
            {
                Era era = eras.FindLast(e => e.start < rects[owner].xMin + Tolerance);
                if (era != null)
                    era.owners.Add(owner);
            }

            int drawn = 0;
            for (int i = 0; i < eras.Count; i++)
            {
                Era era = eras[i];
                float end = i + 1 < eras.Count ? eras[i + 1].start : float.MaxValue;

                // One box per blue sky tech, around it and the techs of this era that depend on
                // it, directly or through other techs. A tech that depends on two of the era's
                // blue sky techs is in both boxes.
                foreach (RDNode owner in era.owners)
                {
                    var members = new HashSet<RDNode> { owner };
                    Rect bounds = rects[owner];
                    foreach (RDNode node in Descendants(owner, children))
                    {
                        Rect rect = rects[node];
                        if (rect.xMin > era.start - Tolerance && rect.xMin < end - Tolerance)
                        {
                            members.Add(node);
                            bounds = Union(bounds, rect);
                        }
                    }

                    // A tech that requires this blue sky tech directly but sits in a later era's
                    // columns is reached by a tab out of the box's right side.
                    var bumps = new List<Rect>();
                    if (children.TryGetValue(owner, out List<RDNode> direct))
                    {
                        foreach (RDNode node in direct)
                        {
                            Rect rect = rects[node];
                            if (blueSky.Contains(node) || rect.xMin < end - Tolerance || !members.Add(node))
                                continue;
                            bumps.Add(Expand(rect, settings.bumpPadding));
                            // Keep the tab within the box's height.
                            bounds.yMin = Mathf.Min(bounds.yMin, rect.yMin);
                            bounds.yMax = Mathf.Max(bounds.yMax, rect.yMax);
                        }
                    }

                    // Techs inside the box that do not belong to it are cut out of its left side.
                    var cutouts = new List<Rect>();
                    foreach (KeyValuePair<RDNode, Rect> pair in rects)
                    {
                        if (!members.Contains(pair.Key) && pair.Value.Overlaps(bounds))
                            cutouts.Add(Expand(pair.Value, settings.cutoutGap));
                    }

                    CreateOutline(grid, Expand(bounds, settings.padding), cutouts, bumps, era.color);
                    drawn++;
                }
            }
            Debug.Log(LogPrefix + "Drew " + drawn + " outlines.");
        }

        private class Era
        {
            public Color color;
            public float start;
            public List<RDNode> owners = new List<RDNode>();
        }

        private static HashSet<RDNode> Descendants(RDNode root, Dictionary<RDNode, List<RDNode>> children)
        {
            var found = new HashSet<RDNode>();
            var pending = new Stack<RDNode>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                if (!children.TryGetValue(pending.Pop(), out List<RDNode> list))
                    continue;
                foreach (RDNode child in list)
                {
                    if (found.Add(child))
                        pending.Push(child);
                }
            }
            return found;
        }

        // The node button's rectangle in the grid's local space.
        private static Rect NodeRect(RDNode node, RectTransform grid)
        {
            var corners = new Vector3[4];
            ((RectTransform)node.graphics.button.transform).GetWorldCorners(corners);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 local = grid.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

        private static Rect Expand(Rect rect, float amount)
        {
            return Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);
        }

        private void CreateOutline(RectTransform grid, Rect box, List<Rect> cutouts, List<Rect> bumps, Color color)
        {
            var go = new GameObject(OutlineObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(OutlineGraphic));
            go.layer = grid.gameObject.layer;

            var rt = (RectTransform)go.transform;
            rt.SetParent(grid, false);
            // Behind every node and line. The grid's own image is the opaque ruled backdrop, so
            // nothing placed behind the grid itself would be visible.
            rt.SetAsFirstSibling();
            // Anchoring at the grid's pivot makes anchoredPosition a grid local position.
            rt.anchorMin = rt.anchorMax = grid.pivot;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = box.min;
            rt.sizeDelta = box.size;

            List<Vector2> corners = OutlineShape.Corners(box, cutouts, bumps, 2f * settings.thickness);
            List<Vector2> path = OutlineShape.Round(corners, settings.cornerRadius);
            for (int i = 0; i < path.Count; i++)
                path[i] -= box.min;

            var graphic = go.GetComponent<OutlineGraphic>();
            graphic.path = path;
            graphic.thickness = settings.thickness;
            graphic.raycastTarget = false;
            graphic.color = new Color(color.r, color.g, color.b, settings.lineAlpha);
        }
    }
}
