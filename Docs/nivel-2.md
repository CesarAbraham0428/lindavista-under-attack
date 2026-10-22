# Nivel 2 — oleada de atardecer

El nivel 2 reutiliza `Gameplay`, los controles, las armas de cada personaje y el flujo de recogida/victoria del nivel 1. Se conserva su ambiente de las 6 p. m. y la decoración existente. No hay una escena de nivel 2 separada.

## Ajuste de dificultad

Se mantienen **14 enemigos**: seis básicos, cinco con pistola y tres con escudo. La secuencia predeterminada ahora es:

`Básico, Pistola, Básico, Pistola, Escudo, Básico, Pistola, Básico, Escudo, Pistola, Básico, Pistola, Escudo, Básico`.

Así, los primeros cuatro enemigos refuerzan lo aprendido en el nivel 1 antes de introducir el escudo. Los enemigos con escudo no aparecen en la presentación inicial de tres enemigos.

- Máximo de seis enemigos vivos de la oleada simultáneamente. Cuando se alcanza el límite, los refuerzos esperan; no desaparecen ni se descuentan del total.
- Intervalo de salida de 3,6 segundos, con 5,5 segundos después de liberar un enemigo con escudo.
- Se conserva la comprobación de espacio libre en el punto de aparición.
- El escudo del nivel 2 se abre después de cuatro impactos frontales consecutivos dentro de la ventana existente y queda vulnerable 2,5 segundos. El impacto que rompe el escudo no resta vida; los siguientes sí.
- La salud del enemigo con escudo sigue siendo 30. No cambian la salud, velocidad, daño ni recompensas de los prefabs.
- Los seis impactos y 1,5 segundos de apertura del prefab compartido siguen vigentes en el nivel 4 y en `Testing`. El nivel 3 tiene su propio ajuste de cinco impactos/dos segundos; consulta [Nivel 3](nivel-3.md).

La recompensa monetaria base de la oleada sigue siendo 125 monedas recogibles (30 + 50 + 45), además de las probabilidades existentes de suministros. Matar al último enemigo permite recoger antes de pulsar **FINALIZAR NIVEL**; finalizar guarda la victoria y desbloquea el nivel 3. Llegar a la entrada o perder toda la salud continúa siendo derrota.

## Dónde ajustar

Fuera de Play Mode, abre `Gameplay` y selecciona `Escenario/Escenario_Tramo_5/E5_Enemigos`.

En `LevelOneEnemySpawner`, la sección **Level 2: gradual shield introduction** contiene la apertura, separación entre escudos, máximo de enemigos, intervalos, impactos para romper el escudo y duración de su apertura. Los valores del nivel 1 y las composiciones de los niveles 3/4 se conservan.

## Comprobaciones

`Tools > Lindavista > Verify level 2 configuration` verifica la escena real en una escena de previsualización, comprueba composiciones normales y personalizadas, la configuración del escudo y el desbloqueo secuencial con un archivo temporal. No modifica la escena abierta, el monedero ni el progreso real.

Prueba manual recomendada: completar nivel 1, elegir nivel 2, jugar con Marco y César, verificar la aparición gradual de escudos, su apertura por disparos, recogida de recompensas, victoria y desbloqueo del nivel 3. La dificultad final aún necesita valoración jugando; las comprobaciones automáticas no sustituyen esa evaluación.

Validación del 17 de octubre de 2026: 69 comprobaciones de configuración y 252 comprobaciones deterministas en Play Mode en una copia aislada. Incluyen regresión de la composición del nivel 1, oleada completa con ambos personajes, espera/reanudación por capacidad, apertura y cierre de escudo, disparos y munición por personaje, 70/125 monedas recogibles, recogida antes de victoria, desbloqueo del nivel siguiente y cancelación de refuerzos tras derrota. La prueba avanza manualmente la secuencia de la oleada; no mide la dificultad de una partida jugada ni constituye una prueba en un teléfono físico.
