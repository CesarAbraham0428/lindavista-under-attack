# Móvil y rendimiento

Esta página describe la adaptación móvil y las optimizaciones presentes en el código. Las comprobaciones automatizadas verifican comportamientos concretos; los resultados y capturas de la revisión se registran por separado al terminar su ejecución. No se deduce rendimiento real de un teléfono a partir del Editor.

Resultados ejecutados y capturas: [Validación del 16 de octubre de 2026](validacion-movil-2026-10-16.md).

## Pantalla e interfaz

- `Assets/_Project/Scripts/Movil/ConfiguracionMovil.cs` aplica la política exclusiva de plataformas móviles: autorrotación entre horizontal izquierda y derecha, con retrato deshabilitado. Calcula `Application.targetFrameRate` a partir de la frecuencia de pantalla, buscando un divisor con un máximo de 60: 60 Hz → 60, 90 Hz → 45 y 120 Hz → 60. Si la frecuencia no es válida, solicita 60. Es un objetivo de cadencia, no una medición ni una garantía de FPS. Referencia: [Unity 6.3 — Application.targetFrameRate](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-targetFrameRate.html).
- `Assets/_Project/Scripts/Interfaz/AreaSeguraUI.cs` aplica los márgenes de `Screen.safeArea` y los actualiza cuando cambian la resolución o el recorte del sistema. Los HUD de vida y táctil también conservan su ajuste al área segura.
- `ContenidoAdaptableUI.cs` encaja el contenido de referencia dentro del espacio disponible; `AdaptacionMenusUI.cs` adapta los menús y mantiene su fondo cubriendo la pantalla. El escalado `CanvasScaler.ScreenMatchMode.Expand` permite acomodar distintas proporciones sin recortar los controles del diseño de referencia.
- `GameFlowController` adapta pausa y resultado. Los HUD de vida, saldo, arma y munición distribuyen sus elementos según el espacio disponible. Los cambios de tamaño evitan repetir escrituras de geometría si las dimensiones siguen iguales.
- `PistolUpgradeShop` usa una lista vertical con `ScrollRect` y recorte en móvil o cuando `Screen.height <= 800`. La cabecera, el saldo, el mensaje de compra y **VOLVER** quedan fuera de la lista; el personaje y las tres armas pueden recorrerse deslizando. Incluye indicador de desplazamiento y textos de compra ampliados para esta distribución.

La adaptación se instala durante la ejecución y conserva las referencias de las escenas existentes. En `Gameplay` y `Testing`, **F9** alterna la previsualización táctil solo en el Editor. El detalle de acciones y reservas está en [Controles](controles.md).

## Entrada y ciclo de vida

`MobileControlsHUD` y `TouchControlRegion` separan los dedos por identificador de puntero. Una flecha se libera al salir de su área; el apuntado admite arrastrar fuera y continúa hasta soltar el dedo que lo inició. Pausa, tienda, resultado y desactivación cancelan entradas pendientes. Al perder foco o entrar en segundo plano se limpian movimiento y disparo retenidos. En móvil, `GameFlowController` también abre la pausa al perder foco o suspenderse; al volver, el jugador puede continuar desde el letrero de pausa.

El HUD táctil se oculta mientras la partida está pausada, terminada o tiene la tienda abierta. Las acciones bloqueadas rechazan nuevos toques. La previsualización con mouse evita que el mismo clic actúe como control táctil y disparo convencional.

## Guardado de progreso

`ProgressionRepository` conserva una instantánea de lectura independiente para las herramientas que la necesiten, pero las consultas habituales de saldo, vida, mejora y munición leen escalares sin clonar ambos inventarios. Esto elimina asignaciones de memoria en consultas que se repiten al disparar o refrescar el HUD.

El servicio activa el agrupado de consumos de munición. `GuardadoProgreso` permanece entre escenas y confirma los cambios como máximo una vez por segundo durante el disparo continuo, usando tiempo sin escalar. También confirma pendientes al descargar una escena, perder foco, pausar la aplicación, salir y destruir su host. Compras, monedas y nuevas victorias mantienen confirmación inmediata; estas transacciones incluyen cualquier consumo de munición pendiente. Volver a completar un nivel ya registrado no reescribe el perfil si no hay consumos pendientes.

La escritura serializa JSON compacto a un temporal. Si el principal previo era válido, primero lo copia a `.bak`; después reemplaza el principal atómicamente mediante `File.Replace`, o mueve el temporal si todavía no existe un principal. La copia del respaldo es una operación separada: evita pedir a `File.Replace` que elimine o reemplace un `.bak` que pueda mantener abierto un sincronizador de Windows. El repositorio conserva si el principal cargado era válido para evitar releer y deserializar el archivo por cada operación. Al recuperarse desde un respaldo válido, no lo reemplaza con el principal corrupto. Un fallo de escritura mantiene los consumos en memoria para reintentarlos; una compra fallida no cobra ni concede la mejora. Si ni el principal ni el respaldo son recuperables, se conservan los archivos y se bloquean las transacciones.

