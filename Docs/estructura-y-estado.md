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
- `PlayerActions`: entrada, apuntado y animaciones; `PlayerHealth`: vida; `PlayerWeaponController`: disparo, daño y cargador.
- `CombatProjectile`: barrido de impacto y aplicación de daño a enemigos.
- `BasicEnemy` y `EnemyHealth`: selección de objetivo, movimiento, ataque, salud, escudo y derrota enemiga.
- `GameFlowController`: derrota global por salud agotada o llegada de un enemigo a la entrada, bloqueo de controles y opciones para reiniciar o volver al menú.
- `SelectedCharacterBootstrap` y `CharacterLevelSelectionController`: persistencia y activación del personaje/nivel elegidos.
- `SelectedLevelEnvironment`: colores del cielo, sprites celestes y visibilidad de grupos decorativos llamados `Daño_Nivel_2` a `Daño_Nivel_4`; esos grupos no implementan daño al jugador.
- `CameraFollow2D`: presentación inicial de la oleada en tramo 5, paneo hacia la entrada y seguimiento horizontal del jugador.
- `PlayerHealthHUD`: interfaz de vida generada durante Play Mode en `Testing` y `Gameplay`.
- `MobileControlsHUD` y `TouchControlRegion`: botones y apuntado táctiles en `Testing` y `Gameplay`.
- `ProgressionService`, `ProgressionRepository`, `CoinPickup`, `EnemyLootDrop`, `ProgressionHUD` y `PistolUpgradeShop`: recompensas físicas, perfil guardado e interfaz de progresión. Ver [detalle](progresion-nivel-1.md).

## Estado conocido

1. `Gameplay` presenta la oleada del nivel elegido en el tramo 5 y libera enemigos progresivamente hacia la entrada. Se comprobó la secuencia y el flujo de victoria del nivel 1 en Play Mode; el ritmo y la dificultad requieren ajuste jugando. `Testing` conserva cuatro enemigos para probar variantes y animaciones.
2. Los proyectiles de pistola tienen un sprite sencillo y los disparos de los enemigos a distancia aplican daño instantáneo sin proyectil visible. El daño de muro no crea agujeros nuevos.
3. Los baches, grafitis, impactos y la mayoría del escenario son elementos estáticos precolocados; el avance de cámara los va revelando.
4. Los horarios son variantes elegidas antes de entrar a `Gameplay`; no hay transición de hora en tiempo real.
5. El HUD táctil se genera en `Testing` y `Gameplay`; se puede previsualizar en Editor con **F9**.
6. **Q** y **E** son teclas de prueba limitadas a `Testing`.
7. `Testing` está habilitada en `ProjectSettings/EditorBuildSettings.asset`, por lo que hoy forma parte de la lista de escenas de build.

## Rutina recomendada

Usar `Testing` para experimentar con daño, animaciones, enemigos y controles. Cuando una mecánica esté validada, integrar los mismos cambios en `Gameplay` y revisar allí el personaje y nivel elegidos. No copiar elementos de prueba sin comprobar si llevan componentes, referencias o ajustes particulares de la escena.
