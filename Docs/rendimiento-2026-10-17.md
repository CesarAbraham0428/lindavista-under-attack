# Revisión de rendimiento del 17 de octubre de 2026

Revisión de CPU, GPU y memoria de los cuatro niveles, con atención al combate intenso y al jefe final. Primero se midió, después se corrigió lo observado y por último se comparó en las mismas condiciones. **Todas las cifras proceden del Editor de Unity 6000.3.23f1 en Windows**, con Android como plataforma activa (calidad `Android` y `AndroidPipeline`). No se generó ninguna build ni APK y no se midió en un dispositivo. Las cifras de memoria son una estimación calculada a partir del formato importado para Android, no una lectura de memoria residente en un teléfono.

No se cambiaron el balance, la jugabilidad ni las imágenes originales: ningún PNG fue modificado.

## Método

`PerformanceBenchmark` (Editor) ejecuta cuatro escenarios fijos dentro de la sesión aislada `AndroidValidationSession`, que no gasta el perfil real:

| Escenario | Contenido |
| --- | --- |
| N1 Marco | Oleada del nivel 1 |
| N3 César | Escudos y subfusiles del nivel 3 |
| N4 Marco + jefe | Oleada final con el camión del jefe forzado desde el inicio |
| N4 César + jefe | Lo mismo con César (escopeta, francotirador, lanzagranadas) |

Condiciones comunes:

- Game View a 1920×1080 y controles táctiles visibles.
- 2 s de calentamiento y 30 s de grabación.
- Jugador invulnerable, con disparo continuo a la derecha y cambio de arma cada 4 s.
- Un enemigo que rebasa al jugador se elimina para que la partida no termine.

Se registra cada fotograma con `ProfilerRecorder` (PlayerLoop, scripts, física, animación, UI, GC y render). `AllocationProbe` lee la jerarquía del Profiler dentro de `PlayerLoop` para atribuir el tiempo propio y la basura generada (GC) a cada script, sin mezclarlos con el coste del propio Editor. El sobredibujado se estima sumando el área en pantalla de los sprites visibles.

Para repetir la medición, en Play Mode:

```csharp
AndroidValidationSession.Begin();
AndroidValidationSession.Resolution(1920, 1080);
PerformanceBenchmark.RunSuite("etiqueta");   // informe en Logs/Rendimiento/etiqueta.md
```

**Advertencia sobre los tiempos.** La primera referencia se grabó con el portátil enchufado y la primera medición «después», con batería (`BatteryStatus = 1`). Con batería, todo el equipo iba ~1,7× más lento, incluso en código que no se modificó. Por eso la comparación válida de tiempos es un A/B en la misma condición: los scripts anteriores (restaurados temporalmente con `git stash`) frente a los nuevos, ambos con batería y con las mismas texturas. La memoria y las asignaciones no dependen de la velocidad del equipo y se comparan con la referencia original.

## Resultados

### Memoria de texturas (estimación con formato Android)

| Escenario | Antes | Después |
| --- | --- | --- |
| N1 Marco | 292,2 MB | 112,2 MB |
| N3 César | 350,1 MB + 28,4 MB creados en tiempo de ejecución | 114,7 MB + 2,5 MB |
| N4 Marco + jefe | 331,1 MB | 126,5 MB |
| N4 César + jefe | 356,1 MB + 28,4 MB creados en tiempo de ejecución | 114,7 MB + 2,5 MB |

Las copias en CPU de las texturas con lectura y escritura pasan de 15,3 MB a 0. En el conjunto de las 100 texturas configuradas del proyecto, incluidos menús, la estimación baja de 385 MB a 112 MB.

### CPU (A/B con la misma condición de energía y las mismas texturas)

| Escenario | PlayerLoop medio | p99 | Fotogramas > 16,7 ms | Fotogramas > 33,3 ms | GC del juego por fotograma |
| --- | --- | --- | --- | --- | --- |
| N1 Marco | 6,64 → 5,94 ms | 32,3 → 10,2 ms | 19 → 3 | 11 → 1 | 808 → 73 B |
| N3 César | 9,85 → 9,02 ms | 35,7 → 18,2 ms | 27 → 15 | 17 → 5 | 724 → 59 B |
| N4 Marco + jefe | 10,12 → 9,23 ms | 34,1 → 13,2 ms | 26 → 4 | 13 → 1 | 863 → 81 B |
| N4 César + jefe | 12,77 → 9,34 ms | 79,3 → 13,7 ms | 81 → 1 | 40 → 0 | 888 → 64 B |

