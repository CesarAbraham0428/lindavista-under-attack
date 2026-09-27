# Animaciones

Las animaciones de personajes se implementan con `AnimatorController` y clips de sprites. Los controladores principales están en `Assets/_Project/Animations` y los clips bajo `Animations/Players` y `Animations/Enemies`.

## Marco y César

Ambos controladores contienen clips para:

- `Idle` y `Walk`.
- `Damage` y `Defeat`.
- Fuego y recarga de pistola, SMG y RPG.
- Un clip `Aim`.

`PlayerMovement` cambia el booleano `IsMoving` cuando hay desplazamiento. `PlayerActions` activa los triggers de fuego y recarga del arma actual; al recibir daño activa `TakeDamage`; al quedarse sin vida o pulsar **E** activa `Defeat`. Después de la derrota, el movimiento queda bloqueado.

El sprite del jugador solo se voltea horizontalmente según la dirección del movimiento. El cursor define el punto de apuntado y la cruz de pantalla, pero el script actual no pone `IsAiming` en `true` (lo inicializa en `false`); por tanto, el clip `Aim` existe como recurso, pero no se activa desde el flujo de código revisado.

## Enemigos

Los controladores de `Enemy1` a `Enemy4` usan `IsMoving` y triggers de ataque, daño y derrota. Sus clips disponibles incluyen:

- `Enemy1`: puñetazo.
- `Enemy2`: disparo de pistola.
- `Enemy3`: golpe pesado con escudo.
- `Enemy4`: fuego de SMG.

`BasicEnemy` actualiza `IsMoving` durante el acercamiento y lanza `Attack` cuando llega a distancia de ataque. `EnemyHealth` lanza `TakeDamage` o `Defeat` según corresponda.

Los sprites y el controlador de `BossTruck` (reposo, caminar, disparo, daño y derrota) existen en el repositorio, pero no se encontró una instancia de ese jefe en `Gameplay` o `Testing`.
