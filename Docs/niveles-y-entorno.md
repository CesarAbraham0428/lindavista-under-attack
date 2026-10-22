# Niveles y entorno

## Variantes de horario

La selección representa cuatro variantes visuales del nivel. No hay un reloj que avance automáticamente durante la partida. `CharacterLevelSelectionController` guarda el índice elegido como `Lindavista.SelectedLevel`; al cargar `Gameplay`, `SelectedLevelEnvironment` aplica la configuración una sola vez.

| Índice guardado | Tarjeta / horario | Configuración ambiental actual |
| ---: | --- | --- |
| 0 | Nivel 1 · 1 p. m. | Usa el fondo diurno configurado en la escena; no activa grupos decorativos adicionales. |
| 1 | Nivel 2 · 6 p. m. | Fondo degradado de atardecer, sprite de sol de las 6 p. m. y el grupo visual `Daño_Nivel_2`. |
| 2 | Nivel 3 · 9 p. m. | Fondo degradado nocturno, sprite de luna de las 9 p. m. y el grupo visual `Daño_Nivel_3`. |
| 3 | Nivel 4 · 3 a. m. | Fondo degradado azul muy oscuro, sprite de luna de las 3 a. m. y el grupo visual `Daño_Nivel_4`. |

Los grupos `Daño_Nivel_2`, `Daño_Nivel_3` y `Daño_Nivel_4` cambian de visibilidad según la selección, pero solo contienen presentación del escenario y no infligen daño. El daño que sí está implementado proviene de ataques enemigos.

Los degradados de los niveles 2, 3 y 4 se generan en memoria al entrar en la escena. El script también cambia el sprite de los objetos cuyo nombre termina en `_Sol`. La cámara recibe el color correspondiente como fondo.

## Escenario lateral

`Gameplay` organiza el arte bajo `Escenario`, con grupos `Escenario_Tramo_1` a `Escenario_Tramo_5`. En ellos están colocados edificios, árboles, nubes, postes y lámparas, muros, grafitis, señales y objetos de calle. La cámara conserva su posición vertical y empieza a seguir al jugador horizontalmente después de que este supere la posición X 3; el seguimiento tiene suavizado y límites de cámara.

La escena también contiene seis objetos de bache (`bache1` a `bache6`), arte de muro agrietado y objetos de muro con impactos. El jugador puede descubrir las partes del escenario al avanzar, pero esos objetos ya están ubicados en la escena. No se encontró código que los haga aparecer por distancia, cambie un bache, o añada un impacto al disparar.

Al entrar a `Gameplay`, la cámara presenta primero la composición de enemigos del tramo 5 y luego recorre el escenario hacia la entrada del jugador. El spawner libera a los enemigos de uno en uno mientras la cámara vuelve; todos avanzan automáticamente hacia X=-6,72. Las cantidades por nivel se configuran en `E5_Enemigos`, y los niveles posteriores agregan tipos de enemigo y aumentan la oleada.

El nivel 2 mantiene seis enemigos básicos, cinco con pistola y tres con escudo, pero introduce los escudos en las posiciones 5, 9 y 13, limita la oleada a seis enemigos vivos simultáneos y amplía su apertura de escudo solo en este nivel. Parámetros y pruebas en [Nivel 2](nivel-2.md).

El nivel 3 conserva seis básicos, cinco con pistola, cuatro con escudo y tres con metralleta. Introduce la metralleta en las posiciones 7, 12 y 18, limita la oleada a siete enemigos vivos y usa una apertura de escudo intermedia. Parámetros y pruebas en [Nivel 3](nivel-3.md). El nivel 1 y la configuración del nivel 4 no cambian.

## Estatura, suelo y orden de los personajes

Los cuatro primeros enemigos conservan su estatura de reposo y las escalas de
sus prefabs. Las hojas de caminar se ajustan por su silueta visible: la pose más
alta coincide con la de reposo, sin hacer crecer al personaje al iniciar la
marcha. Las pequeñas flexiones naturales de las otras poses se conservan. Se
actualizan PPU y pivotes de las ocho poses, no las imágenes, sus identificadores,
la duración de los clips ni las animaciones de ataque/derrota.

`BasicEnemy.standingFootY` guarda el anclaje local de las suelas del sprite de
reposo, medido en el Editor. `PlaceOnGround()` alinea ese punto con el suelo sin
reescalar el objeto ni modificar las dimensiones o el offset local del collider.
La oleada usa **Y=-4,44 como superficie del suelo**, ya no como centro de cada
enemigo; se elimina el desnivel alternado entre apariciones. Los cuatro actores
de `Testing` y sus prefabs también quedan alineados en esa línea. Los distintos
pivotes hacen que sus coordenadas Y de Transform sean diferentes: eso es correcto,
lo que debe coincidir son las suelas, no los centros de los dibujos.

El orden en la capa de dibujo existente queda así:

| Elementos | Order in Layer |
| --- | ---: |
| Escenario y decoración existentes | Se conservan sus valores, hasta 5 en la calle |
| Enemigos 3 y 4: escudo y metralleta | 6 |
| Jugadores Marco y César | 7 |
| Enemigos 1 y 2: puños y pistola | 8 |

Los grandes se dibujan detrás, pero no se cambia su posición X/Z ni el orden de
aparición. La separación táctica compara la línea del suelo en lugar del centro
del personaje, para que tener distinta estatura no haga que se considere otra
calle o permita atravesar la cola de aliados. No cambian salud, daño, cadencia,
alcance ni composición de las oleadas.

Comprobaciones del Editor: **Tools > Lindavista > Verify enemy height, ground and
draw order**. Para revisar anclajes, colliders, separación y ataques en ejecución,
usar una escena vacía en Play Mode y **Verify enemy ground at runtime (empty Play
Mode scene)**. La lectura de píxeles solo se usa en estas herramientas del Editor,
no en el juego. Si se reemplazan las ilustraciones en el futuro, revisar los
anclajes y usar **Normalize walking height of enemies 1-4** antes de verificar.

Validación en copia aislada con Unity 6000.3.23f1: 85 comprobaciones de estatura,
pivotes y escena, y 64 en Play Mode (32 poses, colliders, colocación repetida,
separación de aliados de distinta estatura y daño real de los cuatro roles en
ambas direcciones). Pasaron también las regresiones anteriores de oleadas de
los tres primeros niveles, escudos, impactos y reutilización de disparos. Se
revisó una captura comparando reposo/caminar de los cuatro enemigos alineados.

## Marcas de daño en muros

Hay dos tipos de contenido visual:

- `Muro_Impactos_1` a `Muro_Impactos_4` y el sprite `muro_con_disparos.png` son decoración colocada en la escena.
- Los grupos `Daño_Nivel_2`, `Daño_Nivel_3` y `Daño_Nivel_4` se activan según el nivel elegido. El nivel 1 no activa esos grupos.

Por ahora, disparar contra el muro no crea una marca nueva ni modifica el sprite: el proyectil lógico se consume al encontrar el primer collider. Los baches y las marcas tampoco tienen una reacción visual implementada por scripts.
