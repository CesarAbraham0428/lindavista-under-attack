# Números de daño

`Assets/_Project/Scripts/Combate/DamageNumber.cs` contiene la presentación del daño. Se integra una sola vez en `EnemyHealth.TakeDamage(int)`, después de descontar salud y antes de las notificaciones de muerte. Cubre las seis armas de Marco y César, los cuatro tipos de enemigo y el jefe; un arma futura que use `EnemyHealth` recibe la misma presentación.

El valor es la vida realmente descontada: un disparo de 30 contra un enemigo con 8 puntos restantes muestra **8**. Los impactos bloqueados por el escudo, el daño no positivo y los golpes sobre enemigos muertos no crean números. Las rutas existentes de explosiones y perforación conservan su deduplicación por enemigo.

## Presentación y ajustes

- Texto de 26 unidades a resolución de referencia 1920 × 1080, fuente `LilitaOne`, color crema y contorno oscuro. El tamaño de referencia, la fuente y el escalado coinciden con la interfaz del juego.
- Duración de 0,72 segundos: aparece sobre el borde superior del sprite, sube 40 unidades de interfaz y se desvanece. Cinco posiciones horizontales alternan los impactos consecutivos del mismo enemigo.
- Sigue la traslación del enemigo vivo; al morir o desaparecer conserva la última posición. Su vida es independiente de la del enemigo y puede terminar después del golpe que activa la victoria.
- La pausa detiene la animación. La proyección usa la cámara de juego después de su seguimiento, también al cambiar resolución o proporción de pantalla.

Los ajustes principales están en `Capacity`, `Lifetime` y `FontSize`; los desplazamientos y el desvanecido están en `Draw()`. `EnemyHealth.DamageNumberAnchor` define la posición inicial sobre el sprite y ofrece alternativas con collider y transform.

## Recursos y plataformas

El módulo utiliza `SceneCombatPool<DamageNumber>`. `CombatWarmup` prepara 24 elementos y los glifos numéricos durante la introducción. Se permiten hasta 96 números activos; si se alcanza el límite, el siguiente impacto reutiliza el más antiguo sin alterar el daño del combate. Los números entre 0 y 100 reutilizan cadenas precalculadas. La actualización no busca objetos ni componentes y conserva la cámara en caché.

Hay un solo Canvas para la presentación, con orden 50, debajo de los HUD y los menús. No tiene `GraphicRaycaster` y los textos tienen `raycastTarget = false`, por lo que no interceptan mouse ni controles táctiles. La fuente ya está incluida en Resources; no hay sprites nuevos, paquetes adicionales ni código dependiente de Windows. Los objetos y sus pools pertenecen a la escena y se liberan al descargarla; los estáticos se reinician al entrar en Play Mode.

## Verificación

`DamageNumberChecks.RunRuntime()` se ejecuta en una escena vacía en Play Mode, sin personajes ni proyectiles activos. También está disponible en **Tools → Lindavista → Verify damage numbers (empty Play Mode scene)**. No modifica el perfil ni consume munición. Las pruebas están en la carpeta `Editor` y no forman parte del juego compilado.

Verificado el 17 de octubre de 2026 en Unity 6000.3.23f1, Editor de Windows con Android como plataforma de compilación:

- **422 comprobaciones**: proyectiles reales de las seis armas, daño efectivo, bloqueo del escudo, muerte y destrucción, movimiento, pausa, perforación, daño de área, colliders duplicados, alternancia de posiciones, ráfaga de 200 impactos y reutilización posterior.
- Las comprobaciones existentes de explosivos (**135**) y pools de combate (**3661**) también pasaron.
- Descargar la escena dejó **0 números y 0 Canvas**; precalentar la siguiente creó 24 elementos y las 422 comprobaciones volvieron a pasar.
- La invocación directa de 1000 actualizaciones del módulo con cinco números activos y tiempo congelado registró **0 bytes de asignaciones administradas** en el hilo medido. Esta medición no incluye el renderizado completo ni demuestra FPS en un dispositivo.
- Revisión visual a 1920 × 1080 horizontal y 1040 × 461. Captura de la escena de prueba con arte real: `Library/DamageNumberChecks/preview-landscape-verified.png` (temporal, no versionada).

La compilación y las pruebas se realizaron en el Editor; no se ha ejecutado una APK en un teléfono Android físico.
