# Enemy 3 shield art

Cleanup: the obsolete `Enemy3_Base.png` and `Enemy3_Defeat_8f.png` images and their import metadata have been removed after migrating their remaining animation/preview references to current sprites. The base controller preserves its existing clip IDs and now shares the current death artwork; shielded damage uses the shield action sheet. Mentions of the removed files below record the original generation references and prompts.

## Current walking and death animations

The current unshielded controller uses two dedicated eight-frame sheets generated with the built-in `image_gen` tool:

- `Assets/_Project/Art/Characters/Enemies/Enemy3_Unshielded_Walk_8f.png`: full alternating walk cycle at eight frames per second. Exact prompt and measured slicing rectangles: [Enemy3WalkArt.md](Enemy3WalkArt.md).
- `Assets/_Project/Art/Characters/Enemies/Enemy3_Unshielded_Defeat_8f.png`: recoil, collapse and horizontal resting body over 1.1 seconds, with no opacity fade. Exact prompts and measurements: [Enemy3DeathArt.md](Enemy3DeathArt.md).

The original combined unshielded sheet below continues supplying idle, punch and damage poses. `EnemyShieldVisualSetup.Build()` imports all three sheets and preserves the actor's height and ground anchor. `EnemyHealth` lets the complete fall play and holds the body before despawning; the natural Animator verification observed all eight fall poses and a 0.52-second visible final hold before despawning at 1.50 seconds.

Generated with the built-in `image_gen` tool, using the imagegen skill. No CLI/API fallback was used. Existing artwork was preserved; only new files were added. Generated alpha channels were copied unchanged.

## Final assets

- `Assets/_Project/Art/Characters/Enemies/Enemy3_Unshielded_8f.png` — 1536 × 1024 RGBA. Four columns × two rows, cells 384 × 512. Top row: four left-facing walking frames. Bottom row: guard, extended punch, retraction, recovery. All eight frames have no shield. 1,170,528 fully transparent pixels; alpha range 0–254.
- `Assets/_Project/Art/Characters/Enemies/Enemy3_BrokenShield.png` — 1024 × 1536 RGBA. Matching dark riot shield split into two large pieces with three small chips and a jagged diagonal gap. 1,003,178 fully transparent pixels; alpha range 0–254.

## Slicing measurements

Bounding boxes measured at alpha > 128, using top-left image origin and exclusive right/bottom coordinates. Every unshielded frame lies within its equal grid cell.

| Frame | Left | Top | Right | Bottom |
|---|---:|---:|---:|---:|
| Walk 0 | 83 | 128 | 330 | 456 |
| Walk 1 | 461 | 119 | 707 | 456 |
| Walk 2 | 838 | 123 | 1086 | 456 |
| Walk 3 | 1221 | 119 | 1470 | 456 |
| Punch 0 | 62 | 622 | 339 | 938 |
| Punch 1 | 400 | 626 | 732 | 938 |
| Punch 2 | 832 | 623 | 1104 | 938 |
| Punch 3 | 1215 | 622 | 1482 | 938 |
| Broken shield | 281 | 49 | 748 | 1488 |

For seamless runtime display, import pivots should normalize the boot baseline and PPU should match the intended existing character height. Walking artwork is 328–337 pixels tall; punching artwork is 312–316 pixels tall.

## References

- `Assets/_Project/Art/Characters/Enemies/Enemy3_Walk_8f.png`
- `Assets/_Project/Art/Characters/Enemies/Enemy3_Shield_HeavyPunch_8f.png`
- `Assets/_Project/Art/Characters/Enemies/Enemy3_Base.png`

## Exact prompts

### Unshielded sheet — initial generation

```text
Use case: identity-preserve
Asset type: Unity 2D side-scrolling beat-em-up animation sprite sheet.
Input images: first reference Enemy3_Walk_8f sheet is identity, body proportions and style reference; second Enemy3_Shield_HeavyPunch_8f is gesture/style reference; third Enemy3_Base is exact identity reference.
Primary request: make a NEW transparent unshielded sprite sheet of exactly this same muscular street thug WITHOUT ANY SHIELD. Keep his same angular square face, tan skin, angry eyes, black knit beanie, sleeveless black quilted hooded vest over white tee, black fingerless gloves, baggy dark cargo trousers with silver waist chain, chunky black combat boots and thick clean black outlines and shaded comic-game style. Character always faces LEFT in side/three-quarter profile, never right.
Composition/framing: landscape canvas 1536x1024 pixels. EXACTLY FOUR COLUMNS and TWO ROWS, 8 equal cells of 384x512 pixels. Exactly one separate full body in each cell. Each body centered x in its cell, full boots and hat entirely visible. Feet baseline at y=490 in each cell. Consistent head size and height about 440 pixels in every cell.
Animation contents: top row left-to-right 4 walking cycle frames without shield, forward stride, passing step, opposite stride, passing step, arms naturally swinging, hands fully drawn. Bottom row left-to-right 4 bare-handed punch frames facing LEFT: fists at guard windup; fist extended power punch toward left; fist retracting; guard recovery. Punch and walk frames same character scale, no size growth. Both hands visible naturally as poses allow. No shield remnants in any frame.
Scene/backdrop: genuinely transparent alpha everywhere outside the characters. Do not draw ground, cast shadows, gray or black fill, checkerboard or backdrop.
Constraints: retain reference clothing, colors, face, mature muscular proportions, black beanie in every frame. Full body game sprites arranged on precise equal grid. No text, labels, grid lines, borders, letters, watermark, extra objects. Do not crop any limbs or boots. No shields, guns, swords or props.
```