**Límite de persistencia:** un cierre forzado puede recuperar hasta aproximadamente un segundo de munición ya consumida desde la última confirmación correcta. Si el almacenamiento falla, los pendientes permanecen en memoria hasta que un reintento tenga éxito. El agrupado no se aplica a compras ni al abono de monedas. El archivo sigue siendo `progression-v1.json`, con esquema interno de versión 2 y respaldo `.bak`; no se cambia el formato de progresión existente.

## Optimizaciones de combate y memoria

- `Scripts/Combate/ConsultasFisicasCombate.cs` presta listas reutilizables para barridos, líneas y solapamientos, además de un conjunto para evitar daño repetido en una explosión. Las listas crecen cuando hace falta, de modo que una escena con muchos contactos no pierde impactos por llenar un array fijo. Cada consulta toma su propio contenedor, incluyendo explosiones disparadas desde otros eventos. Conserva las diferencias de máscaras entre barridos y solapamientos y la configuración de triggers.
- `Scripts/Combate/ObjetivosCombate.cs` comparte la búsqueda de jugadores entre enemigos y recoge esa caché al cambiar de escena. `EnemyHealth` conserva referencias que antes buscaba durante impactos. Se reducen búsquedas globales y de componentes en los caminos frecuentes de combate.
- `ShotEffect` reutiliza objetos y `SpriteRenderer` para las partículas breves. Reinicia edad, color, tamaño y orientación antes de reactivarlos. El límite de efectos sigue siendo 180; el contenedor pertenece a la escena para que al cambiar de nivel se libere el conjunto.
- `ShotVisual` prepara los recortes del atlas con una única copia temporal de sus píxeles. `PlayerActions` conserva hashes y duración de animación por arma/controlador. `PlayerWeaponController` conserva la identidad del personaje y la cámara de disparo. Fuentes, marcos, ilustraciones de armas y retratos evitan cargas repetidas durante refrescos de interfaz.
- `WeaponSelectionHUD` reutiliza imágenes de cartuchos entre cambios de capacidad y evita reconstruir variantes del mismo personaje. `HUDSpriteVariants` recorta en GPU antes de leer el sprite, usa un temporal del tamaño necesario y libera las copias de textura en CPU después de prepararlas.
- `DefeatBackdrop` reduce la captura en GPU antes de leer y desenfocar sus píxeles; evita crear un array administrado del tamaño completo de una pantalla de alta resolución. Sus texturas temporales se liberan incluso si falla la captura.
- `SelectedLevelEnvironment` solo cambia la escala del cielo cuando varían zoom o proporción; libera su objeto, sprite y textura generados. La cámara y los controles limitan búsquedas repetidas cuando no hay jugador disponible. La selección de una pista ya activa y los cambios de volumen idénticos evitan trabajo repetido.

Estas medidas reducen trabajo conocido y asignaciones evitables. No sustituyen el perfilado de CPU, GPU, memoria, temperatura y almacenamiento en equipos reales. Se conservan los assets y los scripts necesarios para escenas, recursos, migración, verificaciones y alternativas visuales.

## Reutilización de disparos y preparación de los niveles (17 de octubre)

Se conserva el aspecto de los impactos y el balance de los niveles. Reducir trabajo
por fotograma es el objetivo; tener menos líneas no equivale a ejecutar más rápido.

- `CombatProjectile` reutiliza balas de pistola/escopeta, SMG, cohetes y proyectiles
  perforantes en pools separados. Reinicia dirección, daño, alcance, escala,
  cámara, suelo y víctimas antes de activar el objeto. Los `SpriteRenderer`,
  `TrailRenderer` y conjuntos de penetración también se conservan. Al terminar
  un disparo se libera su plaza inmediatamente y solo una vez, incluso si después
  se destruye el objeto. El presupuesto compartido continúa siendo 64 proyectiles.
- `SniperShotVisual`, `RocketImpact` y los trazos de enemigos también se reutilizan.
  Se reinician opacidad, edad, fotograma, recorrido y estela. Los disparos enemigos
  limitan a 64 sus trazos visuales simultáneos; esto no descarta daño ni modifica
  la cadencia, porque el daño se resuelve antes del efecto. Se mantiene el límite
  previo de 24 explosiones visibles.
