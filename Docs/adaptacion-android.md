# Adaptación de Lindavista a Android

Validación del 17 de octubre de 2026. Registra la implementación y las comprobaciones aprobadas en el editor de Unity. Los scripts quedaron compilados y la segunda sesión de QA en Play Mode terminó con cero errores y cero advertencias en la consola. Las pruebas del editor no equivalen a validación en un teléfono.

El objetivo de esta entrega es Android en horizontal, con ARMv7 de 32 bits y ARM64 de 64 bits, IL2CPP, OpenGL ES 3 y texturas ETC2. Las escenas de entrada son `MainMenu`, `CharacterSelection` y `Gameplay`; los cuatro niveles se seleccionan dentro de Gameplay. No se configuró iOS y se preservó la lista global de escenas, incluida `Testing`.

**No se compiló ni generó un APK por indicación expresa del usuario. Esa generación está excluida de esta entrega y no se registra como tarea pendiente.**

## Controles e interfaz

El HUD permite mantener movimiento con un dedo y apuntar/disparar con otro. Las flechas se compensan cuando se pulsan simultáneamente. El dedo que captura una región es el que puede liberarla; levantar otro dedo no interrumpe la acción. Salir de una flecha detiene ese movimiento, mientras que arrastrar fuera del área de apuntado conserva la captura hasta soltar. `REC` recarga y los espacios del HUD de armas permiten cambiar el arma equipada. Los botones de vida y las acciones de inventario conservan su acceso táctil.

Pausa, tienda, derrota, victoria e introducción del jefe bloquean el combate y cancelan las entradas retenidas. Al soltar el disparo también se elimina la petición pendiente de ese cuadro. La introducción espera que se liberen todos los dedos antes de entregar de nuevo el control al jugador.

Menús, tiendas, HUD de vida y avisos del jefe se ajustan al área segura y al espacio disponible. La tienda dispone de desplazamiento para alcanzar sus acciones en pantallas estrechas. La vida y el aviso de ráfaga del jefe tienen una disposición específica para el HUD táctil. En el editor, `F9` permite previsualizar los controles con ratón; esta vista no sustituye una prueba multitáctil real.

## Rendimiento y batería

El perfil Android usa una canalización URP propia, sin HDR, MSAA, sombras ni copias de cámara de profundidad/color que el juego no necesita. La interfaz superpuesta conserva la resolución de pantalla; la escala del mundo se calcula aparte.

| Ajuste | Cadencia objetivo | Lado corto objetivo del mundo |
| --- | --- | --- |
| Rendimiento | Hasta 60 FPS, ajustados a un divisor de la frecuencia de pantalla | Hasta 900 píxeles |
| Ahorro de batería | Hasta 30 FPS, ajustados a la frecuencia de pantalla | Hasta 720 píxeles |

El ajuste se guarda desde el panel de configuración y se reaplica al volver a la aplicación. La escala nunca amplía una pantalla de menor resolución y tiene un límite inferior de 0,35. Estos valores son límites de trabajo y objetivos de presentación; no son una medición de FPS obtenidos en hardware.

El calentamiento de proyectiles, trazadores, explosiones y números de daño se reparte entre cuadros de la introducción. El nivel 4 prepara además los proyectiles del jefe antes de su llegada. Los pools reutilizan objetos y una precarga repetida crea solo los que faltan. El jefe conserva su límite de 16 proyectiles simultáneos y limpia sus balas al terminar el combate. Las esperas entre disparos de enemigos se reutilizan. En Android, el humo del RPG se emite cada 0,05 segundos y los efectos de partículas tienen un máximo de 128 objetos activos; las reglas de daño y la cadencia de las armas se mantienen.

## Texturas y audio

`AndroidTextureSetup` configuró y verificó 49 PNG no legibles por CPU que antes heredaban importación sin compresión, con un override exclusivo de Android `ETC2_RGBA8`, calidad 50 y Crunch desactivado. `AndroidTextureSetup.Verify()` pasó sobre los 49 recursos. El proceso terminó con `LastStage = "Complete"`, `ProcessedCount = 49` y `LastError` vacío.

La herramienta conserva la resolución efectiva y comprueba las imágenes originales y el YAML fuera del bloque Android, incluidos GUID, recortes de sprites, filtrado, valores predeterminados y ajustes de otras plataformas. También se compararon independientemente los 49 metadatos contra Git: ningún ajuste ajeno a Android cambió, salvo espacios finales de serialización. Si una comprobación de preservación falla, la herramienta intenta restaurar los metadatos y conserva el diagnóstico si Windows impide escribirlos.

