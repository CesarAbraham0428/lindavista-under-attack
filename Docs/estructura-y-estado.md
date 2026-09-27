# Estructura y estado del proyecto

## Carpetas principales

| Ruta | Contenido |
| --- | --- |
| `Assets/_Project/Scenes` | `MainMenu`, `CharacterSelection`, `Gameplay` y `Testing`. |
| `Assets/_Project/Scripts` | Movimiento, acciones del jugador, combate, selección, cámara y HUD. |
| `Assets/_Project/Animations` | Controladores de Animator y clips para jugadores y enemigos. |
| `Assets/_Project/Art` | Sprites de personajes, enemigos, escenarios, armas y menú. |
| `Assets/_Project/Settings` | Ajustes de URP 2D y asset de acciones de Input System. |
| `Docs` | Documentación funcional del proyecto. |

El proyecto usa Unity **6000.3.23f1**, Universal Render Pipeline 2D, Input System y Unity UI (`com.unity.ugui`). Las versiones de paquetes están declaradas en `Packages/manifest.json`.

## Scripts clave

- `PlayerMovement`: entrada horizontal, velocidad, límite del jugador y bloqueo al ser derrotado.
- `PlayerActions`: apuntado, selección y disparo de armas, recarga, salud y daño.
- `CombatProjectile`: barrido de impacto y aplicación de daño a enemigos.
- `BasicEnemy` y `EnemyHealth`: selección de objetivo, movimiento, ataque, salud, escudo y derrota enemiga.
- `SelectedCharacterBootstrap` y `CharacterLevelSelectionController`: persistencia y activación del personaje/nivel elegidos.
- `SelectedLevelEnvironment`: colores del cielo, sprites celestes y grupos de daño ambiental.
- `CameraFollow2D`: presentación inicial de la oleada en tramo 5, paneo hacia la entrada y seguimiento horizontal del jugador.
- `PlayerHealthHUD`: interfaz de vida generada durante Play Mode en `Testing` y `Gameplay`.
- `MobileControlsHUD` y `TouchControlRegion`: botones y apuntado táctiles; actualmente solo se crean en `Testing`.

## Estado conocido

1. `Gameplay` presenta la oleada del nivel elegido en el tramo 5 y libera enemigos progresivamente hacia la entrada. Los conteos de los cuatro niveles son ajustables. Falta comprobar la secuencia visualmente en Play Mode; `Testing` conserva cuatro enemigos para probar variantes y animaciones.
2. El arte de proyectiles no está conectado: el objeto de proyectil es lógico y no tiene sprite/trail. El daño de muro tampoco crea agujeros nuevos.
3. Los baches, grafitis, impactos y la mayoría del escenario son elementos estáticos precolocados; el avance de cámara los va revelando.
4. Los horarios son variantes elegidas antes de entrar a `Gameplay`; no hay transición de hora en tiempo real.
5. El HUD táctil de `MobileControlsHUD` se genera únicamente en `Testing`, aunque hay código para mostrarlo en Android y previsualizarlo en Editor con **F9**.
6. **Q** y **E** son teclas de prueba que también se procesan en `Gameplay` actualmente.
7. `Testing` está habilitada en `ProjectSettings/EditorBuildSettings.asset`, por lo que hoy forma parte de la lista de escenas de build.

## Rutina recomendada

Usar `Testing` para experimentar con daño, animaciones, enemigos y controles. Cuando una mecánica esté validada, integrar los mismos cambios en `Gameplay` y revisar allí el personaje y nivel elegidos. No copiar elementos de prueba sin comprobar si llevan componentes, referencias o ajustes particulares de la escena.