- `SceneCombatPool` centraliza la reutilización y mantiene los objetos dentro de
  su escena. Sus reservas inactivas son acotadas y desaparecen al descargar el
  nivel; no se usa `DontDestroyOnLoad` para retenerlas entre partidas.
- `CombatWarmup` reparte la preparación en varios fotogramas de la introducción:
  atlas y partículas, 48 partículas reutilizables, 21 proyectiles de cuatro modos,
  6 trazos enemigos, 4 trazos de francotirador y 2 explosiones. Los objetos se
  preparan inactivos, sin disparar, consumir munición ni adelantar la oleada. Si
  hace falta más capacidad durante el combate, se amplía hasta los límites; no se
  recorta el número configurado de enemigos. Las granadas balísticas mantienen
  su implementación de contacto/rebote/mecha y no se mezclan con el pool de cohetes.
- La geometría de cámara se comparte entre proyectiles y se recalcula cuando
  cambia la cámara o su matriz, incluido movimiento y zoom dentro del mismo frame.
- `BasicEnemy` evita búsquedas por fotograma cuando no hay objetivo, escrituras
  idénticas de movimiento/orientación y consultas de ataque duplicadas cuando no
  ha cambiado de posición. Las ráfagas comprueban alcance y cobertura una vez por
  emisión, no dos; los golpes y disparos siguen revalidando al causar daño. No se
  omiten colisiones ni se alarga el intervalo de simulación física.

### Comprobaciones ejecutadas

En una copia aislada con Unity 6000.3.23f1 se ejecutaron las regresiones de niveles
2/3 (69/76 comprobaciones de configuración), oleadas de los niveles 1–3 con ambos
personajes donde corresponde (1628), avance enemigo (138), impactos/IA (105),
explosivos (133), buffers/partículas y reutilización de proyectiles (3661).
Pasaron además las comprobaciones completas de francotirador (73) y lanzagranadas
(40), incluyendo el disparo desde las acciones reales de César, munición, pose,
pausa/fundido y contacto/rebote/mecha. Se actualizó la aserción de fin del trazo
para comprobar que queda inactivo después del fundido, en vez de exigir destruir
un objeto que ahora se reutiliza.
También se probó la preparación durante varios fotogramas y la descarga de una
escena mientras ya existe un disparo en la siguiente: el nuevo disparo y su
presupuesto permanecen válidos.

La prueba nueva está disponible en una **escena vacía en Play Mode**, no durante
una partida: **Tools > Lindavista > Verify projectile pools (empty Play Mode
scene)**, método `CombatPoolingChecks.RunRuntime()`. Comprueba reutilización,
estado reiniciado, límite de 64, destrucción externa, granadas, estelas, fundido,
explosiones y movimiento/zoom de cámara. Usa disparos sin dueño para no consumir
munición del perfil; deja reservas inactivas que se liberan al descargar la escena.

Un ensayo sintético local, con recursos calientes y 200 emisiones consecutivas
por modo, obtuvo lo siguiente. Se midió únicamente `TrySpawn`, sin incluir las
búsquedas del verificador, destrucción, renderizado, IA ni guardado:

| Modo | Objetos distintos antes / después | CPU acumulada de emisión antes / después |
| --- | --- | --- |
| Pistola/escopeta | 200 / 1 | 14.237 / 4.454 ms |
| SMG | 200 / 1 | 20.147 / 4.248 ms |
| Cohete | 200 / 1 | 24.411 / 3.850 ms |
| Perforante | 200 / 1 | 21.902 / 3.184 ms |

Son mediciones locales de una ejecución por versión, **no FPS ni una reducción
equivalente del tiempo total de la partida**. El contador de asignaciones por hilo
no proporcionó datos útiles en este entorno Mono; no se afirma «cero GC». La copia
de validación usa el renderer integrado, no la configuración URP completa del
proyecto; tampoco equivale a una build Android. El Editor de pruebas registró una
excepción de su indexador `UnityEditor.Search`, sin impedir las comprobaciones de
combate; no se presenta el registro como una consola íntegramente libre de errores.

Para comparar fluidez real, dejar recompilar, reiniciar Play y recorrer los tres
niveles con ambos personajes: disparo sostenido, cambios de arma, grupos de
enemigos, pausa, derrota y reintento. Repetir en una build del equipo/dispositivo
objetivo. En el Profiler se añadieron las muestras `Lindavista.ProjectileSweep` y
`Lindavista.EnemyAI`; comparar CPU Timeline, GC Alloc, GPU y los fotogramas lentos
con la misma resolución y oleada. El guardado periódico conserva sus garantías y
no se trasladó a un hilo sin coordinación con las compras.

## Dependencias y compilación

