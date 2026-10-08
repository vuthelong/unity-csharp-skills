# Rule Tiles

Package `com.unity.2d.tilemap.extras`. Classes `RuleTile`, `IsometricRuleTile`, `HexagonalRuleTile` (all derive from `RuleTile`), plus `RuleOverrideTile` / `AdvancedRuleOverrideTile` for reskinning an existing rule set.

## TilingRule fields

| Field | Meaning |
|---|---|
| `m_Neighbors` (`List<int>`) | Condition per position: `TilingRuleOutput.Neighbor.This` (1) or `NotThis` (2) |
| `m_NeighborPositions` (`List<Vector3Int>`) | Relative cell for each condition (same index as `m_Neighbors`) |
| `m_RuleTransform` | `Fixed`, `Rotated`, `MirrorX`, `MirrorY`, `MirrorXY`, `RotatedMirror`: extra orientations the rule also matches |
| `m_Output` | `Single`, `Random`, `Animation` |
| `m_Sprites` (`Sprite[]`) | One entry for `Single`; may hold `null` for a template |
| `m_ColliderType` | `Tile.ColliderType` (`None`, `Sprite`, `Grid`) |
| `m_GameObject` | Optional instantiated prefab |

Tile-level: `m_DefaultSprite` (used when nothing matches), `m_DefaultColliderType`, `m_TilingRules`.

A position that is absent from `m_NeighborPositions` is "don't care". There is no DontCare constant.

## 3x3 neighbor mapping (rectangular and isometric)

```
Pattern index        Vector3Int
[0] [1] [2]   (-1, 1,0) (0, 1,0) (1, 1,0)
[3] [*] [5]   (-1, 0,0)    ---   (1, 0,0)
[6] [7] [8]   (-1,-1,0) (0,-1,0) (1,-1,0)
```

`IsometricRuleTile` uses the same positions in cell space.

## Pattern string format

`[TopRow] / [MiddleRow] / [BottomRow]`, each row three space-separated symbols, top to bottom, left to right:
- `.` matches the center color -> `Neighbor.This`
- `X` does not -> don't care (omitted)
- `*` the center (always index 4)

Example: `X X X / X * X / . . .` = only the bottom row must be this tile.

## 3x3 color-grid segmentation (`SpriteSegment3x3Grid`)

1. Split the sprite's `rect` into 3x3 cells (`width / 3`, `height / 3`).
2. Find the center cell's majority color (most frequent `Color32`).
3. For each outer cell, compute the share of pixels within `colorTolerance` of that color (RGB and alpha).
4. Share >= `matchThreshold` -> `.`, else `X`. Output rows top to bottom (texture Y is flipped).

| `matchThreshold` | Use |
|---|---|
| 0.5 | Lenient; mixed regions |
| 0.75 (default) | Mostly solid fills |
| 0.9 | Very uniform art |

`colorTolerance` defaults to 0.04 (per channel, 0..1). Raise it for gradients or noisy textures; lower it for limited palettes. The texture must be readable (the generator toggles Read/Write for you). Works best with distinct terrain vs. background colors; transparent pixels count as their own color.

The same function documents sprite structure in a compact form, e.g. for tests or cataloging.

## From patterns to a RuleTile

1. Analyse every sprite -> pattern.
2. Filter: keep only the 47 known patterns (`IsKnownPattern`) unless the user asks to keep non-standard ones (`filterToKnownPatterns: false`). Unknown patterns are usually noise or decoration tiles.
3. Dedupe: identical patterns keep the first sprite.
4. Build each rule: `.` adds `(This, position)`; `X` adds nothing; `m_Output = Single`; `m_Sprites = { sprite }`; `m_RuleTransform = Fixed`; collider `Sprite`.
5. Sort by specificity (count of `This`) descending.
6. Assign to `m_TilingRules`; `m_DefaultSprite` = sprite of the last (least specific) rule.
7. `EditorUtility.SetDirty`, `AssetDatabase.CreateAsset` / `SaveAssets`.

```csharp
var rules = TilemapRuleTileCreateFromSegment.CreateRuleTileFromSprites(sprites, matchThreshold: 0.75f, colorTolerance: 0.04f);
var ruleTile = ScriptableObject.CreateInstance<RuleTile>();
TilemapRuleTileCreateFromSegment.ApplyRulesToTile(ruleTile, rules);
AssetDatabase.CreateAsset(ruleTile, "Assets/Tiles/Ground_RuleTile.asset");
```

## Empty templates

`RuleTileTemplates.CreateEmpty<RuleTile>(path, useRotatedTemplate)`:
- `false`: 47 Fixed rules (every edge/corner variant drawn explicitly).
- `true`: 15 rules with `Rotated` transforms (art drawn once, rotated by Unity); use when the tileset is rotation-symmetric.

Each rule gets `m_Sprites = new Sprite[1]` (a `null` slot) so the user can drop sprites in the Inspector. Use `CreateEmpty<IsometricRuleTile>` for isometric. Do not use these templates for `HexagonalRuleTile`; see the hexagonal table.

## Hexagonal rule tiles

Six neighbors instead of eight, positions depend on the grid's offset coordinates. See `hexagonal-ruletile-patterns.md`.
