# Enemy 3 unshielded walk art

Created with the built-in `image_gen` tool using the imagegen skill. No CLI/API fallback was used. Generated alpha was copied unchanged into the project.

## Final asset

`Assets/_Project/Art/Characters/Enemies/Enemy3_Unshielded_Walk_8f.png`

- 1536 × 1024 pixels, RGBA.
- Eight walk frames arranged left-to-right, top row then bottom row.
- Four-column, two-row composition. Use the measured variable rectangles below because the last frame extends eight pixels beyond its nominal grid cell.
- 940,289 fully transparent pixels; 565,956 pixels have alpha > 240; alpha range 0–254.
- Same left-facing Enemy 3, no shield: black beanie, dark padded hooded vest, white tee, fingerless gloves, dark cargo trousers, silver chain and heavy boots.
- Visible differences include heel contact, bent loading knees, narrow passing poses with lifted feet, rear heel elevation and arm swing. Passing frames 2 and 6 have substantially narrower leg silhouettes than contact frames.
- Runtime import should normalize feet and character height because the generator used approximately 447–453-pixel bodies rather than the requested 340 pixels.

## Slicing measurements

Connected-component bounds measured at alpha > 128, using top-left image coordinates and exclusive right/bottom values. The actual components are separate and their rectangles do not overlap. Provide a small padding around each measured box, and use the boot baseline as the vertical pivot.

| Frame | Left | Top | Right | Bottom |
|---|---:|---:|---:|---:|
| 0 | 28 | 34 | 371 | 482 |
| 1 | 435 | 34 | 741 | 481 |
| 2 | 851 | 33 | 1037 | 485 |
| 3 | 1157 | 32 | 1500 | 482 |
| 4 | 17 | 530 | 382 | 977 |
| 5 | 436 | 530 | 751 | 976 |
| 6 | 849 | 529 | 1048 | 977 |
| 7 | 1144 | 529 | 1507 | 976 |

## Reference assets

- `Assets/_Project/Art/Characters/Enemies/Enemy3_Unshielded_8f.png`
- `Assets/_Project/Art/Characters/Enemies/Enemy3_Walk_8f.png`

## Exact final prompt

```text
Use case: identity-preserve
Asset type: production Unity 2D beat-em-up WALK animation sprite sheet.
Input images: Image1 Enemy3_Unshielded_8f is EXACT character identity and illustration reference only; do NOT copy its static-legged poses. Image2 Enemy3_Walk_8f is outfit/boot/body reference, remove the shield entirely.
Primary request: create EIGHT DISTINCT consecutive keyframes of a TRUE left-facing natural WALK CYCLE for this same muscular man. The walk must visibly articulate hips, knees, ankles, soles and opposite arm swing. This MUST animate as walking with real alternating steps, never stationary sliding or eight minor variations of one wide stance. In passing poses the boots come close together and one knee lifts forward, making the silhouette much narrower than in contact poses. Keep cap/head/face and torso consistent, same square face, tan skin, black beanie, black quilted hooded vest, white tee, fingerless black gloves, baggy black cargo trousers with silver chain, chunky black laced combat boots, black comic outlines, cel shaded colors. No shield, props or weapons.
Exact phase ordering, reading left-to-right then next row:
TOP LEFT frame1: CONTACT A. Near leg strides forward LEFT, near heel contacts floor with toe raised; far leg extended back RIGHT with heel raised and only toe touching. Far arm forward, near arm back.
TOP SECOND frame2: DOWN A. Near foot now flat on floor ahead of hip supporting weight, near knee bends; rear far boot lifts off from toe, far knee bends and heel comes upward.
TOP THIRD frame3: PASSING A. Near leg straightens planted directly under hip, far thigh swings forward with knee clearly bent, lifted far boot passes beside planted calf; feet visually close together.
TOP RIGHT frame4: UP A. Near support leg pushes off behind hip with heel raised, far bent knee travels forward LEFT and far boot suspended forward preparing heelstrike; body slightly high.
BOTTOM LEFT frame5: CONTACT B. Far leg now fully forward LEFT with heel planted/toeup; near leg extended back RIGHT on toe. Near arm forward, far arm back. Distinct from frame1 because legs and arms swap.
BOTTOM SECOND frame6: DOWN B. Far forward foot flat supports weight and far knee bends; near rear leg bends, boot lifts off toe.
BOTTOM THIRD frame7: PASSING B. Far support leg under hip, near thigh moves forward, near knee clearly bent and near boot lifted beside planted calf, feet close together.
BOTTOM RIGHT frame8: UP B. Far leg pushes off behind with heel raised, near knee and boot suspended forward LEFT ready to loop into frame1.
Composition: canvas1536x1024, EXACT4columns x2rows, each cell384x512. One fullbody percell at xcenters192,576,960,1344. Characters about340pixels tall, maximumwidth320, ample transparent gaps. Ground baseline y450 top row and962 bottom row, planted boots consistent; head size exactly stable, hips centered same x eachcell, small natural torso bob only. Full hat/hands/boots within each cell. Side camera fixed, always facing LEFT. No growth/shrinking, no drifting offcenter. Clean genuine transparent alpha outside each solid opaque character; no scenery, ground, shadows, gridlines, labels, numbers, text, borders or watermark.
```

