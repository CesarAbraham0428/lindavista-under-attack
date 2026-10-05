# Combate y vida

## Salud del jugador

`PlayerHealth` inicializa la salud en **5 de 5** al activar el personaje. `PlayerActions` conecta sus eventos con las animaciones. La vida no se guarda entre escenas. Un impacto válido:

1. Resta salud hasta un mínimo de cero.
2. Notifica al HUD con el evento `HealthChanged`.
3. Reproduce `TakeDamage` si el jugador sigue con vida.
4. Al llegar a cero, activa `Defeat` y detiene el movimiento.

Después de recibir daño, el jugador no acepta otra aplicación de daño durante **0,75 segundos**. La tecla **Q** invoca el mismo método de daño con una unidad solo en `Testing`; sirve para verificar la barra y la animación. La fila del HUD parpadea al recibir daño.

### Derrota de la partida

La partida termina si la salud del personaje llega a cero o si un enemigo que avanza hacia la entrada alcanza la posición inicial del jugador activo. Al activarse la derrota, se bloquean el movimiento, el apuntado, el disparo, el cambio de arma y las acciones de los enemigos. La pantalla usa carteles de madera del menú con el título **¡PERDISTE!** y el mensaje **¡Inténtalo otra vez!**, sin estadísticas ni saldo. Ofrece **REINTENTAR** (recarga la escena actual), **TIENDA** (mejoras de pistola) y **MENÚ** (abre `MainMenu`). El diseño se ajusta al tamaño y área segura de la pantalla; los botones admiten mouse, toque y navegación de teclado.

`PlayerHealthHUD` genera un Canvas con cinco segmentos y el contador numérico, sin nombre de personaje. Busca los componentes `PlayerHealth` activos y actualiza las filas si cambia el jugador activo. En `Gameplay` se muestra la fila del personaje seleccionado; en `Testing` pueden verse ambos. Los HUD se construyen en Play Mode.

## Armas del jugador

El disparo se procesa con un barrido `CircleCast` en física 2D. El primer collider válido consume el proyectil; si pertenece a un objeto con `EnemyHealth`, recibe el daño correspondiente. El proyectil excluye al jugador que lo disparó y a cualquier jugador.

| Arma | Daño | Velocidad | Intervalo mínimo | Radio de impacto |
| --- | ---: | ---: | ---: | ---: |
| Pistola | 2; mejorable a 3 y 4 | 22 | 0,40 s | 0,07 |

Solo está disponible la pistola. Su alcance es 18 unidades, tiene cargador de 12 balas y recarga real de 1,20 s con reserva ilimitada. El proyectil tiene un sprite sencillo y conserva el daño que tenía al dispararse. Las monedas y los triggers de decoración no bloquean los disparos. Los impactos en muros no crean marcas: las marcas visibles existentes son parte del escenario. El balance se edita en `Resources/PistolDefinition.asset`.

## Enemigos

`BasicEnemy` busca cada 0,25 segundos al jugador activo más cercano que no esté derrotado. Puede acercarse, detenerse a rango y atacar con un tiempo de preparación. La escena `Testing` contiene cuatro instancias con estos ajustes propios:

| Objeto | Estilo | Vida en `Testing` | Daño y cadencia |
| --- | --- | ---: | --- |
| `Enemy1` | Melee | 10 | 1 de daño; velocidad 1,5; intervalo entre ataques 1,4 s. |
| `Enemy2` | Pistola | 15 | 1 de daño; alcance 6; intervalo 1,8 s. |
| `Enemy3` | Melee con escudo | 30 | 2 de daño; seis impactos frontales dentro de 1,5 s rompen el escudo por 1,5 s. |
| `Enemy4` | SMG | 50 | 1 de daño por disparo; hasta tres disparos separados por 0,16 s por ataque; alcance 6,5. |

El valor inicial de `EnemyHealth.maxHealth` en el script es 3, pero cada enemigo de `Testing` y los prefabs lo sobrescriben con los valores de la tabla. Al agotarse la salud, el enemigo desactiva el collider, activa `Defeat`, emite una única muerte y desaparece después de 1,2 segundos. `EnemyLootDrop` crea una moneda recogible por un jugador vivo. El escudo también se abre durante la animación de golpe pesado.

Los enemigos pueden atacar a corta distancia o aplicar el impacto de sus disparos de inmediato. El código actual no crea una representación gráfica de los disparos enemigos. Los ataques de SMG hacen hasta tres intentos separados por 0,16 s; la protección del jugador de 0,75 s puede bloquear los impactos posteriores del mismo ataque.

### Oleadas y presentación del nivel

`LevelOneEnemySpawner`, colocado en `Escenario/Escenario_Tramo_5/E5_Enemigos`, prepara en el tramo 5 la composición del nivel elegido. Los enemigos aparecen ahí desde el inicio con su animación, pero con la IA de movimiento detenida. `CameraFollow2D` centra la cámara en la formación, la mantiene visible 1,1 segundos y después hace un paneo de 3,5 segundos hacia la entrada. Al comenzar ese paneo, el spawner activa un enemigo cada 1,65 segundos. El jugador no puede moverse, apuntar, disparar ni cambiar armas hasta que termina la presentación.

Los conteos iniciales, editables en el componente del spawner, son: Nivel 1 (índice 0), seis `Enemy1` y cuatro `Enemy2`; Nivel 2, seis `Enemy1`, cinco `Enemy2` y tres `Enemy3`; Nivel 3, seis `Enemy1`, cinco `Enemy2`, cuatro `Enemy3` y tres `Enemy4`; Nivel 4, ocho `Enemy1`, seis `Enemy2`, cinco `Enemy3` y cinco `Enemy4`. Los tipos se intercalan en la formación. Los valores de los niveles 2–4 son una base ajustable para las oleadas futuras.

La formación empieza en X=93 con 1,2 unidades entre enemigos; la cámara ajusta temporalmente el zoom para encuadrar la fila y vuelve al tamaño normal durante el paneo. Al activarse, cada `BasicEnemy` avanza hacia la posición inicial del jugador activo (X=-6,72 en la escena actual) y conserva su comportamiento de ataque hasta que llega o termina la partida.

Los prefabs reutilizables están en `Assets/_Project/Prefabs/Enemy1_Base.prefab` a `Enemy4_Base.prefab`; mantienen el sprite, el controlador de animación, salud y estilo de ataque de cada enemigo. Los sprites estáticos de muestra bajo `Enemigos` se ocultan para que la composición seleccionada no se duplique.

## Alcance actual

- `Testing` tiene instancias funcionales de los cuatro enemigos.
- `Gameplay` tiene composiciones para cuatro niveles y desbloqueo secuencial al guardar una victoria.
- La oleada del nivel 1 tiene 10 enemigos y 70 monedas en total. Tras eliminarla se permite recoger el dinero antes de finalizar.
- El cargador, las mejoras, el monedero y el guardado están conectados. Consulta [Progresión del nivel 1](progresion-nivel-1.md). La compra de munición y otras armas queda para la siguiente etapa.