- Antes, todos los fotogramas generaban basura. Después, solo lo hace entre el 15 % y el 27 % de los fotogramas.
- En las cuatro grabaciones «después» del A/B no hubo ninguna recolección de basura (`GC.Collect`).
- El coste de IMGUI baja de ~0,24 a ~0,03 ms por fotograma; lo que queda es del propio Editor.
- La reconstrucción de canvas en el nivel 4 baja de 0,47–0,57 a 0,28–0,39 ms.

### GPU y renderizado (sin cambios)

Las medidas de GPU son orientativas: proceden del Editor en un equipo de escritorio.

- Entre 50 y 61 draw calls de media y entre 7 y 10 SetPass.
- La escena del mundo se dibuja en una sola pasada `Renderer2D` agrupada por el SRP Batcher; la interfaz superpuesta usa unas 19 llamadas.
- El sobredibujado estimado es de 3,1 pantallas en el nivel 1 y de 4,1 a 4,6 en los niveles 2 a 4. El cielo degradado cubre la pantalla completa y muros y calle se solapan con él.

## Cambios aplicados

1. **Compresión Android real de texturas.**
   - Problema: ETC2 solo codifica texturas con ancho y alto múltiplos de 4. Unity dejaba en RGBA32 sin comprimir, sin aviso, 73 texturas que pedían compresión (1086×1448, 2048×683, 1672×941, entre otras). Entre ellas había 24 de las 49 que la adaptación anterior daba por comprimidas; las otras 49 eran texturas con compresión automática que también caían a RGBA32.
   - Cambio: `AndroidTextureSetup` usa ETC2 RGBA8 cuando las dimensiones lo permiten y ASTC 4×4 en el resto. ASTC 4×4 ocupa los mismos 8 bits por píxel y no tiene esa restricción. Solo se toca el bloque Android y no cambia ninguna resolución.
   - Verificación: `Verify()` comprueba ahora también el formato realmente importado; esa es la comprobación que faltaba. Una comprobación independiente contra git confirmó que, en 72 `.meta`, todo lo ajeno a Android quedó intacto. La única excepción son los bloques inertes `overridden: 0` que Unity añade para WebGL.
2. **Atlas de efectos sin lectura y escritura.**
   - Problema: `ShotProjectileAtlas`, `SniperTracer` y `SniperBloodImpact` exigían `Read/Write` (copia doble en memoria) para calcular sus recortes leyendo píxeles en tiempo de ejecución.
   - Cambio: `EffectSpriteSetup` (**Tools > Lindavista > Bake effect sprite rectangles**) hornea esos mismos recortes como sprites importados. Se usan el mismo umbral de alfa y la misma región, y se respetan el pivote y los 256 px por unidad efectivos. Se tuvo en cuenta que el atlas de 1254 px se importa a 1024 px. `ShotVisual` y `SniperShotVisual` cargan los sprites horneados y las texturas pasan a comprimirse.
3. **Guardado periódico fuera del hilo principal.**
   - Problema: durante el combate, `GuardadoProgreso` escribía la munición en disco cada segundo en el hilo principal (peor fotograma medido: 23,8 ms).
   - Cambio: ahora toma el JSON en el hilo principal y escribe en otro hilo. Las escrituras se serializan con un bloqueo y un número de secuencia impide que una instantánea antigua sustituya una confirmación posterior.
   - Sin cambios: compras, niveles, suspensión, salida y cambio de escena siguen siendo síncronos. Antes de confirmar, esperan a que termine cualquier escritura de fondo pendiente. Un fallo de disco deja los consumos pendientes para reintentarlos.
