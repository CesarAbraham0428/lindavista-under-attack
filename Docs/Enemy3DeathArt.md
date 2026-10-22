# Enemy 3 unshielded death art

Generated with the built-in `image_gen` tool through the imagegen skill. No CLI/API fallback was used. The selected output was copied unchanged into the Unity project; its generated alpha was preserved.

## Final asset

- `Assets/_Project/Art/Characters/Enemies/Enemy3_Unshielded_Defeat_8f.png`
- 1536 × 1024 RGBA, four columns × two rows, equal cells 384 × 512.
- Row-major animation: recoil, buckling knees, backward fall, hip contact, horizontal impact, settling, resting corpse, final resting corpse.
- No shield, blood, weapons, floor or other props.
- Alpha range: 0–254. Fully transparent pixels: 1,224,101.
- Selected generation: `C:\Users\lopez\.codex\generated_images\01a14c11-3d80-7bf0-b909-c6210bf5791d\exec-db39401a-bb3d-4f68-9987-b25253d35b33.png`

The old `Enemy3_Defeat_8f.png` has a shield in every pose and nonsequential poses, so it was not reused for unshielded death. The new sheet preserves the unshielded character's beanie, muscular build, black quilted vest, white T-shirt, cargo trousers, chain, gloves and boots.

## Slicing measurements

Bounding boxes are measured at alpha > 128. Cell-local coordinates use the top-left origin and exclusive right/bottom. All eight silhouettes are contained inside their own equal cell.

| Frame | Pose | Left | Top | Right | Bottom | Width | Height |
|---|---|---:|---:|---:|---:|---:|---:|
| 0 | Recoil | 37 | 143 | 288 | 495 | 251 | 352 |
| 1 | Knees buckle | 40 | 171 | 308 | 495 | 268 | 324 |
| 2 | Falling | 15 | 237 | 328 | 495 | 313 | 258 |
| 3 | Hip contact | 52 | 290 | 351 | 495 | 299 | 205 |
| 4 | Ground impact | 30 | 245 | 353 | 408 | 323 | 163 |
| 5 | Settling | 6 | 268 | 351 | 412 | 345 | 144 |
| 6 | Resting | 4 | 291 | 360 | 406 | 356 | 115 |
| 7 | Final resting pose | 11 | 287 | 359 | 405 | 348 | 118 |

Runtime import should normalize each frame's visible bottom to the same ground anchor, and use the first frame's 352-pixel body height to derive PPU. Do not derive death scale from the shorter height of a horizontal corpse: its body rotates, rather than shrinking.

## Reference images

- `Assets/_Project/Art/Characters/Enemies/Enemy3_Unshielded_8f.png` — exact in-game appearance.
- `Assets/_Project/Art/Characters/Enemies/Enemy3_Base.png` — supporting character identity.

## Exact prompts

### Initial generation

```text
Use case: identity-preserve
Asset type: production Unity 2D side-scrolling beat-em-up sprite sheet.
Input images: Enemy3_Unshielded_8f.png is the exact character, palette, outlines and on-screen scale reference; Enemy3_Base.png is supporting exact face/clothing reference.
Primary request: create a NEW dedicated, genuinely transparent eight-frame DEATH / FALL animation sprite sheet of this exact SAME muscular street thug AFTER his shield has already broken. He has NO SHIELD in any frame. Preserve the exact angular square face, tan skin, black knit beanie, sleeveless black quilted hooded vest over white short-sleeved T-shirt, black fingerless gloves, baggy dark charcoal cargo trousers with silver waist chain, and dark gray chunky combat boots, thick clean black outlines, crisp shaded comic-game style.
Composition/framing: 1536x1024 canvas, EXACTLY FOUR COLUMNS and TWO ROWS, 8 equal cells of 384x512. Read frames left to right across top row then bottom row. Exactly ONE complete character per cell. Empty transparent margins in every cell, all body parts fully contained, no clipping or overlap. Same head size and same body scale in all eight poses; standing character about 330 pixels tall. Top row floor baseline at y=456 and bottom row floor baseline at y=968. Each character/corpse centered x in its own cell at x=192,576,960,1344. Use bent knees and curled fall poses so a horizontal body fits a maximum width of about340 pixels, NEVER enlarge the head or body.
Animation, in strict progressive order:
Frame1 upper-left: facing LEFT, standing struck, head and chest recoil backwards toward the RIGHT, arms loosen.
Frame2: knees buckle visibly, hips sink, chest tilts farther backwards to the right, hands reach for balance.
Frame3: body falling backwards toward the RIGHT, hips near ground, torso strongly diagonal, one boot starts to lift.
Frame4 upper-right: hips strike ground, back still diagonal just before collapsing, legs bent, side-view motion clear.
Frame5 lower-left: body hits ground and becomes HORIZONTAL, head toward RIGHT and boots toward LEFT, knees bent slightly, no standing or crouching.
Frame6: settles onto his side/back, horizontal torso on floor, eyes shut, legs bent and arms relaxed.
Frame7: lies MOTIONLESS fully down on ground, head toward RIGHT, knees gently curled, both arms slack, eyes shut.
Frame8 lower-right: same final resting death pose as frame7, fully prone and motionless, natural anatomy.
The final four frames must be clearly LYING DOWN with the character's torso horizontal, not kneeling. The first four must visibly rotate and lower in a continuous coherent fall, not merely shrink or crouch. Keep the beanie on his head.
Backdrop: real transparent alpha everywhere outside the sprites, including all space between cells. Do NOT draw a background, dark glow, gradient, ground, floor line, dust, shadow, impact marks, text, labels, grid lines or border.
Avoid: ANY SHIELD OR SHIELD REMNANTS, weapons, props, gore, blood, different costume, duplicates within a cell, mirrored design, chibi proportions, perspective/scale changes, cropped boots or hat.
```

### Selected packing/scale refinement

```text
Use case: precise-object-edit
Asset type: production Unity animation sprite sheet.
The attached image is the edit target. KEEP THE EXACT EXISTING EIGHT DRAWN POSES, CHARACTER IDENTITY, FALL PROGRESSION, COLORS AND CLOTHING. Change ONLY packing, scale and positions.
Keep canvas1536x1024 with4columns2rows of384x512 cells, no labels or drawn grid.
Critical correction: SHRINK EVERY CHARACTER uniformly to 84% (eighty-four percent) of its present visual size. All8 bodies and head sizes must receive exactly the SAME reduction. The standing first character must become330pixels tall, NOT393pixels. Do not leave current large bodies. Keep poseangles and sameanatomicalproportions.
Then CENTER EACH reduced figure horizontally within its own384wide cell. Place the lowest visible edge of EACH figure, including every lying corpse, at baseline y456 in top row and y968 in bottom row. Thus top row standing cap near y126 and boots at456. Bottom row lying bodies' lowest glove or boot MUST touch y968, with their heads and torsos just above this baseline. Avoid the current bottom-row figures floating high in their cells.
The exact continuous pose sequence remains: upright backwards recoil, knees buckle, fallingbackwards, hipgroundcontact, horizontal groundimpact, horizontal settling, samefinal horizontal deadpose, samefinaldeadpose. Character remains no shield whatsoever.
Preserve alloriginal alpha transparency. Entirely transparent outside each character and between allcells. No background, no glow, no checkerboard, ground, shadows, text, labels, gridlines or additional details.
```

