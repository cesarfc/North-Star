# Character Art Pipeline — sourcing rigged meshes from the Characters/Meshy platform

How North-Star's **Module 2 (Character & Customization)** gets its missing content —
rigged base body, armor, and hair meshes — from the sibling **Characters** repo's
`chibi_humanoid_v1` 3D platform (an AI image→3D pipeline wired to **Meshy.ai**).

> Repos: this game lives at `North-Star/`; the content factory lives at
> `../Characters/` (`/Users/cesarcordero/Projects/cesarfc/Characters`). The Characters
> repo is the **source of art**; North-Star **consumes** the exported meshes. Neither
> references the other at build time — the handoff is files (FBX) + a frozen contract.

---

## Why the two fit

North-Star's `CharacterCustomizer` swaps `SkinnedMeshRenderer.sharedMesh` per equipment
slot **on one shared skeleton** — armor is a mesh swap, not a prefab spawn
([CharacterCustomizer.cs](../Assets/_Game/Scripts/Character/CharacterCustomizer.cs)).
That is exactly what the Characters pipeline produces: garments **skinned to a single
frozen rig** (`chibi_humanoid_v1`), plus static gear attached to named **sockets**. The
two systems were designed for each other without knowing it. Three bridges close the gap
(below): **FBX export**, **facing/handedness**, and a **bone-rebind** on the Unity side.

---

## The frozen contract (`chibi_humanoid_v1`)

Source of truth: `../Characters/content/model-pack/rig/chibi_humanoid_v1.contract.json`
and `../Characters/packages/model3d-schema/src/ids.ts`. **Never changes within v1.x** —
every mesh, socket, and (future) animation binds to these names.

| Property | Value |
|---|---|
| Rig id | `chibi_humanoid_v1` |
| Bones | 24, snake_case (`root, hips, spine_01, chest, neck, head, shoulder_*, upper_arm_*, lower_arm_*, hand_*, fingers_*, thumb_*, upper_leg_*, lower_leg_*, foot_*`) |
| Sockets | 10 (`socket_head_top, socket_face, socket_back, socket_chest, socket_hand_r, socket_hand_l, socket_weapon_tip, socket_cast, socket_ground, socket_pet_follow`) |
| Up axis | **+Y** |
| Facing | **−Z** (three.js/glTF forward) — see [Facing](#bridge-2--facinghandedness) |
| Scale | **1 unit = 1 m**; base character height = **1.0 u** |
| Origin | ground-center between the feet |
| Proportion profiles | `chibi` (big-head) and `standard` (humanoid) — the shipped contract is `standard` |

---

## What already exists to consume (`../Characters/content/model-pack/`)

| Asset | Files | Skinned? | North-Star use |
|---|---|---|---|
| Base rigged body | `assets/3d/characters/base_chibi_v1/exports/base_chibi_blender_v1.glb` | **skinned, 24 joints** | The shared skeleton the character prefab is built on |
| Worn garments — placeholder | `equipment/{iron_chestplate,cloth_tunic,leather_pants,leather_boots}.glb` | **skinned, 20 joints** | `ArmorData.mesh` for Chest / Legs / Feet — **swap-ready** |
| Worn garments — Meshy-textured | `equipment/{…}_meshy.glb` | **static (0 joints)** | textured art, but **needs a re-skin** before it can be a skinned swap (see below) |
| Socket gear | `equipment/{iron_helmet,hair_*,iron_sword,fire_staff,round_shield,green_cape}{,_meshy}.glb` | static | attach to a **bone-child socket** (MeshRenderer parented to a bone), not a skinned swap |
| Rig contract | `rig/chibi_humanoid_v1.contract.json` | — | Bone-name reference for the rebind |
| Face expressions | `faces/chibi_toon/*.png` | — | Optional, beyond Module 2 scope |

**Verified (2026-06-19):** the base body FBX carries all 24 bones, 10 sockets, 49 skin
clusters, embedded textures; the placeholder chestplate FBX carries 20 bones + 40 skin
clusters, and **its 20 joints are a perfect subset of the base's 24** (the base's extra
4 are fingers/thumbs) — so a rebind-by-name maps a garment onto the base skeleton with
zero leftover bones. The Meshy worn garments are currently **static** (raw image→3D
output, not yet skinned).

