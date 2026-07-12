# North-Star — Roadmap / Backlog

Where the game is and what's left. The end-to-end **vertical slice is complete and playable on
real, factory-generated assets** (`Docs/VERTICAL_SLICE.md`); every gameplay module is built,
merged, and covered by the headless gate (**EditMode 253/253**, `END_TO_END_BUILD_OK`, standalone
macOS player builds).

The remaining work is tracked as GitHub issues. This file is the human-readable index; the issues
are the source of truth.

## Done (this pass)

- Boot → main-menu flow (`SCN_Boot`), New Game / Continue / Quit.
- Rigged, dressed character from the **character pack** (full wardrobe library built from
  `northstar-manifest.json`; visible armor/hair swaps at the station). — `Docs/CHARACTER_ART_PIPELINE.md`
- Terrain zones, prop scatter, and GPU-instanced grass from the new **3D land pack**. —
  `Docs/LAND_PIPELINE.md`
- Yarn Spinner 3.x dialogue (Elder Vane) that **starts** The Dying Spark quest.
- Player-driven turn battle (abilities via `CombatUnit.UseAbility`, enemy AI).
- Registered audio: SFX on events, per-stride footsteps, per-zone music crossfade.
- Zone-gate spawn-point teleport (with EditMode tests).

## Remaining — tracked issues

| # | Item | Type | Issue |
|---|------|------|-------|
| 1 | Ember shrines + complete **The Dying Spark** quest | content / slice | [#2](https://github.com/cesarfc/North-Star/issues/2) |
| 2 | Character animations (locomotion + combat) retargeted to `chibi_humanoid_v1` | content / art | [#3](https://github.com/cesarfc/North-Star/issues/3) |
| 3 | Enemy battle visuals from the factory enemy pipeline | content / art / pipeline | [#4](https://github.com/cesarfc/North-Star/issues/4) |
| 4 | Runtime socket-gear slots in `CharacterCustomizer` (weapon/offhand/back) | enhancement | [#5](https://github.com/cesarfc/North-Star/issues/5) |
| 5 | Replace placeholder audio with authored SFX + music | content / audio | [#6](https://github.com/cesarfc/North-Star/issues/6) |
| 6 | DOTween UI/camera polish (optional) | enhancement | [#7](https://github.com/cesarfc/North-Star/issues/7) |
| 7 | Cinemachine 3.x third-person free-look camera | enhancement / slice | [#8](https://github.com/cesarfc/North-Star/issues/8) |
| 8 | Expand the world beyond two zones (world map + spawn network) | enhancement / content / slice | [#9](https://github.com/cesarfc/North-Star/issues/9) |

### Suggested order

1. **#2 ember quest** — closes the one incomplete story beat; small, high value.
2. **#3 enemy visuals** + **#8 camera** — make combat and traversal *look* like a game.
3. **#4 animations** — the biggest visual upgrade; unblocks a shippable feel.
4. **#5 socket gear**, **#6 audio**, **#7 DOTween** — depth + polish.
5. **#9 world expansion** — scale the loop into a chapter (pairs with more land biomes).

> Pipeline backlog (more biomes / LODs / authored layouts, more character roster) lives in the
> respective pipeline docs and feeds #3/#8/#9 above as the factory packs grow.