Se retiró de los archivos de paquetes la dependencia directa `com.unity.ai.inference` 2.6.1 (Sentis), junto con `com.unity.dt.app-ui` 2.1.1, que solo estaba requerida por ella. La revisión del código y los recursos del juego no encontró llamadas a su API ni modelos de inferencia. `com.unity.ai.assistant` no depende de Sentis y se conserva. Burst, Collections, Newtonsoft e ImageConversion también permanecen, porque los requieren otros sistemas del proyecto.

Sentis aportaba 65 shaders y 42 compute shaders bajo su carpeta `Resources`; el intento de build Android estaba compilando variantes `Hidden/Sentis` ajenas al juego. Retirar la dependencia evita incluir ese conjunto en las siguientes resoluciones de paquetes. No se eliminaron assets del proyecto ni archivos de `PackageCache`. Se limpiaron únicamente sus dos símbolos de compilación (`SENTIS_ANALYTICS_ENABLED` y `APP_UI_EDITOR_ONLY`) y la referencia de configuración de App UI en `EditorBuildSettings`, conservando las acciones de Input System.

**Estado posterior a la retirada:** los archivos JSON y las referencias del grafo se revisan en disco, pero la resolución/importación de paquetes y la compilación final quedan pendientes de que el Editor deje libre el build en curso. Las pruebas registradas hasta este punto son anteriores a esta retirada; todavía no se presenta una build Android validada ni un ahorro de tamaño medido.

## Reproducir las comprobaciones

Permite que Unity termine de importar y compilar antes de ejecutar las herramientas. Las rutas de menú y métodos disponibles son:

| Comprobación | Preparación y ejecución | Qué comprueba |
| --- | --- | --- |
| Guardado agrupado | Menú **Tools > Lindavista > Verificar guardado agrupado**, o `ComprobacionesGuardadoProgreso.Ejecutar()` | Diez consumos sin diez escrituras, confirmación del lote, inventarios independientes, compra inmediata, lecturas sin asignaciones, victoria repetida sin reescritura, fallo de disco/reintento y recuperación de respaldo. Usa una carpeta temporal con identificador único y no cambia el perfil real. |
| Controles móviles | Abrir `Gameplay` o `Testing` en Play Mode; esperar a que termine la introducción y mantener la partida activa, sin pausa ni tienda. Menú **Lindavista > Comprobar controles móviles (Play Mode)**, o `ComprobacionesMoviles.Ejecutar()` | Identificadores de varios dedos, flechas opuestas, salida de región, arrastre de apuntado, cancelación por pausa/segundo plano, límites de las áreas táctiles y cadencias de 60/90/120 Hz. Restaura visibilidad y libera toques al terminar. |
| Geometría de interfaz | En Play Mode, abrir la pantalla que se quiera verificar. Menú **Tools > Lindavista > Verificar interfaz en pantalla actual**, o `ComprobacionesInterfazMovil.ValidarDisposicionActual()` | Los controles visibles quedan dentro del área segura y tienen una superficie que recibe raycast. Considera el recorte de las listas desplazables. Ejecutarla en menú, selección, ajustes, tienda, pausa y resultado, desplazando también la tienda hasta el final. |
| Buffers y partículas | Iniciar una partida activa en Play Mode. Menú **Lindavista > Comprobar optimización de combate (Play Mode)**, o `ComprobacionesRendimientoCombate.Ejecutar()` | Más de 80 contactos sin truncamiento, triggers y capa Ignore Raycast, daño único y reutilización de partículas con estado reiniciado. Crea objetos de prueba alejados y los limpia al terminar. |

Para revisar proporciones, cambiar el tamaño de la Game View y repetir las pantallas y comprobaciones: al menos 16:9, teléfono panorámico y tableta 4:3 en horizontal. Alternar **F9** en partida, abrir y cerrar pausa/tienda, recorrer todas las compras y probar volver a la aplicación tras perder foco. Una comprobación de límites geométricos no evalúa por sí sola legibilidad, superposición, ergonomía ni sensación del disparo; revisar también la pantalla y jugar.

## Alcance de la validación

La instalación actual de Unity dispone del módulo de Android; el módulo de iOS no está instalado. Que un módulo exista no confirma una build ni una ejecución en dispositivo. Los cambios comunes de interfaz y controles pueden comprobarse en el Editor, pero la rotación real, los recortes/notch, los dedos físicos, la suspensión del sistema, el coste térmico y el rendimiento necesitan pruebas en Android/iOS reales.

No se promete una cifra de FPS ni ausencia de bloqueos o cierres. Las comprobaciones anteriores tienen un alcance concreto y no cubren todas las escenas, rutas de hardware ni condiciones del sistema. Registrar por separado qué resoluciones, flujos, builds y dispositivos se hayan ejecutado y qué resultados se observaron.
