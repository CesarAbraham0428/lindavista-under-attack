# Progresión de la pistola y nivel 1

## Flujo implementado

Un perfil nuevo empieza con 0 monedas, la pistola de nivel 1 y únicamente el nivel 1 desbloqueado. Marco y César comparten saldo, mejoras y niveles completados. La vida y el cargador se reinician en cada partida.

1. Los impactos aplican daño real a `EnemyHealth` y `PlayerHealth`.
2. Una muerte enemiga emite un único evento y crea una moneda física. Matar no acredita dinero automáticamente.
3. Pasar sobre la moneda guarda su valor en el monedero antes de eliminarla. Los contactos repetidos no duplican la recompensa.
4. El HUD muestra VIDA sin nombre, saldo, pistola, nivel, daño y cargador.
5. Después de eliminar la oleada completa, el jugador puede seguir caminando para recoger monedas. **FINALIZAR NIVEL** guarda la victoria y desbloquea el nivel siguiente.
6. La pantalla de resultado permite repetir/reintentar, mejorar la pistola o volver al menú. **Tienda** en el menú principal abre las mismas mejoras.

Las monedas ya recogidas y las mejoras se conservan al morir. Las monedas que siguen en el suelo se pierden al abandonar o reiniciar la escena. Finalizar el nivel también deja atrás cualquier moneda pendiente.

## Balance inicial editable

| Ajuste | Valor |
| --- | --- |
| Vida del jugador | 5; protección de 0,75 s tras un impacto |
| Daño de pistola por nivel | 2 → 3 → 4 |
| Coste de cada mejora | 25 y 60 monedas |
| Cadencia / alcance | 0,40 s / 18 unidades |
| Cargador / recarga | 12 balas / 1,20 s |
| Reserva | Ilimitada en esta entrega |
| Enemigos 1 / 2 / 3 / 4 | 10 / 15 / 30 / 50 puntos de vida |
| Moneda de enemigos 1 / 2 / 3 / 4 | 5 / 10 / 15 / 20 monedas |
| Oleada del nivel 1 | 6 enemigos tipo 1 y 4 tipo 2; recompensa total 70 |

La pistola se configura en `Assets/_Project/Resources/PistolDefinition.asset`; cada prefab enemigo tiene `EnemyLootDrop` y su recompensa. El prefab de moneda es `Assets/_Project/Resources/CoinPickup.prefab`. El sprite se genera con píxeles y está guardado en `Assets/_Project/Art/UI/Progression/coin.png`.

## Responsabilidades

- `PlayerHealth`: vida, protección tras daño y evento de muerte; `PlayerActions`: entrada y animaciones.
- `PlayerWeaponController` y `WeaponDefinition`: daño, cadencia, disparos, cargador y recarga.
- `EnemyHealth`: daño, escudo y muerte; `EnemyLootDrop`: recompensa física.
- `CoinPickup`: contacto con jugadores vivos y cobro único.
- `ProgressionRepository`: validación y transacciones; `ProgressionService`: perfil compartido y eventos.
- `ProgressionHUD` y `PistolUpgradeShop`: información y compras.
- `LevelOneEnemySpawner` y `GameFlowController`: enemigos pendientes/vivos, recogida final, victoria y derrota.

## Guardado

El perfil se guarda en `Application.persistentDataPath/progression-v1.json`, con versión, saldo, mejora de pistola, armas poseídas y niveles completados. Cada recogida, compra y finalización escribe primero un archivo temporal y después reemplaza el principal. El respaldo `.bak` permite recuperar un principal inválido. Si ambos archivos son inválidos, se conservan y se bloquean las transacciones, mostrando el error. Una operación fallida no altera el saldo ni consume la moneda.

`PlayerPrefs` sigue guardando selección de personaje/nivel y volumen, pero no el monedero.

## Comprobaciones en el Editor

- `Tools > Lindavista > Configure pistol progression`: vuelve a conectar componentes, recursos, prefabs y escenas; ejecutar fuera de Play Mode.
- `Tools > Lindavista > Verify progression storage`: 20 comprobaciones contra archivos temporales aislados.
- `ProgressionChecks.RunRuntime()`: comprobaciones de combate en Play Mode.
- `ProgressionChecks.BeginSceneChecks()` y después `CompleteSceneChecks()`: contactos físicos, recarga, oleada, victoria y compra en la interfaz. Esperar a que termine la presentación y se liberen todos los enemigos. `EndSceneChecks()` restaura el perfil real; salir de Play Mode también lo restaura.

Las comprobaciones usan un monedero temporal y no modifican el guardado real. Reiniciar Play Mode después de comprobar el flujo restaura los actores de la escena.

## Siguiente etapa

SMG y RPG están bloqueadas, aunque se mantienen sus animaciones y arte. Comprar otras armas, comprar munición y limitar la reserva requieren una siguiente entrega. Los niveles 2–4 conservan las composiciones existentes y se desbloquean secuencialmente; su dificultad todavía necesita ajuste jugando.
