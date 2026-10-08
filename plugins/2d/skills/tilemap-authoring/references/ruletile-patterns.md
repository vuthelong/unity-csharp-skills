# Rectangular RuleTile Patterns

Legend: `.` = This (neighbor must be this tile), `X` = don't care, `*` = the tile itself. Rows read top to bottom, cells left to right.

## Fixed template (47 rules)

Also the known-pattern set used to filter segmentation output (`TilemapRuleTileCreateFromSegment.IsKnownPattern`, `RuleTileTemplates.FixedPatterns`). Sort order in the generated tile is by This count, descending.

| ID | Pattern | This | Tile |
|---|---|---|---|
| 0 | `. . . / . * . / . . .` | 8 | Fully surrounded (inner tile) |
| 1 | `X . . / . * . / . . .` | 7 | Top left corner missing |
| 2 | `. . X / . * . / . . .` | 7 | Top right corner missing |
| 3 | `. . . / . * . / X . .` | 7 | Bottom left corner missing |
| 4 | `. . . / . * . / . . X` | 7 | Bottom right corner missing |
| 5 | `X . X / . * . / . . .` | 6 | Top corners missing |
| 6 | `. . . / . * . / X . X` | 6 | Bottom corners missing |
| 7 | `X . . / . * . / X . .` | 6 | Left corners missing |
| 8 | `. . X / . * . / . . X` | 6 | Right corners missing |
| 9 | `X . . / . * . / . . X` | 6 | Top Left and Bottom Right diagonal corners missing |
| 10 | `. . X / . * . / X . .` | 6 | Top Right and Bottom Left diagonal corners missing |
| 11 | `X . X / . * . / X . .` | 5 | Top Left, Top Right and Bottom Left corners missing |
| 12 | `X . X / . * . / . . X` | 5 | Top Left, Top Right and Bottom Right corners missing |
| 13 | `X . . / . * . / X . X` | 5 | Top Left, Bottom Left and Bottom Right corners missing |
| 14 | `. . X / . * . / X . X` | 5 | Top Right, Bottom Left and Bottom Right corners missing |
| 15 | `X . X / . * . / X . X` | 4 | Cross Section |
| 16 | `X X X / . * . / . . .` | 5 | Flat edge (Bottom) |
| 17 | `. . . / . * . / X X X` | 5 | Flat edge (Top) |
| 18 | `. . X / . * X / . . X` | 5 | Flat edge (Left) |
| 19 | `X . . / X * . / X . .` | 5 | Flat edge (Right) |
| 20 | `X X X / . * . / . . X` | 4 | L-Shape, Point Right |
| 21 | `. . X / . * X / X . X` | 4 | L-Shape, Point Bottom |
| 22 | `X . . / . * . / X X X` | 4 | L-Shape, Point Left |
| 23 | `X . X / X * . / X . .` | 4 | L-Shape, Point Top |
| 24 | `X X X / . * . / X . .` | 4 | L-Shape Inverse, Point Left |
| 25 | `X . . / X * . / X . X` | 4 | L-Shape Inverse, Point Bottom |
| 26 | `. . X / . * . / X X X` | 4 | L-Shape Inverse, Point Right |
| 27 | `X . X / . * X / . . X` | 4 | L-Shape Inverse, Point Top |
| 28 | `X X X / . * . / X . X` | 3 | T-Shape, face down |
| 29 | `X . X / . * . / X X X` | 3 | T-Shape, face top |
| 30 | `X . X / . * X / X . X` | 3 | T-Shape, face left |
| 31 | `X . X / X * . / X . X` | 3 | T-Shape, face right |
| 32 | `X X X / . * . / X X X` | 2 | Horizontal bridge |
| 33 | `X . X / X * X / X . X` | 2 | Vertical bridge |
| 34 | `X X X / X * . / X . .` | 3 | Corner piece, Bottom Right |
| 35 | `X X X / . * X / . . X` | 3 | Corner piece, Bottom Left |
| 36 | `. . X / . * X / X X X` | 3 | Corner piece, Top Left |
| 37 | `X . . / X * . / X X X` | 3 | Corner piece, Top Right |
| 38 | `X X X / X * . / X . X` | 2 | Edge end, Bottom Right |
| 39 | `X X X / . * X / X . X` | 2 | Edge end, Bottom Left |
| 40 | `X . X / . * X / X X X` | 2 | Edge end, Top Left |
| 41 | `X . X / X * . / X X X` | 2 | Edge end, Top Right |
| 42 | `X X X / . * X / X X X` | 1 | Single isolated tile, Left |
| 43 | `X X X / X * . / X X X` | 1 | Single isolated tile, Right |
| 44 | `X . X / X * X / X X X` | 1 | Single isolated tile, Top |
| 45 | `X X X / X * X / X . X` | 1 | Single isolated tile, Bottom |
| 46 | `X X X / X * X / X X X` | 0 | Center |

## Rotated template (15 rules)

Use when the art is drawn once and Unity rotates it (`m_RuleTransform = Rotated`). Matches `RuleTileTemplates.RotatedPatterns`.

| ID | Pattern | Transform | Tile |
|---|---|---|---|
| 0 | `. . . / . * . / . . .` | Fixed | Fully surrounded (inner tile) |
| 1 | `X . . / . * . / . . .` | Rotated | One corner missing |
| 2 | `X . X / . * . / . . .` | Rotated | Two corners missing |
| 3 | `X . . / . * . / . . X` | Rotated | Two diagonal corners missing |
| 4 | `X . X / . * . / . . X` | Rotated | Three corners missing |
| 5 | `X . X / . * . / X . X` | Fixed | Cross Section |
| 6 | `X X X / . * . / . . .` | Rotated | Flat edge |
| 7 | `X X X / . * . / . . X` | Rotated | L-Shape |
| 8 | `X X X / . * . / X . .` | Rotated | L-Shape Inverse |
| 9 | `X X X / . * . / X . X` | Rotated | Isolated edge |
| 10 | `X X X / . * . / X X X` | Rotated | Vertical/Horizontal bridge |
| 11 | `X X X / X * . / X . .` | Rotated | Corner piece |
| 12 | `X X X / X * . / X . X` | Rotated | Edge end |
| 13 | `X X X / . * X / X X X` | Rotated | Single isolated tile |
| 14 | `X X X / X * X / X X X` | Fixed | Center |

## Applying a template to a new tileset

Slice the texture into one sprite per configuration (47 for Fixed, 15 for Rotated), map each sprite to its rule by pattern, and confirm the RuleTile ends up with every rule in the template. Missing configurations fall back to `m_DefaultSprite`.
