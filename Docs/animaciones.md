# Animaciones

Las animaciones de personajes se implementan con `AnimatorController` y clips de sprites. Los controladores principales están en `Assets/_Project/Animations` y los clips bajo `Animations/Players` y `Animations/Enemies`.

## Marco y César

Ambos controladores contienen clips para:

- `Idle` y `Walk`.
- `Damage` y `Defeat`.
- Fuego y recarga de pistola, SMG y RPG.
- Un clip `Aim`.
- `Marco_UseHealthConsumable` y `Cesar_UseHealthConsumable`: ocho fotogramas existentes de `Art/UI/Health`, en orden de lectura, durante 1,2 segundos y sin bucle.

`PlayerMovement` cambia el booleano `IsMoving` cuando hay desplazamiento. `PlayerActions` evalúa directamente el estado de fuego del arma actual antes de emitir cada proyectil, para que el sprite con el arma ya sea visible al disparar; usa triggers para recarga, `TakeDamage` y `Defeat`. Después de la derrota, el movimiento queda bloqueado.

`PlayerActions.TryUseHealthConsumable()` reproduce directamente el estado de consumo. El sexto fotograma aplica +1 de vida y descuenta una pastilla mediante una transacción guardada; al terminar vuelve al reposo. Daño, derrota, tienda y bloqueo de entrada cancelan el consumo pendiente. `HealthConsumableSetup.Generate()` permite reconstruir los clips y los enlaces de ambos controladores conservando los rectángulos e identificadores de los recortes. Ajusta sus píxeles por unidad y pivotes para mantener el cuerpo de cinco unidades y los pies en el mismo suelo.

Durante el disparo, el sprite se voltea hacia el objetivo, incluso si el jugador camina en sentido contrario. Fuera del disparo conserva la orientación del movimiento. `PlayerMuzzle` localiza el cañón en el sprite de cada personaje y arma, considerando fotograma, pivote, escala y reflejo; las balas y el fogonazo salen de ese punto. El cohete del RPG mide 1,1 unidades. El cursor define el punto de apuntado y la cruz de pantalla, pero el script no pone `IsAiming` en `true` (lo inicializa en `false`); el clip `Aim` no se activa desde este flujo.

## Enemigos

Los controladores de `Enemy1` a `Enemy4` usan `IsMoving` y triggers de ataque, daño y derrota. Sus clips disponibles incluyen:

- `Enemy1`: puñetazo.
- `Enemy2`: disparo de pistola.
- `Enemy3`: golpe pesado con escudo.
- `Enemy4`: fuego de SMG.

`BasicEnemy` actualiza `IsMoving` durante el acercamiento y lanza `Attack` cuando llega a distancia de ataque. `EnemyHealth` lanza `TakeDamage` o `Defeat` según corresponda.

Los sprites y el controlador de `BossTruck` (reposo, caminar, disparo, daño y derrota) existen en el repositorio, pero no se encontró una instancia de ese jefe en `Gameplay` o `Testing`.