**Two variants per piece:** the plain `.glb` is the FREE code-gen placeholder and is
**skinned to the rig** (rig-ready for the SkinnedMeshRenderer swap); the `_meshy.glb` is
the AI-textured art but is a **static mesh**. To get a textured *and* skinned garment,
run the Meshy mesh through Characters' `skin_garment.py` (a Characters-side follow-up) —
until then, use the skinned placeholders for the swap path and the Meshy meshes as static
attached/decorative gear or as the texture source.

**The pipeline itself is the highest-value asset.** New armor is generated on demand and
is mesh-swap-compatible by construction:
- `npm run gen-equipment -- --id=<id> [--subject="…"] --billed` — concept (gpt-image) →
  Meshy image→3D → Blender fit/skin → `equipment/<id>.glb`. **Billing double-gated** by
  `--billed` + keys; `--mesh <file.glb>` processes a provided mesh FREE.
- `npm run gen-model-3d -- --style=<id> [--base] --billed` — a whole new base body.
- `npm run make-equipment-glb` / `npm run blender-rig` — FREE code-gen / Blender baseline.

---

## Slot mapping (Characters' 10 slots → North-Star's 5 + hair)

North-Star: `enum EquipmentSlot { Head, Chest, Legs, Hands, Feet }` plus a separate
`HairStyleData` ([SharedTypes.cs](../Assets/_Game/Scripts/Core/SharedTypes.cs:8)).

| Characters slot | North-Star target | Example mesh |
|---|---|---|
| `helmet` | **Head** (`ArmorData`) | `iron_helmet` |
| `armor_overlay` / `body_outfit` | **Chest** (`ArmorData`) | `iron_chestplate`, `cloth_tunic` |
| `legs` | **Legs** (`ArmorData`) | `leather_pants` |
| `feet` | **Feet** (`ArmorData`) | `leather_boots` |
| `hair` | **HairStyleData** | `hair_short/long/spiky` |
| `weapon_right_hand`, `offhand_left_hand`, `cape_or_back_item`, `pet` | *not modelled by the customizer yet* — these are **socket** gear (attach to a bone child), not skinned swaps | `iron_sword`, `round_shield`, `green_cape` |

**Gaps:** Characters has no *worn* `Hands` garment (it uses the hands for socket weapons
instead), and North-Star's customizer doesn't model weapon/offhand/cape sockets. Those are
future work — leave Hands empty for now, or add socket attachment to the customizer.

---

## The three bridges

### Bridge 1 — FBX export (Characters side) ✅
Unity has **no glTF importer** in `Packages/manifest.json`, so `.glb` can't be imported
directly. Rather than add a package, we export **FBX** from Blender (carries skeleton +
skin weights + embedded textures, zero new Unity packages). Characters' own export-profile
note already flagged *".fbx is opt-in per engine."*

- Tool: `../Characters/tools/blender/export_fbx.py` + `npm run export-fbx` (FREE, headless
  Blender). `npm run export-fbx -- --all` batches the whole model-pack into
  `content/model-pack/fbx/{characters,equipment}/*.fbx`; `npm run export-fbx -- <file.glb>
  [out.fbx]` does one file.
- Unity-targeted FBX settings: `apply_unit_scale`, `FBX_SCALE_UNITS` (so Unity reads 1 u =
  1 m with no 0.01 rescale), `axis_up='Y'`, `axis_forward='-Z'`, `add_leaf_bones=False`,
  `primary_bone_axis='Y'`, embedded textures.

### Bridge 2 — facing/handedness
Characters faces **−Z** (three.js forward); Unity's forward is **+Z**, left-handed. The FBX
axis conversion handles up/handedness, but the character may still import facing **away**
from the camera. On import, enable **Bake Axis Conversion** in the FBX importer (Model tab)
or apply a 180° yaw on the character root. **Verify once** on the first imported body, then
it's consistent for the whole roster.

### Bridge 3 — bone-rebind (North-Star side)
`CharacterCustomizer.ApplyArmorVisual` currently swaps `sharedMesh` only. A skinned-mesh
swap deforms correctly **only if** the renderer's `bones[]`/`rootBone` already match the
new mesh's bindposes by index. Mismatched bone order → the armor renders mangled. The fix
is a **rebind-by-bone-name** step that remaps each swapped mesh onto the character's shared
skeleton after assigning `sharedMesh`. See Module 2 follow-up.

---

## Importing into Unity (one-time setup)