4. **Mira de escritorio sin IMGUI.** La existencia de `PlayerActions.OnGUI` obligaba a ejecutar IMGUI en cada fotograma y en todas las plataformas (368 B de basura por fotograma), también en Android, donde la mira no se muestra. Las mismas cuatro barras amarillas se dibujan ahora en un lienzo mínimo, que solo se crea cuando hace falta la mira.
5. **Menos cadenas por fotograma.** En `PlayerActions` desaparecen las lecturas de `scene.name` en cada fotograma. `PlayerMuzzle` y `EnemyMuzzle` interpretan cada fotograma de animación una sola vez, en lugar de leer `sprite.name` en cada disparo o en cada comprobación de línea de visión. `WeaponSelectionHUD` solo vuelve a formatear un texto cuando cambia su valor.
6. **Números de daño.** El desvanecimiento usa `CanvasRenderer.SetAlpha`. El alfa del texto y del contorno es el mismo, pero ya no se regenera la malla del texto con contorno en cada fotograma.
7. **Iconos de munición del HUD.** Antes se creaban copias RGBA32 a tamaño completo de los dibujos de munición: 28 MB con César. Ahora se reducen en GPU a un máximo de 256 px, por pasos que promedian 2×2, y llevan mipmaps.
8. **Audio de César precargado.** Sus seis clips de disparo y recarga se cargaban y descomprimían en el hilo principal al sonar por primera vez: 7,6 ms la escopeta y 7,9 ms el francotirador en el Editor. Ahora se precargan con la escena, como los de Marco.
9. **Liberación de recursos no usados en la partida.** Se destruyen el personaje no elegido, las decoraciones de daño de otros niveles y las referencias a los astros que no se muestran, y después se ejecuta una sola descarga de recursos durante la presentación. En el nivel 3 con César, la medición manual liberó 19,5 MB.

## Evaluado y descartado

- **Cambiar Sprite-Lit por Sprite-Unlit.** Solo hay una luz global blanca de intensidad 1 y el Frame Debugger no muestra pasadas de texturas de luz, así que no habría ahorro apreciable.
- **Pool de granadas.** El lanzagranadas dispara una vez por segundo y su asignación media es mínima.
- **Pool de enemigos.** Se instancia uno cada 3 s o más y la instanciación cuesta unos 2 ms como máximo.
- **Reducir el sobredibujado** (recortar el cielo o usar mallas ajustadas en casas y grafitis). El ahorro estimado es de un 5–8 % del relleno, con riesgo de cortar bordes del arte. No se aplicó.

## Riesgos

- **ASTC en GPU antiguas.** Los dispositivos GLES3 sin ASTC (sobre todo Adreno 3xx, de 2013–2015) descomprimen esas texturas al cargarlas: más tiempo de carga y, en ellos, más memoria que antes. ETC2 se mantiene en las 27 texturas compatibles.
- **Compresión con pérdida.** Las texturas que antes iban sin comprimir ahora llevan compresión con pérdida, que puede notarse en degradados suaves o bordes finos. No se ha hecho una revisión visual en un teléfono.
- **Guardado asíncrono.** Un cierre forzado sigue pudiendo perder alrededor de un segundo de consumos, como antes. La espera síncrona al suspender depende de que Android entregue `OnApplicationPause`.
- **Descarga al empezar el nivel.** `Resources.UnloadUnusedAssets()` bloquea el hilo principal un momento al comenzar. Su coste en un teléfono no se midió.
- **Iconos de munición.** Los cartuchos del HUD se muestrean desde una copia de 256 px con mipmaps; a tamaños pequeños se ven más suaves que antes.

## Pendiente

- **Hojas de César en el nivel 1.** Con Marco seguían cargadas 9 hojas de animación de César aunque su objeto se destruye. Causa probable: la caché estática de `PlayerMuzzle` guardaba el `Sprite` como clave. Se corrigió el 18 de octubre; la liberación queda por confirmar en un dispositivo. Ver [Rendimiento en Windows](rendimiento-escritorio-2026-10-18.md).
- **Comprobaciones en Play Mode.** Se repitieron y aprobaron el 18 de octubre: `CombatPoolingChecks`, `AndroidCombatPerformanceChecks`, `SniperShotChecks`, `DamageNumberChecks`, `ShotVisualChecks.VerifyPlayerMuzzles/VerifyScreenShooting`, `ComprobacionesMoviles` y `AndroidLifecycleChecks`.
- **Comprobaciones aprobadas en el Editor.** Sí se ejecutaron y aprobaron:
  - `ComprobacionesGuardadoProgreso` (con casos nuevos de escritura de fondo, fallo y orden)
  - `EffectSpriteSetup.Verify`
  - `AndroidTextureSetup.Verify` (100 texturas)
  - `ShotVisualChecks.Run`
  - `AndroidBuildTools.Verify` (13 comprobaciones)
- **Medición en dispositivo.** Generar una Development Build y medir con el Profiler conectado:
  - tiempos de CPU y GPU, temperatura y memoria residente con Memory Profiler;
  - si el dispositivo admite ASTC;
  - el coste de la descarga inicial;
  - el aspecto de las texturas comprimidas.
