# Niveles y entorno

## Variantes de horario

La selección representa cuatro variantes visuales del nivel. No hay un reloj que avance automáticamente durante la partida. `CharacterLevelSelectionController` guarda el índice elegido como `Lindavista.SelectedLevel`; al cargar `Gameplay`, `SelectedLevelEnvironment` aplica la configuración una sola vez.

| Índice guardado | Tarjeta / horario | Configuración ambiental actual |
| ---: | --- | --- |
| 0 | Nivel 1 · 1 p. m. | Usa el fondo diurno que ya está configurado en la escena; no activa un grupo adicional de daño. |
| 1 | Nivel 2 · 6 p. m. | Fondo degradado de atardecer, sprite de sol de las 6 p. m. y `Daño_Nivel_2`. |
| 2 | Nivel 3 · 9 p. m. | Fondo degradado nocturno, sprite de luna de las 9 p. m. y `Daño_Nivel_3`. |
| 3 | Nivel 4 · 3 a. m. | Fondo degradado azul muy oscuro, sprite de luna de las 3 a. m. y `Daño_Nivel_4`. |

Los degradados de los niveles 2, 3 y 4 se generan en memoria al entrar en la escena. El script también cambia el sprite de los objetos cuyo nombre termina en `_Sol`. La cámara recibe el color correspondiente como fondo.

## Escenario lateral

`Gameplay` organiza el arte bajo `Escenario`, con grupos `Escenario_Tramo_1` a `Escenario_Tramo_5`. En ellos están colocados edificios, árboles, nubes, postes y lámparas, muros, grafitis, señales y objetos de calle. La cámara conserva su posición vertical y empieza a seguir al jugador horizontalmente después de que este supere la posición X 3; el seguimiento tiene suavizado y límites de cámara.

La escena también contiene seis objetos de bache (`bache1` a `bache6`), arte de muro agrietado y objetos de muro con impactos. El jugador puede descubrir las partes del escenario al avanzar, pero esos objetos ya están ubicados en la escena. No se encontró código que los haga aparecer por distancia, cambie un bache, o añada un impacto al disparar.

Al entrar a `Gameplay`, la cámara presenta primero la composición de enemigos del tramo 5 y luego recorre el escenario hacia la entrada del jugador. El spawner libera a los enemigos de uno en uno mientras la cámara vuelve; todos avanzan automáticamente hacia X=-6,72. Las cantidades por nivel se configuran en `E5_Enemigos`, y los niveles posteriores agregan tipos de enemigo y aumentan la oleada.

## Marcas de daño en muros

Hay dos tipos de contenido visual:

- `Muro_Impactos_1` a `Muro_Impactos_4` y el sprite `muro_con_disparos.png` son decoración colocada en la escena.
- Los grupos `Daño_Nivel_2`, `Daño_Nivel_3` y `Daño_Nivel_4` se activan según el nivel elegido. El nivel 1 no activa esos grupos.

Por ahora, disparar contra el muro no crea una marca nueva ni modifica el sprite: el proyectil lógico se consume al encontrar el primer collider. Los baches y las marcas tampoco tienen una reacción visual implementada por scripts.
