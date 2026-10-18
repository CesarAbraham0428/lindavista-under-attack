# Rendimiento en Windows del 18 de octubre de 2026

Continúa la [revisión del 17 de octubre](rendimiento-2026-10-17.md), que se centró en Android. Esta entrega cierra lo que quedaba de Android con poco esfuerzo y optimiza la build de escritorio (Windows). No se cambiaron el balance, la jugabilidad ni ningún PNG.

## Punto de partida en Windows

Hasta ahora la build de PC usaba la configuración de plantilla:

- Calidad por defecto **Ultra** con `UniversalRP.asset`, con **HDR** y **sombras de luz principal** activos. El juego no usa post-procesado (la cámara tiene `Post Processing` desactivado) y el Renderer 2D no usa mapas de sombra 3D, así que el HDR solo añadía un búfer intermedio de color de coma flotante y una copia final por fotograma.
- **Pantalla completa sin bordes a resolución nativa.** En un monitor 4K el mundo se dibujaba a 3840×2160, con un sobredibujado de 3 a 4,6 pantallas. Esa es la carga de GPU más alta del juego en PC.
- **Sin límite de cadencia si el V-Sync está desactivado** (por ejemplo, forzado desde el panel del controlador): menús y pausa podían renderizar sin tope, al 100 % de la GPU.
- **Texturas:** BCn (DXT5/BC7), igual que ETC2, solo comprime texturas cuyo ancho y alto son múltiplos de 4. En Windows quedaban en RGBA32 sin comprimir 73 texturas por tamaño y otras 27 marcadas como «sin compresión»: unos 449 MB estimados.
- Backend de scripts **Mono**.

## Densidad del arte

El tamaño de render del mundo se eligió midiendo, en `Gameplay`, cuántos texels de cada sprite caben en la altura de la pantalla (cámara ortográfica de tamaño 5):

| Elementos | Texels por altura de pantalla |
| --- | --- |
| Marco, César, calle y muros | ≈ 1000 |
| Camión del jefe, fábrica | 1000–1100 |
| Grafitis, árboles, enemigos 1, 2 y 4 | 1500–3100 |
| Casas, postes, contenedores | 3700–4500 |
| Nubes, sol | 6200–7700 |

Por encima de unas 1000 líneas, los personajes y el suelo ya se están ampliando. Por eso, por encima de 1440 líneas, renderizar el mundo a resolución nativa no añade detalle a lo principal.

## Cambios aplicados

1. **Escala de render del mundo en pantallas grandes** (`Scripts/Escritorio/RendimientoEscritorio.cs`).
   - Solo en el reproductor de Windows, macOS y Linux; no actúa en el Editor ni en Android.
   - En `Gameplay` dibuja el mundo con un máximo de 1440 líneas y lo amplía: escala 1 hasta 1440p, 0,667 en 4K y 0,5 en 5K y 8K. En 4K se dibujan 2,25 veces menos píxeles del mundo.
   - Los lienzos Screen Space Overlay (HUD, números de daño, controles, mira) siguen a resolución nativa.
   - Los menús usan lienzos Screen Space Camera, así que en ellos la escala se mantiene en 1 y no se suavizan.
   - Igual que `RendimientoAndroid`, trabaja sobre una copia del asset URP y no modifica el original. Se reevalúa al cambiar de escena, de tamaño de ventana o de frecuencia de pantalla.
2. **Cadencia de respaldo.** Con V-Sync activo (Ultra usa `vSyncCount = 1`), la pantalla marca el ritmo y no cambia nada. Si el V-Sync está desactivado, limita `targetFrameRate` a la frecuencia del monitor: 59,94 → 60, 143,86 → 144, con un máximo de 360.
3. **URP de escritorio sin HDR ni sombras de luz principal** (`Settings/UniversalRP.asset`). Coincide con lo que ya usa `AndroidPipeline`, revisado visualmente en la validación de Android. La única luz es una global blanca de intensidad 1, así que no hay valores por encima de 1 que perder.
4. **BC7 en las 27 texturas comprimibles** (`Editor/StandaloneTextureSetup.cs`, menú **Tools > Lindavista > Configure/Verify desktop texture compression**).
   - Son las mismas 27 que en Android usan ETC2: hojas de César y del enemigo 3 sin escudo, píldoras, atlas de proyectiles, impacto del francotirador, botín y UI de ajustes.
   - BC7 ocupa 8 bits por píxel, la cuarta parte de RGBA32, y es el formato BCn de mejor calidad.
   - Solo cambia el bloque `Standalone` de cada `.meta`, con la misma comprobación de preservación que la herramienta de Android (hash del PNG y YAML fuera de Standalone idéntico). La resolución no cambia.
   - Con Windows como destino activo, `Verify()` confirmó el formato importado BC7 en las 27.
5. **Las cachés de muzzles ya no retienen hojas de animación.**
   - Problema: `PlayerMuzzle` y `EnemyMuzzle` guardaban en un diccionario estático el `Sprite` como clave. Una referencia estática administrada impide que `Resources.UnloadUnusedAssets()` libere ese sprite y su textura, así que después de jugar con César sus hojas seguían en memoria en una partida con Marco, y lo mismo con los enemigos de un nivel anterior.
   - Cambio: ahora la clave es el identificador de instancia (`GetInstanceID`), que no retiene el recurso.
   - Afecta a PC y a Android y es la causa más probable del pendiente «Hojas de César en el nivel 1» de la revisión anterior, porque el Editor tiene desactivada la recarga de dominio y los estáticos sobreviven entre sesiones de Play Mode.
   - En el Editor no se pudo confirmar la liberación: el Editor carga las hojas bajo demanda y las retiene por su cuenta (después de forzar la descarga, el controlador de César sí se libera, pero sus sprites no, con o sin el cambio).