`Boss_Base.png` queda excluido por completo.

**Corrección posterior (revisión de rendimiento del mismo día):** ETC2 solo comprime dimensiones múltiplo de 4, y 24 de esas 49 texturas seguían importándose como RGBA32. `AndroidTextureSetup` usa ahora ASTC 4×4 en esos casos y verifica el formato realmente importado. Cubre 100 texturas: 27 en ETC2 y 73 en ASTC. `SniperBloodImpact`, `SniperTracer` y `ShotProjectileAtlas` ya no necesitan `Read/Write`, porque sus recortes se hornean como datos de importación. Detalle y mediciones: [Revisión de rendimiento](rendimiento-2026-10-17.md).

La auditoría de metadatos encontró las dos músicas con carga en streaming. Los efectos de armas son clips cortos; no se identificó música larga configurada para descomprimirse completa al cargar. El ahorro teórico de las texturas no equivale a memoria residente medida, porque depende de los recursos cargados en cada pantalla.

## Suspensión y regreso a la aplicación

En Android, perder foco o pasar a segundo plano cancela los dedos activos, guarda los cambios de progresión pendientes y suspende tiempo/audio. Durante una partida normal, el regreso mantiene visible la pausa para que el jugador decida cuándo continuar. Recuperar solo el foco no reanuda una aplicación que sigue suspendida.

Si hay un diálogo del jefe o una tienda abierta, se conserva esa interfaz y se restaura su estado anterior al regresar; al cerrarla se presenta la pausa pendiente. La restauración preserva el valor anterior de `Time.timeScale` y de la pausa de audio. Los diálogos también rechazan avance mientras la aplicación carece de foco.

## Configuración y comprobaciones en el editor

Usar Unity `6000.3.23f1` con Android Build Support, SDK, NDK y OpenJDK. Fuera de Play Mode, ejecutar los siguientes métodos desde las herramientas del editor o un ejecutor C# del editor:

```csharp
AndroidBuildTools.Configure();
Debug.Log(AndroidBuildTools.Verify());
Debug.Log(AndroidTextureSetup.Configure());
Debug.Log(AndroidTextureSetup.Verify());
```

Los menús equivalentes son `Tools > Lindavista > Android > Configurar Android`, `Tools > Lindavista > Configure Android texture compression` y `Tools > Lindavista > Verify Android texture compression`. `AndroidBuildTools.Configure()` configura Android y valida las rutas de sus tres escenas sin sustituir la lista global de escenas. No genera un instalador.

Las comprobaciones se ejecutan en el contexto indicado:

| Método o comprobación | Contexto | Cobertura | Resultado registrado |
| --- | --- | --- | --- |
| `AndroidBuildTools.Verify()` | Fuera de Play Mode | Perfil Android, arquitecturas, canalización, GLES3/ETC2 y escala/cadencia | Aprobado: 13 comprobaciones |
| `LevelTwoChecks.RunAssets()` | Editor | Recursos y configuración del nivel 2 | Aprobado: 84 comprobaciones |
| `LevelThreeChecks.RunAssets()` | Editor | Recursos y configuración del nivel 3 | Aprobado: 74 comprobaciones |
| `LevelFourChecks.RunAssets()` | Editor | Recursos y configuración del nivel 4 | Aprobado: 22 comprobaciones |
| `ComprobacionesGuardadoProgreso.Ejecutar()` | Editor, perfil temporal | Guardado agrupado de progresión | Aprobado |
| `HealthConsumableChecks.RunRepository()` | Repositorio temporal | Inventario y transacciones de pastillas | Aprobado: 24 comprobaciones |
| `HealthConsumableChecks.RunRuntime()` | Play Mode | Uso y comportamiento de pastillas | Aprobado: 66 comprobaciones |
| Movimiento y cámara | Play Mode | Movimiento y seguimiento de cámara | Aprobado |
| `ComprobacionesRendimientoCombate.Ejecutar()` | Play Mode | Buffers y partículas | Aprobado |
| `AndroidLifecycleChecks.RunRuntime()` | Play Mode, estados simulados | Suspensión, foco, pausa, diálogos y tienda | Aprobado: 16 comprobaciones |
| `AndroidCombatPerformanceChecks.RunRuntime()` | Escena vacía en Play Mode | Precarga por etapas, reutilización y límite de balas del jefe | Aprobado: 417 comprobaciones |
| `CombatPoolingChecks.RunRuntime()` | Escena vacía en Play Mode | Pools de armas, trazadores, explosiones y presupuestos | Aprobado: 3661 comprobaciones |
| Interfaz y superficies táctiles | Pantallas de menú y partida en Play Mode | Geometría y acceso a controles | Aprobado en las pantallas y resoluciones detalladas abajo |
| `AndroidTextureSetup.Verify()` | Fuera de Play Mode | Conjunto de 49 texturas y preservación de metadatos | Aprobado: 49 recursos; proceso completo sin error |
| `ComprobacionesMoviles.Ejecutar()` | Play Mode | Captura, cancelación y bloqueo de entradas | Aprobado a 1280×720, 1024×768, 2400×1080 y 960×540 |
| `ShotVisualChecks.VerifyPlayerMuzzles()` y `VerifyScreenShooting()` | Play Mode | Orígenes de disparo y disparos en pantalla | Ambos aprobados con Marco y César y sus seis armas |
| Generación de APK | No realizada | Excluida por indicación expresa del usuario | No es una tarea pendiente |

