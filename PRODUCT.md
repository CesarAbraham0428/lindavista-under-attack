# LINDAVISTA: UNDER ATTACK

Unity 2D side-scrolling action prototype set in Linda Vista. The repository README describes character selection (Marco or Cesar), pistol combat, enemy coin pickups and weapon upgrades, with MainMenu, CharacterSelection, Gameplay and Testing scenes.

## Platform and existing UI

This is a Unity game using UnityEngine.UI and the Input System, not an HTML application. Existing input paths include keyboard/mouse and touch. The shared progression canvas uses a 1920 x 1080 virtual reference resolution with CanvasScaler ScaleWithScreenSize / Expand. Existing safe-area helpers adapt content to Screen.safeArea. Visual constraints and their source values are recorded in [DESIGN.md](DESIGN.md).

## Approved Level 4 scope

[Docs/Level4.md](Docs/Level4.md) approves a cinematic presentation over gameplay at 03:00: black letterbox bands, the original boss portrait on the right, wood sign and dialogue on the left, four complete sentences advanced by click, tap, Space or Enter. The HUD is hidden for the presentation and its last input is consumed before gameplay. A red, bottom-centred health HUD accompanies the boss encounter.

This extends the existing cartoon UI; it does not establish a replacement visual identity. Keep Assets/_Project/Art/Characters/Enemies/Boss_Base.png intact, including its metadata. The generated composition is an approved reference, not shipped game art or a replacement background. This records asset usage authority, not a new authorship or licensing claim.

## Handoff boundaries and evidence

This documentation closes the documentation portion of the finish-review gap. It does not constitute a gameplay approval, completed visual review or raster-provenance audit. All gameplay scripts remain owned by the main agent. This handoff writes only PRODUCT.md, DESIGN.md and .impeccable/design.json; it does not change global source, scripts, scenes, art or metadata.

Evidence: README.md; Docs/Level4.md; Assets/_Project/Scripts/{CartoonUI,ProgressionUI,BossIntroduction,BossHealthHUD}.cs; Assets/_Project/Scripts/Interfaz/AreaSeguraUI.cs; Docs/movil-y-rendimiento.md. Product context is extracted from these sources, without a new branding interview.

Unity Editor/Game View render captures are not actual Android-device proof. No Android build, device execution, frame-rate guarantee or completed device validation is asserted here.