1. Run the FBX export in Characters: `cd ../Characters && npm run export-fbx -- --all`.
2. Copy `content/model-pack/fbx/` into `Assets/_Game/Art/Characters/` (FBX prefixed per
   [CONVENTIONS.md](CONVENTIONS.md) on import, e.g. `PFB_`/mesh naming).
3. Import the **base body** FBX → it brings in the 24-bone armature. Build the character
   prefab around this skeleton: one `SkinnedMeshRenderer` for the body, one per armor slot
   (Head/Chest/Legs/Feet), one for hair — all sharing the base armature's bones.
4. For each garment FBX, create an `ArmorData` asset: set `itemId` (lowercase-with-hyphens,
   per CLAUDE.md), `slot`, drag the imported `Mesh` + `Material[]`. `boneNames` fills itself —
   `CharacterBoneNameImporter` ([Editor](../Assets/_Game/Editor/CharacterBoneNameImporter.cs))
   auto-syncs it on FBX (re)import; force it any time via **Tools ▸ North-Star ▸ Character ▸
   Sync boneNames** or the asset's gear-icon context menu.
5. Wire the renderers into `CharacterCustomizer._armorRenderers` (slot → renderer),
   `_hairRenderer`, and `_skeletonRoot` (the imported armature root) in the Inspector.

### Smoke test (prove the seam with one piece)
Use the **skinned placeholder** `iron_chestplate.glb` (not `_meshy`, which is static):
export `base_chibi_blender_v1` + `iron_chestplate` as FBX → import → bind one `ArmorData`
→ `Equip` it in `SCN_VerticalSlice`. If the chestplate deforms with the body, format,
facing, and the rebind all check out and the rest of the roster is just repetition.

---

## What does **not** port directly

- **Animations.** Characters' `rig-3d` clip library (idle/walk/run/attack/…) is a pure-TS
  rotation-channel sampler, **not** Unity `.anim`. Its value to Unity is the **standardized
  bone names** → Mixamo / Unity-humanoid retargeting "just works." Author/retarget Unity
  clips against the imported skeleton; don't try to port the TS clips.
- **Style/proportions.** The default base is **chibi** (big-head). Use the **`standard`**
  proportion profile for an adventure-game humanoid (the shipped contract is already
  `standard`). Confirm the look fits North-Star's art direction before generating a roster.

---

## Status

| Bridge | State |
|---|---|
| (c) This doc | ✅ |
| (a) FBX export path in Characters | ✅ `tools/blender/export_fbx.py` + `npm run export-fbx` — base body FBX verified (24 bones/10 sockets/49 clusters/textures) |
| (b) Bone-rebind in `CharacterCustomizer` | ✅ `SkeletonRebinder` (rebind-by-name on swap) + `boneNames` on `ArmorData`/`HairStyleData`; **EditMode gate 249/249** |
| `boneNames` auto-importer | ✅ `CharacterBoneNameImporter` — auto-fills on FBX (re)import + `Tools ▸ North-Star ▸ Character` menu / context menu |
| Armor smoke-test builder | ✅ `ArmorSmokeTestBuilder` — **Tools ▸ North-Star ▸ Character ▸ Build Armor Smoke Test** assembles `SCN_ArmorSmokeTest` + pre-equips via the real rebind |
| Rig-swap rebind verified on real assets | ✅ **headless PASS** — 20/20 armor bones resolved against the imported body skeleton (`missing=0`), chest renderer bound to 20 bones |
| Visual check (fit / co-deform / facing) | ⛔ your eyes — open `SCN_ArmorSmokeTest`; if the character faces away, tick **Bake Axis Conversion** on the FBX (currently `bakeAxisConversion=0`) |

> **Art is in Git LFS:** `*.fbx`/`*.glb` are tracked via `.gitattributes`; a clone needs
> `git lfs` installed to pull the real meshes (otherwise they arrive as pointer files).

> **`boneNames` population:** the rebind engages only when `ArmorData.boneNames` /
> `HairStyleData.boneNames` is filled (bind-pose order). `CharacterBoneNameImporter` does this
> automatically — on FBX (re)import it resolves each asset's `mesh` → source FBX →
> `SkinnedMeshRenderer.bones` (via `SkeletonRebinder.ExtractBoneNames`) and writes the names.
> Re-run on demand from **Tools ▸ North-Star ▸ Character ▸ Sync boneNames**. Empty = safe
> legacy `sharedMesh`-only swap (unchanged behavior).