En la revisión de interfaz se comprobaron controles dentro del área segura y superficies táctiles en menú y sonido a 1280×720, ayuda táctil a 960×540 y selección a 1024×768. En la tienda a 1024×768 se validaron 9 y 8 objetivos visibles en sus posiciones de desplazamiento y las 11 acciones de compra/selección resultaron alcanzables. Pausa y derrota se revisaron a 1024×768. Los modos Fluido y Ahorro se alternaron en el editor y se comprobó que guardan la elección; esto no mide la cadencia real de un dispositivo.

Las regresiones de entrada y disparo se repitieron después de corregir la preparación de las pruebas para componentes deshabilitados y para el tipo de actualización actual de Input System (`currentUpdateType`). Los resultados aprobados de la tabla corresponden a esa repetición.

Se verificó el arranque de los niveles 1, 2 y 3. En el nivel 4 se recorrieron las cuatro frases de la introducción y se forzó la aparición del jefe dentro de la sesión de QA para inspeccionar su HUD y controles. **No se jugó ni se verificó una campaña completa hasta derrotar al jefe.**

Para revisar niveles sin gastar el inventario real, iniciar `AndroidValidationSession.Begin()` en Play Mode y usar `AndroidValidationSession.PlayLevel(indice, cesar)`, con índice de 0 a 3. `AndroidValidationSession.Resolution(ancho, alto)` cambia la resolución de revisión. `AndroidValidationSession.End()` solicita salir de Play Mode; el perfil real y las preferencias se restauran al finalizar la sesión. Durante la revisión se bloquea la recarga de ensamblados para mantener el aislamiento del perfil. `EmptyScene()` solo funciona dentro de esa sesión y vacía su escena de ejecución para las pruebas de pools.

Las capturas están en `Captures/Android20261017/`:

| Pantalla | Archivos |
| --- | --- |
| Menú y ajustes | `menu-1280x720.png`, `ajustes-sonido-1280x720.png`, `ajustes-pantalla-1280x720.png` |
| Ayuda táctil | `ayuda-tactil-small.png` |
| Selección y tienda | `seleccion-tableta.png`, `tienda-tableta.png`, `tienda-final-tableta.png` |
| Pausa y resultado | `pausa-tableta.png`, `resultado-tableta.png` |
| Niveles 1 a 3 | `nivel1-tableta.png`, `nivel2-2400x1080.png`, `nivel3-2400x1080.png` |
| Jefe del nivel 4 | `jefe-introduccion-tableta.png`, `jefe-controles-tableta.png`, `jefe-pausa.png` |
| Revisión final | `android-final-960x540.png` |

## Límites de lo comprobado

No hubo dispositivos Android detectados por `adb`, por lo que no se realizaron instalación ni pruebas físicas. Tampoco se generó un APK, conforme a la instrucción del usuario. La validación registrada se limita al editor y no incluye iOS.

Las pruebas verifican lógica, reutilización de objetos y geometría simulada. No miden latencia multitáctil real, compatibilidad de GPU/controlador, consumo, temperatura, tiempos de cuadro CPU/GPU, memoria residente ni estabilidad de una sesión prolongada. Las comprobaciones de ciclo de vida simulan los estados por reflexión; no demuestran la entrega de eventos HOME, notificaciones o bloqueo de pantalla por Android. No se atribuye una medición de FPS ni ausencia de tirones en teléfonos a estos resultados del editor.
