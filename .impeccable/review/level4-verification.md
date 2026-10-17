# Level 4 — evidence and composition comparison

Scope: narrow native Unity extension in the existing game identity. The user fixed
the boss-right/cinematic-bar composition, approved a generated preview, then asked
to implement it. No new identity, illustration or generated background is shipped.

## Execution record

- Approved reference: `exec-02954f42-3b69-4e49-9d76-a645fc7c68a7.png` in the Codex
  generated_images folder. Generation was preview-only. Actual UI references the
  pre-existing `Boss_Base.png` and `Resources/UI/WoodSign`, without editing their pixels.
- Implementation: `BossIntroduction`, `BossHealthHUD`, `BossTruck`, `BossProjectile`;
  spawner/camera integration and serialized `LevelFourAssets`/`BossTruck` prefab.
- Asset validation: `LevelFourChecks` (22 assertions). Missing-only setup is available
  through the Tools menu; it does not modify portrait import settings.
- Runtime evidence: isolated Unity 6000.3.23f1 copy, temporary progression repository;
  266 checks covering both characters, 24 minions + boss, cap, no premature victory,
  shot contact/misses, weapons against boss, pause, death, collection, full camera
  introduction, held final click and restoration. Test log is in the Codex workspace.
- Regression: 1,628 combat assertions covering existing levels and shared effects.
- Visual captures: native Unity render, desktop 1920x1080 and landscape-phone aspect
  2340x1080, at `Codex/2026-09-13/s/level-four-preview/`. These are not emulator or
  Android-hardware captures. Fixture scenery uses existing assets and a built-in
  material; the shipped truck uses the same URP material reference as Enemy4.

The Impeccable context launcher was unavailable on this Windows session. There is
no automated build-phase state, pixel-diff score or Android-device pass to assert.
This is the manual native evidence record, not a fabricated successful script run.

## Approved reference versus built UI

Coordinates below are normalized viewport anchors from implementation; the comp
positions are visual comparisons, not claims of pixel-identical reproduction.

| Region | Approved reference | Implemented and captured |
|---|---|---|
| Portrait | Large boss on right, cropped legs | Crop x=.49–1, y=.09–.935, original portrait enlarged without stretching |
| Dialogue | Lower left, about 56% width | x=.022–.58, y=.13–.425; original wording in three readable lines |
| Speaker | Wooden EL JEFE sign above dialogue | Existing WoodSign x=.03–.245, y=.41–.50 |
| Letterbox | Solid top/bottom black bands | Top .935–1, bottom 0–.09, full canvas width |
| Continuation | Bottom center, unobstructed | Safe-area x=.10–.90, y=.012–.075, click/tap plus keyboard support |
| Sentence count | 1/4, one advance per click | Same 1/4 state; debounce and final release confirmed in runtime |
| Boss health | Bottom center only in combat | Safe-area x=.27–.73, y=.025–.128; separate screen-space root avoids truck scale inheritance |

Expected adaptations: real level scenery instead of the generated imagined street;
incumbent LilitaOne instead of generated lettering; original full portrait rather
than a redrawn face; existing wooden plaque/frame rather than new raster decoration.

## Review and limits

Independent visual review found no material visual failure in the two intro captures;
it requested documentation/evidence, now supplied by PRODUCT.md, DESIGN.md,
`.impeccable/design.json` and this record. Independent engineering review confirmed
the pause-before-intro and narrow-camera fixes and the shipped material reference.
The specialist agent type was unavailable; reviews used fresh general-agent contexts.
Actual Android-device touch ergonomics, safe-area cutouts, frame rate and subjective
combat difficulty still require device/player testing. No Git commit or push is made.

Final verdict: **ship**, scoped to the two scored introduction documentation/manual-
evidence fixes, which the independent reviewer marked resolved after reopening both
intro captures. This does not assert an independently rerun combat test or Android
device pass. The final boss-combat capture was inspected in the build thread and
shows the 300/300 red bar bottom-center after moving its canvas to a separate root.