6. **Escena `Testing` retirada.** El archivo ya se había borrado, pero seguía en `EditorBuildSettings`. Se quitó de la lista de build y de las comprobaciones de nombre de escena en `GameFlowController` y los HUD. También se quitaron las teclas de prueba **Q**/**E** de `PlayerActions` y la escena se retiró de `MenuSceneBuilder`, `ProgressionSceneSetup` y `EnemyPresentationChecks`.

## Evaluado y no aplicado

- **IL2CPP en Windows.** Suele reducir el tiempo de CPU de los scripts frente a Mono, y el módulo `win64_player_*_il2cpp` está instalado. Pero en este equipo no hay Visual Studio con la carga **Desarrollo para el escritorio con C++**, que IL2CPP necesita para compilar, y cambiar el backend rompería la build. Se mantiene Mono. Para activarlo: instalar esa carga en Visual Studio y en **Player > Other Settings > Scripting Backend** elegir IL2CPP para Windows.
- **Sprite Atlas para las 73 texturas que no son múltiplo de 4.** Es la única forma de comprimirlas en PC sin tocar los PNG: el atlas tiene dimensiones válidas para BC7. Se descartó por ahora por tres motivos:
  - Ocho scripts leen `sprite.texture` o su rectángulo (HUD, muzzles, proyectiles, derrota) y habría que revalidarlos.
  - Un atlas también reemplaza la textura en Android.
  - Esas texturas ocupan unos 346 MB en Windows: aceptable en cualquier GPU con 2 GB o más, o en una gráfica integrada con 8 GB de RAM.
  Es el siguiente paso si se busca menos memoria o cargas más rápidas en PC.
- **Mipmaps en casas, postes y nubes.** Se dibujan reducidos de 3,7 a 7,7 veces sin mipmaps. Tenerlos mejoraría el aliasing y el uso de caché de textura, pero añaden un 33 % de memoria y cambian el aspecto. No se aplicó.
- **Graphics Jobs y Direct3D 11.** Con 50–61 draw calls de media, el hilo de render no es el cuello de botella. Se mantiene la API automática (Direct3D 12 y después 11).

## Comprobaciones ejecutadas

En el Editor (Unity 6000.3.23f1), con el perfil aislado de `AndroidValidationSession`, se ejecutaron las comprobaciones que la revisión anterior dejó sin repetir. Todas pasaron:

| Comprobación | Resultado |
| --- | --- |
| `ComprobacionesMoviles.Ejecutar` | `CONTROLES_MOVILES_OK` |
| `AndroidLifecycleChecks.RunRuntime` | 16 aserciones |
| `ShotVisualChecks.VerifyPlayerMuzzles` | 72 disparos reales, ambos personajes |
| `ShotVisualChecks.VerifyScreenShooting` | `SCREEN_SHOOTING_CHECKS_OK` |
| `ComprobacionesRendimientoCombate.Ejecutar` | `RENDIMIENTO_COMBATE_OK` |
| `CombatPoolingChecks.RunRuntime` | 3661 aserciones |
| `AndroidCombatPerformanceChecks.RunRuntime` | 417 aserciones |
| `DamageNumberChecks.RunRuntime` | 423 aserciones |
| `SniperShotChecks.Run` | 73 aserciones |
| `EnemyPresentationChecks.RunAssets` | 80 aserciones |
| `AndroidTextureSetup.Verify` | 100 texturas, formatos importados confirmados |
| `StandaloneTextureSetup.Verify` | 27 texturas BC7, formatos importados confirmados con Windows activo |

`RendimientoEscritorio.CalcularEscala` y `CalcularCadencia` se comprobaron con resoluciones de 720p a 8K y frecuencias de 0 a 500 Hz.

## Build de Windows

Se generó una build de Windows de 64 bits (Mono, no Development) fuera del proyecto y después se devolvió el Editor a Android.

- Resultado: `Build Finished, Result: Success`.
- Tamaño total: 516,7 MB. Las texturas son 416,9 MB (97,9 % de los recursos), los shaders 751 KB y las escenas 145 KB.
- Escenas incluidas: `MainMenu`, `CharacterSelection` y `Gameplay`. Solo se incluye un asset URP: el de Android no viaja en la build de PC.
- Se ejecutó 15 segundos en ventana de 1280×720 sin excepciones en `Player.log`.
- En este equipo (AMD Radeon integrada, 4 núcleos), el filtro de dispositivos de Unity rechaza Direct3D 12 y usa Direct3D 11. Es una GPU integrada como las que más dependen de la escala de render y de la memoria de texturas.

No se midieron FPS en el reproductor: la medición automatizada existente (`PerformanceBenchmark`) solo funciona en el Editor.

La build sin compresión pesa unos 517 MB porque la mayoría de las texturas siguen en RGBA32. Para distribuir el juego conviene elegir **LZ4HC** en **Build Profiles > Windows > Compression Method**: el instalador ocupa menos y se descomprime rápido al cargar. Ese ajuste se guarda en la configuración local del Editor, no en el proyecto, así que no se cambió aquí.

## Pendiente

- **Medir en el reproductor de Windows**: FPS, tiempo de GPU y memoria residente con el Profiler conectado a una Development Build, en 1080p y 4K, y comparar la escala de render 1 con 0,667.
- **Revisión visual en 4K** de la ampliación del mundo. El texto y la UI no se ven afectados.
- **Confirmar en un teléfono** que las hojas de César se liberan en una partida con Marco después de una con César (Memory Profiler).
