# Estado de vida, tienda, armas y daño

Este resumen describe el código y los valores de los prefabs actuales. Los valores de combate se inspeccionaron en los prefabs reutilizables `Enemy1_Base`–`Enemy4_Base`; las conclusiones de balance son una lectura de esos valores, no una afirmación de que todas las dificultades se hayan validado jugando.

## Vida

Cada personaje empieza con 5 de vida. `PlayerHealth` reinicia la vida cuando se activa la instancia; la vida y la munición no se guardan entre escenas ni partidas. Después de un impacto válido, ese personaje ignora daño durante 0,75 segundos. La protección es individual para cada `PlayerHealth`.

Los enemigos aplican daño desde ataques cuerpo a cuerpo o desde una comprobación de impacto instantánea para sus disparos. Al llegar a cero, el juego termina y bloquea las acciones. También hay derrota si un enemigo de la oleada alcanza la entrada, aunque al jugador todavía le quede vida. **Q** prueba daño de un punto y **E** intenta agotar la vida; ambas teclas existen solo en `Testing`.

El HUD muestra segmentos y cifra de vida del jugador activo en `Gameplay`; `Testing` puede mostrar una fila por personaje activo. No hay una barra de vida enemiga visible. Las rutinas de daño no persisten el saldo: las monedas ya recogidas permanecen en el perfil, mientras que las monedas que sigan en la escena se pierden al reiniciar.

## Tienda y progresión

Un perfil nuevo tiene 0 monedas, solo la pistola básica y el nivel 1 desbloqueado. Las recompensas físicas valen 5, 10, 15 y 20 monedas para los enemigos 1–4. La muerte deja caer una moneda; solo el contacto con un personaje vivo acredita su valor. Marco y César comparten saldo, mejora y niveles completados.

La tienda abre desde **Tienda** en `MainMenu`, **TIENDA** en la pantalla de derrota y **Mejorar pistola** al ganar. Cada compra guarda inmediatamente el progreso: la mejora de nivel 1 cuesta 25 monedas y eleva el daño de 2 a 3; la siguiente cuesta 60 y eleva el daño a 4. No hay más niveles de mejora. El saldo y las mejoras sobreviven derrota, reintento y cambio de personaje.

El progreso se guarda en `progression-v1.json`, con respaldo `.bak` y escritura temporal antes de reemplazar el archivo principal. Si los datos principal y de respaldo no son recuperables, se preservan y el perfil bloquea nuevas transacciones en lugar de sobrescribirlos. La selección de personaje, nivel y volumen usa `PlayerPrefs`, aparte de este perfil.

## Armas

La pistola es la única arma equipada y poseída actualmente. La metralleta (SMG) y el RPG aparecen en el arte y en la interfaz, pero no se pueden desbloquear, comprar ni disparar. Pulsar `2` o `3` no equipa esas armas. La reserva de la pistola es ilimitada.

| Ajuste de pistola | Valor actual |
| --- | ---: |
| Daño por impacto | 2, 3 o 4 según mejora |
| Intervalo mínimo | 0,40 s |
| Alcance | 18 unidades |
| Velocidad del proyectil | 22 unidades/s |
| Radio de barrido | 0,07 unidades |
| Cargador / recarga | 12 balas / 1,20 s |

Se puede mantener el disparo; al quedarse sin munición comienza la recarga automática. Un disparo conserva el daño que tenía al salir. El proyectil usa un barrido de colisión para no saltarse objetos entre cuadros y se consume al chocar con el primer objeto válido. Las monedas y los triggers de decoración no lo detienen.

## Daño de enemigos

| Prefab | Vida | Daño por impacto | Ataque configurado | Golpes de pistola al daño base para derrotarlo* |
| --- | ---: | ---: | --- | ---: |
| `Enemy1_Base` | 10 | 1 | Melee; velocidad 1,5; intervalo 1,4 s; preparación 0,25 s | 5 |
| `Enemy2_Base` | 15 | 1 | Pistola; alcance 6; intervalo 1,8 s; preparación 0,30 s | 8 |
| `Enemy3_Base` | 30 | 2 | Escudo frontal; melee; intervalo 2 s; preparación 0,45 s | 15** |
| `Enemy4_Base` | 50 | 1 | SMG; alcance 6,5; hasta 3 tiros cada 0,16 s; intervalo 2 s | 25 |

\* Cálculo simple con 2 de daño por bala, sin impactos bloqueados.  
\** El escudo bloquea los disparos frontales mientras está cerrado. Se rompe tras 6 impactos frontales dentro de 1,5 s y queda abierto 1,5 s; durante la ventana abierta el enemigo también detiene su ataque. Los disparos por detrás sí pueden dañarlo.

En ataques melee, un impacto de 2 puntos del enemigo 3 equivale al 40 % de la vida del jugador. La ráfaga de SMG de tres tiros cubre 0,32 s entre su primer y último intento, menos que la protección de 0,75 s del jugador; por ello, en condiciones normales solo el primer tiro de cada ráfaga causa daño. Los disparos enemigos se resuelven de inmediato y no tienen sprite de proyectil visible.

## Alcance del daño ambiental y puntos para balancear

Aunque algunos grupos de escenario se llaman `Daño_Nivel_2`, `Daño_Nivel_3` y `Daño_Nivel_4`, `SelectedLevelEnvironment` únicamente los activa o desactiva según la variante visual. No hay un componente de peligro que llame a `PlayerHealth.TakeDamage` desde esos grupos; hoy no causan daño. El daño jugable viene de los enemigos y de las teclas de prueba.

La protección temporal reduce la ráfaga de SMG a un solo impacto por ataque. El enemigo 3, en cambio, puede retirar dos de los cinco puntos de vida de una vez. Ambos resultados salen directamente de las cifras configuradas; deben tenerse en cuenta cuando se haga la siguiente pasada de balance. Las cuatro portadas horarias son variantes de entorno con composiciones y recompensas compartidas por el nivel, no cuatro mecánicas distintas de daño.

Consulta [Combate y vida](combate-y-vida.md) y [Progresión](progresion-nivel-1.md) para la descripción operativa, la secuencia de la oleada y las rutas de implementación.
