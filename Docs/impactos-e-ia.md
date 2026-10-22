# Impactos legibles y enemigos tácticos — niveles 1 a 3

## Impactos de las armas

- Las balas de Marco y la escopeta de César muestran un destello de contacto más visible al tocar un enemigo. El destello no agrega daño ni convierte la bala en un explosivo.
- El francotirador conserva su penetración de 2–4 enemigos según la mejora. Ahora cada enemigo dañado muestra el destello y su salpicadura propia, sin repetir el daño por colliders duplicados ni por permanecer dentro de un mismo enemigo.
- Un escudo que bloquea el tiro muestra un destello azulado y **detiene** la bala de francotirador: no recibe daño un enemigo situado detrás. El disparo que rompe el escudo sigue siendo bloqueado; el siguiente puede atravesarlo durante la apertura.
- El lanzagranadas de César **detona al contactar un enemigo durante el vuelo/rebote**, con la misma explosión visible del RPG. Impacto directo y área no duplican daño. Si falla, conserva su arco, rebote y mecha de 0,5 segundos al tocar el suelo. Las paredes siguen desviando la granada y protegiendo del daño de área.
- Los contactos a quemarropa usan la posición real de la colisión, evitando destellos en el origen del mundo.

No se cambian la cadencia, el consumo de munición, el daño ni las mejoras de las armas. Se reutiliza el arte existente; no se añaden imágenes generadas nuevas.

## Comportamiento táctico

`LevelOneEnemySpawner` activa `BasicEnemy.ConfigureTactics()` en los niveles 1, 2 y 3. Se mantienen las composiciones y el balance de escudos ajustados anteriormente.

- Los enemigos se detienen al tener al jugador a rango útil y preparan su ataque sin caminar sobre él.
- Revalidan distancia, dirección, salud y cobertura antes de aplicar daño y entre los disparos de una ráfaga. Un jugador que se aparta o rebasa al enemigo deja de recibir ese ataque.
- Dan prioridad a un jugador al que realmente pueden atacar, sin elegir objetivos fuera del mismo plano de combate.
- Los enemigos que siguen la misma ruta mantienen separación. Los de puñetazos/escudo pueden adelantar a tiradores para que un enemigo disparando no atasque a todo el grupo.
- Un barrido del cuerpo impide caminar a través de obstáculos sólidos. No es búsqueda de rutas: una pared física que cierre toda la calle detiene legítimamente el avance.
- Si el jugador queda detrás, continúan hacia la entrada sin girarse para dispararle. Sin un objetivo válido también siguen defendiendo esa ruta.
- Pausa, tienda, muerte y resultado detienen o cancelan las acciones correspondientes.

El nivel 4 y las instancias sueltas de `Testing` no reciben automáticamente el movimiento táctico. Conservan su comportamiento anterior, con las protecciones compartidas de pausa y final de partida. No se aumentan daño, vida ni cadencia enemigos.

## Comprobaciones

`CombatFeedbackAndAIChecks.RunRuntime()` verifica contactos y movimiento con física real en una escena vacía de Play Mode y un perfil temporal. Rechaza ejecutarse sobre una partida con actores activos. El acceso está en `Tools > Lindavista > Verify impact feedback and tactical enemies (Play Mode)`.

También se mantienen las comprobaciones de penetración del francotirador, vuelo/mecha de granadas, explosivos contra escudos, ataques direccionales y configuración de niveles. Las comprobaciones lógicas no sustituyen jugar los tres niveles para valorar su dificultad ni probar rendimiento en Android.

Validación del 17 de octubre de 2026 en una copia aislada: 105 comprobaciones de impactos/IA, 138 de ataques direccionales, 133 de explosivos contra escudos y 1628 comprobaciones deterministas de oleadas con ambos personajes, además de las 69/76 de configuración de niveles. Se revisó una captura del destello de daño frente al de bloqueo. La prueba de oleadas avanza manualmente la secuencia de liberación: no es una simulación de una partida completa en tiempo real.
