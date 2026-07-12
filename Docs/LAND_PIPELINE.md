# Land Pipeline — sourcing 3D environment assets from the Characters factory

The environment analog of `CHARACTER_ART_PIPELINE.md`. The `../Characters` factory repo grows a
**3D land pack** the same way it grows the character pack: headless Blender generates the meshes
and textures, an FBX/PNG handoff crosses the repo seam (North-Star has no glTF importer), and an
editor-only builder turns the handoff into a Unity scene. Neither repo references the other at
build time — the contract is **files + a manifest**.

> This is distinct from the factory's older 2D `land-gen` (biome PNGs for the vanilla-JS
> TypeQuest game). That pipeline produces sprites, not importable geometry, and does **not** feed
> North-Star. This doc covers the new **3D** land pack only.

---

## The two moving parts

### Factory side (`../Characters`) — generate + export

- **`tools/blender/build_land_props.py`** — procedurally builds 8 low-poly stylized props
  (`tree_oak`, `tree_pine`, `rock_large`, `rock_small`, `bush`, `stump`, `mushroom_cluster`,
  `grass_tuft`) and 4 tileable ground textures (grass/dirt/rock/path, FFT low-pass noise so they
  tile by construction). Deterministic (fixed seed) — re-runs are byte-identical. Unity targeting
  matches `export_fbx.py`: 1 unit = 1 m, +Y up, −Z forward, flat Principled materials.
- **`tools/export-northstar-land.mjs`** (`npm run export-northstar-land`) — runs the Blender
  script into `content/model-pack/land/` and writes `northstar-land-manifest.json` (per-prop
  scatter density/scale/footprint + texture roles). A **gated** `--copy --yes` copies the pack
  into the Unity project (refuses without `--yes`; target overridable via `NORTHSTAR_LAND_ART`).

```bash
# in ../Characters — run with Node 20 (system node may be too old for cpSync)
PATH="/opt/homebrew/opt/node@20/bin:$PATH" npm run export-northstar-land
PATH="/opt/homebrew/opt/node@20/bin:$PATH" \
  NORTHSTAR_LAND_ART="<unity-repo>/Assets/_Game/Art/Environment" \
  npm run export-northstar-land -- --skip-blender --copy --yes
```

Override the Blender binary with `BLENDER_BIN` (default `/Applications/Blender.app/...`).

### Consumer side (North-Star) — import + author the scene

- Pack lands under `Assets/_Game/Art/Environment/` (`environment/*.fbx`, `textures/*.png`,
  `northstar-land-manifest.json`). FBX are Git-LFS tracked (`.gitattributes`).
- **`NorthStarLandManifest`** (`NorthStar.Editor.Smoke`) — typed view of the manifest.
- **`SliceEnvironmentBuilder`** — authors a zone from the pack:
  - **Terrain**: a Unity `Terrain` bowl — flat gameplay plateau at world Y = 0, Perlin hills
    beyond it, a raised rim so the player can't walk off, slope-and-noise-splatted layers built
    from the 4 ground textures.
  - **Prop scatter**: deterministic placement in the ring between plateau and rim, at each prop's
    manifest density; capsule/sphere colliders on trees and large rocks; marked static.
  - **Grass**: the `grass_tuft` mesh fed to the Landscape module's GPU-instanced
    `ProceduralFoliage` (one instanced URP material, saved under `Art/Environment/Generated`).

Generated Unity sub-assets (TerrainData, TerrainLayers, grass material) live in
`Assets/_Game/Art/Environment/Generated/`.

---

## How it's consumed in the slice

`SliceSceneBuilder` calls `SliceEnvironmentBuilder` for both zones:

| Zone | Terrain | Props | Grass |
|---|---|---|---|
| `SCN_VerticalSlice` (Terrain_Slice) | 120 m bowl, plateau r=26 | 176 scattered | ~6 000 tufts |
| `SCN_Zone02` (Terrain_Outpost) | 80 m bowl, plateau r=20 | 16 scattered | ~3 000 tufts |

The whole chain runs headlessly via `EndToEndBuilder.BuildAll` (greps `[Env]` / `END_TO_END_BUILD_OK`).

---

## What does **not** port / what's next

- **Biome variety** — one prop set + one texture family. More biomes (snow, desert, ruins) mean
  more Blender prop builders + texture roles.
- **Authored layouts** — scatter is procedural/random within a ring; hand-placed landmarks,
  paths, and set-dressing are not generated.
- **LODs / occlusion** — props are single-LOD; large open zones will want LOD groups.

See `Docs/ROADMAP.md` for the tracked backlog (issues on GitHub).

---

## Status

| Piece | State |
|---|---|
| This doc | ✅ |
| Blender prop/texture generator | ✅ `tools/blender/build_land_props.py` (8 props + 4 textures, deterministic) |
| Land handoff orchestrator | ✅ `npm run export-northstar-land` + gated `--copy --yes` (manifest written) |
| Manifest reader + scene builder | ✅ `NorthStarLandManifest` + `SliceEnvironmentBuilder` (terrain/props/grass) |
| Consumed in the slice | ✅ both zones on real terrain; **headless build green** (`[Env]` logs, gate 253/253) |
| More biomes / LODs / authored layouts | ⛔ backlog (`Docs/ROADMAP.md`) |