### Unshielded sheet — intermediate spacing pass

```text
Use case: precise-object-edit
Asset type: exact animation sprite sheet for Unity.
Input: the attached sprite sheet is the edit target, preserve all eight existing unshielded Enemy3 poses and character design.
Primary request: correct ONLY the packing/spacing of this sheet. Keep the entire sheet exactly 1536 by 1024 pixels with exactly FOUR COLUMNS by TWO ROWS. The columns have boundaries x=0,384,768,1152,1536; rows y=0,512,1024. Every character MUST be entirely within its own rectangle with at least 20 pixels of empty transparent padding on both left and right. In particular, bottom row second character extended punch must fit entirely inside its own cell x384..768 with clear transparent space before x384 and after x768. Make that punching torso slightly more side on so both glove and boot fit; no silhouette overlaps with neighboring cells. Preserve constant body/head scale comparable to top row and comparable 440-pixel character heights, all facing LEFT, all complete full bodies and boots. Feet baseline y=490 and 1002, same across each row. Do not add shields.
Backdrop: true transparent alpha, including spaces between all eight sprites, preserve transparency. Keep drawing style, black beanie, face, vest, tee, chain, gloves, baggy cargo trousers and combat boots unchanged. No labels, grid lines, text or ground shadows.
```

### Unshielded sheet — final packing pass

```text
Use case: identity-preserve
Asset type: production game sprite sheet.
Input is an edit target sprite sheet. Preserve its eight unshielded Enemy3 characters and exact face, clothing, black cap, comic shading and poses. Critical correction: SHRINK ALL EIGHT FIGURES to seventy-five percent (75%) of current visual size and then reposition each figure at the CENTER of its own one-eighth cell. Keep canvas1536x1024 with exactly4columns2rows of384x512. There must be HUGE EMPTY GAPS between every sprite. Each figure only about330pixels TALL, and maximum width330pixels even during the punch. Every single sprite must be smaller, NOT only the punching sprite. Do not leave the figures at their original large size.
Exact centers x192,576,960,1344 in both rows. Feet baselines y450 and962. Thus top of each character near y120 in top row and y632 in bottom row; each has blank space over cap and beneath boots. The extended punch in bottom row second cell is fully contained x410..742 and y632..962 with transparent margin, no fist or boot crossing into neighbors.
Top row4 walking frames; bottom row4 punch frames. All facing left and full-body visible. True transparent alpha. No shields, backdrop, ground shadows, gridlines, labels or text. Keep identity/appearance invariant and only reduce/space the sprites.
```

### Broken shield

```text
Use case: stylized-concept
Asset type: isolated Unity 2D game sprite for a broken shield falling effect.
Input image: Enemy3_Walk_8f is reference for the shield's exact design and comic game rendering style, not a layout to copy.
Primary request: draw ONLY ONE broken tall charcoal-black riot shield matching the enemy's riot shield from the reference. The shield is a narrow tall rectangular black ballistic shield with beveled round corners, thick dark rim, small square light blue-gray viewport near top, metal rivets. It has a BIG CLEAR JAGGED CRACK running diagonally across its middle, with two large near-separated pieces and three tiny chips around the central crack; keep the silhouette as one overall recognizable upright shield. A visible small gap between upper and lower halves, held in the same overall vertical placement, conveying breakage. It will be animated falling by code. No hands, arms, person or character, no additional shields.
Composition/framing: one shield centered on a portrait canvas1024x1536 with generous transparent padding; full shield from top to bottom entirely visible. Shield approx 400pixels wide and950pixels tall. Side/three-quarter view with small right bevel, same as reference. Bold clean black ink outlines, charcoal gray planes, crisp shaded hand-drawn beat-em-up art. No text, no insignia, no watermark.
Backdrop: genuinely transparent alpha outside the shield and between its broken pieces and chips, no gradient, no scene, no floor or shadows.
```


