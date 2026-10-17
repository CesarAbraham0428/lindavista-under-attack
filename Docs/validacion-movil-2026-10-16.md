# Validación móvil y rendimiento — 16 de octubre de 2026

Proyecto revisado con Unity 6000.3.23f1. Los cambios se aplican durante la ejecución a las escenas existentes; no se reconstruyeron escenas ni se eliminaron assets.

Las pruebas siguientes se ejecutaron antes de retirar Sentis de las dependencias. La importación y compilación posteriores a esa retirada siguen pendientes, como se detalla más abajo.

## Pruebas ejecutadas

| Área | Resultado observado |
| --- | --- |
| Compilación de scripts | Sin errores ni advertencias de los scripts del juego al finalizar la importación. |
| Controles táctiles | `ComprobacionesMoviles.Ejecutar()` pasó en 960×540, 1280×720, 2400×1080 y 1024×768: dos dedos, flechas opuestas, liberación del puntero correcto, arrastre de apuntado, pausa y segundo plano. |
| Geometría | Pasaron las comprobaciones de menú, selección y tienda en 1024×768; pausa en 2400×1080; ajustes y resultado en 960×540; HUD en las cuatro resoluciones anteriores. |
| Tienda | `CharacterShopChecks.RunRuntime()` pasó sus 19 comprobaciones, con perfil temporal, compras y mejoras independientes. Se revisaron visualmente el inicio y el final del listado desplazable. |
| Guardado | `ComprobacionesGuardadoProgreso.Ejecutar()` pasó: lote de consumos, confirmación, compras inmediatas, consultas sin asignaciones, error de disco/reintento y recuperación. También pasó `CharacterProgressionChecks.RunStorage()`. |
| Movimiento y cámara | Pasaron `PlayerMovementChecks.RunRuntime()` y `CameraFollowChecks.Run()`. Este último comprueba además el límite de entrada al alternar 4:3, 3:2, 16:9 y 20:9 sin acumular desplazamiento. Se revisó el personaje completo en 4:3. |
| Disparos | Pasaron `ShotVisualChecks.VerifyPlayerMuzzles()` y `VerifyScreenShooting()`: ambos personajes, tres armas, distancias y direcciones, origen del cañón, munición y cadencia. |
| Física y partículas | `ComprobacionesRendimientoCombate.Ejecutar()` pasó: más de 80 contactos, capas/triggers, daño único y reutilización con reinicio de estado. |
| Francotirador | `SniperShotChecks.Run()` pasó 73 comprobaciones de penetración, daño, obstáculos, escudos, alcance y visuales. |
| Granadas | `GrenadeProjectileChecks.Run()` pasó 36 comprobaciones de trayectoria, rebote, mecha, pausa, daño y obstáculos. |

Las comprobaciones de compras y disparos usan perfiles temporales y no gastan el saldo ni la munición del jugador. La prueba de disparos se ajustó para usar el directorio temporal del sistema y el mismo guardado agrupado de la partida: sus escrituras inmediatas anteriores chocaban con archivos de respaldo abiertos por OneDrive.

## Retirada de dependencia y validación pendiente

Después de las comprobaciones anteriores se retiró `com.unity.ai.inference` 2.6.1 del manifiesto y del archivo de bloqueo. También se retiró del bloqueo `com.unity.dt.app-ui` 2.1.1, cuyo único dependiente era Sentis. La revisión no encontró API ni modelos de inferencia usados por el juego; Assistant y las dependencias compartidas permanecen. Se limpiaron los símbolos `SENTIS_ANALYTICS_ENABLED` y `APP_UI_EDITOR_ONLY`, y la entrada de configuración App UI de `EditorBuildSettings`. Se conservaron las acciones de Input System, las escenas y todos los archivos de assets y `PackageCache`.

La build Android iniciada seguía ocupada compilando shaders `Hidden/Sentis` al preparar esta limpieza. El estado fuente se comprueba mediante lectura de JSON y validación de referencias del grafo; falta que el Editor quede libre, resuelva/importe los paquetes y termine una nueva compilación. No se afirman pruebas posteriores a la retirada ni un APK final validado. La reducción del coste de shaders y del tamaño del binario debe medirse con la nueva build.

## Capturas revisadas

Las imágenes están en `Captures/RevisionMovil`:

- `gameplay-2400x1080.png`: HUD y controles panorámicos.
- `gameplay-tableta-hud.png`: personaje completo y controles en tableta 4:3.
- `menu-1024x768.png` y `seleccion-1024x768.png`: contenido completo de los menús.
- `tienda-1024x768-1.png` y `tienda-final-1024x768.png`: listado desplazado de principio a fin.
- `ajustes-960x540.png`: instrucciones móviles.
- `pausa-2400x1080.png` y `resultado-final-960x540.png`: modales y fondo desenfocado.

## Alcance

Las pruebas de varios dedos generan eventos de UI con distintos identificadores; no equivalen a una prueba física en teléfono. Quedan por verificar en Android/iOS reales los recortes del sistema, rotación, suspensión, tacto y rendimiento sostenido. La simulación manual de márgenes adicionales no se cuenta como validación del notch del sistema.

No se presenta una comparación de FPS o memoria del dispositivo: las lecturas del Profiler del Editor no permiten deducir esa mejora. Las optimizaciones eliminan operaciones concretas: escrituras por bala, copias de inventarios por lectura, arrays de consultas físicas por impacto, creación repetida de partículas y lecturas completas de capturas/atlas.

La munición pendiente se confirma cada segundo y al abandonar la aplicación o escena. Un cierre forzado puede recuperar aproximadamente un segundo de munición; compras y recompensas se confirman inmediatamente. Véase [Móvil y rendimiento](movil-y-rendimiento.md).

## Cierre pendiente del Editor

La primera build de Android (`build-e7b8fbc52d`, destino `Builds/RevisionMovil/Lindavista.apk`) comenzó antes de retirar la dependencia de inferencia. Esa build quedó ocupada compilando variantes de `Hidden/Sentis/ConvTranspose`; el MCP dejó de responder a las solicitudes de estado y cancelación. No se considera una APK final validada.

El manifiesto y el archivo de bloqueo ya preparan la retirada de Inference/App UI. Su grafo se comprobó como JSON: 54 dependencias directas, 68 nodos y ninguna referencia ausente. La resolución de paquetes, importación y compilación finales necesitan que el Editor quede libre. También se amplió el botón de recarga a 150×145 unidades de referencia después de las capturas; corresponde repetir la comprobación táctil al terminar la importación.

Unity generó cambios de caché de shaders en `UniversalRP.asset` y `UniversalRenderPipelineGlobalSettings.asset` durante esa build. La restauración del estado anterior no pudo completarse porque Windows mantiene ambos archivos mapeados por el Editor; no se forzó su reemplazo. Revisar estos cambios automáticos y las opciones temporales de build al cancelar o terminar, y restaurar la selección original de Game View. `runInBackground` ya volvió a su valor original `0`.

La build también generó los temporales `Assets/Resources/PerformanceTestRunSettings.json` y `PerformanceTestRunInfo.json` con sus `.meta`, que no existían al iniciar la revisión. Revisar su limpieza al cerrar la build; no son arte original del proyecto.

Se espera a que la compilación anterior termine, conforme a la preferencia del usuario. Después corresponde comprobar los paquetes resueltos y los scripts, repetir controles/geometría y generar la build definitiva. Las pruebas en hardware real siguen pendientes.
