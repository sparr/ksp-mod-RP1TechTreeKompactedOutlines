# RP1TechTreeKompactedOutlines

Draws colored outlines around each blue sky tech and the techs it unlocks in RP-1 TechTree Kompacted.

A KSP 1.12 plugin mod: .NET Framework 4.8, built with the `dotnet` SDK and the
`KSPBuildTools` NuGet package.

## How it works

- Nothing about the tree is configured. `Outlines.cfg` holds only line style.
- The blue sky techs are the ids typed `Materials` or `Electronics` in RP-1's
  `KCT_TECH_NODE_TYPES` config node, read from `GameDatabase`. The plugin has
  no reference to RP-1's assembly.
- Blue sky techs with the same left edge form one era. Its color is sampled
  from the frame of their icons by `IconColorRemover.TryGetFrameColor`.
  Sampled colors differ from the PNGs by a few units per channel because the
  game compresses textures. A blue sky tech whose icon has no frame joins the
  era whose span it sits in.
- An era spans every node whose left edge is at or after the era's blue sky
  techs and before the next era's.
- Each blue sky tech gets a box: the bounding rectangle of itself and its
  descendants (through `RDNode.parents`, any depth) inside its era's span.
  Boxes of one era overlap where a node descends from both blue sky techs.
  Descent is limited to the span because every materials science tech descends
  from the previous one.
- The addon listens to `RDTechTree.OnTechTreeSpawn`, which fires before the
  nodes run `Start`. It waits until every node in `RDController.nodes` has its
  graphics, then draws. RP-1 removes empty nodes from that list in the same
  event, so they are never outlined.
- Outlines are `OutlineGraphic` children of
  `RDController.gridArea.gridBackground`, the same transform that holds the
  nodes. Zoom, pan, and clipping come from that parent. They are placed as
  first siblings, so they render behind nodes and lines.
- `OutlineShape` builds each outline's path: the padded bounding rectangle,
  with a notch in the left side for every visible node inside the bounds that
  is not a member, then rounded corners. `OutlineGraphic` strokes the path as
  a mesh with feathered edges. In the stock layout only one notch occurs, for
  `supersonicFlightRP0` in the post-war materials science box.
- A direct child of a blue sky tech that sits right of the era's span gets a
  tab out of the box's right side. The only case is `capsulesNF`, which
  requires `materialsScienceNF` but is placed in the Colonization column.
  Direct children left of the span are not handled: four techs that require
  `materialsScienceColonization` sit in the Near Future column.
- `IconColorRemover` replaces the icon of every node that is not a blue sky
  tech with a copy whose colored pixels are transparent. The layout
  mod's colored icons are the plain white icon plus a solid frame. The white
  era's frame cannot be found by color, so the frame's pixel positions are
  learned from a colored icon and removed by position.
- They cannot go behind the grid ruling. The ruling is that transform's own
  `Image`, which is opaque, so anything behind it is invisible, and its
  children always render in front of it. A translucent fill inside an outline
  would tint the ruling and show through the node backgrounds.
- Node rectangles are measured from each node button's world corners, not from
  `pos` and `scale`. Node transforms carry the inverse of the canvas scale, so
  their local sizes are not in grid units.
- The plugin references no other mod's assembly. It depends on the layout mod
  through `:NEEDS[RP1KompactedTechTree]` on the config node, and on RP-1
  through the `KCT_TECH_NODE_TYPES` data. Without either, nothing is drawn.

## Layout

- `src/RP1TechTreeKompactedOutlines/` is the plugin source and its `RP1TechTreeKompactedOutlines.csproj`.
- `GameData/RP1TechTreeKompactedOutlines/` is the folder players install. Config and the
  `.version` file are tracked there. `Plugins/`, `LICENSE`, and `README.md`
  in it are build output and are ignored.
- `Directory.Build.props` holds everything shared by all projects: metadata,
  the mod folder location, and the import of `Version.props`.
- `RP1TechTreeKompactedOutlines.props.user` (ignored, copied from `RP1TechTreeKompactedOutlines.props.user.example`)
  holds the path to the local KSP install.
- `netkan/RP1TechTreeKompactedOutlines.netkan` is the CKAN metadata to submit to `KSP-CKAN/NetKAN`.
  Once it is merged there, the copy upstream is the one that counts.
- `.github/workflows/` uses `KSPBuildTools`, pinned to the same version as the
  NuGet package in the csproj. Bump both together. `build.yml` and
  `validate.yml` call its reusable workflows. `create-release.yml` calls its
  actions directly, because the reusable release workflow names the zip after
  the repository and the zip is named after the mod.

## Building

`dotnet build` at the repo root. The game install is only read for reference
assemblies. Nothing is deployed into it. On Linux, `KSPBuildTools` does not
detect a Steam install, so `RP1TechTreeKompactedOutlines.props.user` or `KSP_ROOT` is required.

`KSPBuildTools` generates the `[KSPAssembly]` attribute from the version, and a
`[KSPAssemblyDependency]` for each `ModReference` in the csproj. A dependency
that is missing at runtime is silent: KSP skips the plugin with a warning in
`KSP.log` and nothing on screen.

## Version

The version is written in two tracked files, `Version.props` and
`GameData/RP1TechTreeKompactedOutlines/RP1TechTreeKompactedOutlines.version`. Both are generated from the
`.versiontemplate` file beside them. Do not edit the generated files. Edit the
templates when the content around the version needs to change, and regenerate.

The `create-release` workflow regenerates both. To do it locally, use
`update-version.sh` from the `KSPBuildTools` repository:

```sh
update-version.sh 1.2.3 .versiontemplate
```

## Changelog

`CHANGELOG.md` is in the format `yaclog` reads. Add entries as bullets under
`## Unreleased`. The release workflow renames that section to the version and
date, and uses `yaclog-ksp` to generate `GameData/RP1TechTreeKompactedOutlines/changelog.cfg` for the
Kerbal Changelog mod. Do not edit `changelog.cfg` by hand. The table at the
top of `CHANGELOG.md` supplies that file's metadata. `yaclog-ksp` silently
drops any row whose key or value contains a hyphen.

## Releasing

Run the `create-release` workflow from the GitHub Actions tab with a version.
It updates the changelog and version files, commits, tags, pushes to the
branch it was run on, builds in Release, and creates a draft GitHub release
with the zip attached. Publishing the draft is a manual step.

## Config files

- Name ModuleManager passes after the mod: `@PART[...]:FOR[RP1TechTreeKompactedOutlines]`. Use
  `:NEEDS[OtherMod]` for patches that support another mod.
- Braces go on their own lines. KSP's parser silently misreads
  `NODE { a = b }` written on one line.
- State written at runtime goes in `GameData/RP1TechTreeKompactedOutlines/PluginData/`, which
  ModuleManager does not read.
- Localization tags are `#LOC_RP1TechTreeKompactedOutlines_...` in
  `GameData/RP1TechTreeKompactedOutlines/Localization/`.
