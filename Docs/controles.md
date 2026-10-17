# Controles

El juego usa el paquete **Input System** de Unity. Los controles están disponibles en `Gameplay` y `Testing`; la introducción, la pausa y el resultado bloquean las acciones del personaje.

## Teclado y mouse

| Acción | Control | Comportamiento |
| --- | --- | --- |
| Mover a la izquierda | `A` o `←` | Camina hacia la izquierda. |
| Mover a la derecha | `D` o `→` | Camina hacia la derecha, hasta el límite horizontal configurado. |
| Apuntar | Mouse | Actualiza la dirección y el punto objetivo mientras el cursor está dentro del área visible del juego. |
| Disparar | Mantener clic izquierdo | Dispara repetidamente el arma seleccionada, sujeto a su cadencia. |
| Elegir arma | `1` / `2` / `3` | Selecciona una de las tres armas del personaje; también se puede pulsar su icono en el HUD. |
| Recargar | `R` | Rellena el cargador hasta su capacidad o la munición total disponible. Al mantener disparo con cargador vacío se intenta recargar automáticamente. |
| Usar pastilla | `H` o botón de pastillas | Reproduce la animación de consumo y recupera exactamente 1 punto de vida al tragar. |
| Pausar / continuar | `Esc` o botón **PAUSA** | Abre la pausa. Tocar el letrero **PAUSA** continúa la partida. |
| Previsualizar controles móviles | `F9` | Solo en el Editor: muestra u oculta los controles táctiles. |
| Probar daño | `Q` | Solo en `Testing`: intenta quitar 1 punto de vida. |
| Forzar derrota | `E` | Solo en `Testing`: intenta quitar la vida restante, respetando la protección temporal. |

El puntero de apuntado es una pequeña cruz amarilla. El clic solo dispara cuando el cursor está dentro del viewport del juego y fuera de la interfaz. Cuando se muestra el HUD táctil, el disparo con mouse se desactiva para que el clic en los controles no genere un segundo disparo. La dirección de apuntado usa la distancia configurada por arma; los proyectiles emitidos dentro de la cámara pueden recorrer la vista actual. Las granadas mantienen su vuelo y detonación propios.

## Armas y munición

Las tres ranuras están disponibles desde el inicio para cada personaje. `CharacterWeapons`, `WeaponDefinition` y los assets de definición de César determinan estos valores base:

| Ranura / tecla | Marco | Cargador / recarga | César | Cargador / recarga |
| --- | --- | --- | --- | --- |
| `1` | Pistola | 12 / 1,20 s | Escopeta | 6 / 2,00 s |
| `2` | Metralleta | 30 / 1,20 s | Francotirador | 5 / 2,20 s |
| `3` | Lanzacohetes | 2 / 1,20 s | Lanzagranadas | 2 / 2,00 s |

La munición es finita y se guarda por personaje y ranura. El contador total incluye las balas que todavía están en el cargador: cada disparo descuenta una unidad del total y del cargador; recargar no vuelve a descontarlas. Cambiar de arma conserva las balas del cargador durante la partida. Una partida nueva rellena cada cargador hasta su capacidad, limitada por el total persistido.

| Ranura | Total inicial de perfil nuevo | Total máximo | Paquete de tienda | Coste |
| --- | --- | --- | --- | --- |
| `1` | 48 | 60 | +12 | 10 monedas |
| `2` | 90 | 180 | +30 | 20 monedas |
| `3` | 6 | 12 | +2 | 35 monedas |

Estos valores de `ShopBalance` son iguales para ambos personajes, aunque sus armas y cargadores difieran. La tienda exige espacio para el paquete completo; no vende paquetes parciales. Saldo y niveles desbloqueados son compartidos; vida, velocidad, mejoras de armas y munición pertenecen a cada personaje.

## Pastillas de vida

La tienda vende **una pastilla por 15 monedas**, con **un máximo de diez por personaje**. El contador y el botón de compra se actualizan al cambiar entre Marco y César. Las pastillas compradas y las gastadas se guardan; los perfiles anteriores empiezan con cero pastillas y conservan su progreso.

Cada pastilla recupera **un punto de la vida actual**, sin aumentar su máximo. No se permite usarla con vida llena, inventario vacío, durante recarga o daño, ni durante la introducción, pausa, tienda o resultado. El botón aparece debajo de PAUSA y funciona con clic o toque. Durante el consumo se detienen el movimiento, el disparo y el cambio de arma. Recibir daño interrumpe la animación; si todavía no había llegado al gesto de tragar, no se gasta la unidad. La curación nunca resucita al personaje.

## Controles táctiles

`MobileControlsHUD` construye las flechas izquierda/derecha, el botón **REC** y una zona de **APUNTA / DISPARA** con arrastre. El selector de las tres armas lo construye `WeaponSelectionHUD`. El apuntado mantiene el disparo mientras se presiona y permite arrastrar fuera de su círculo. Al salir de una flecha se libera el movimiento.

Cada región conserva el identificador de su dedo: soltar otro dedo no cancela una acción en curso. Se puede mover y apuntar simultáneamente; mantener ambas flechas compensa el movimiento. Pausar, abrir la tienda, terminar la partida, desactivar el control o pasar la aplicación a segundo plano cancela los toques para evitar acciones retenidas.

En móvil los controles aparecen automáticamente y se ocultan durante pausa, tienda y resultado. En el Editor, **F9** permite previsualizarlos con mouse; la comprobación automática también puede simular varios identificadores de puntero. Los controles se colocan dentro de `Screen.safeArea`. La versión móvil permite las dos orientaciones horizontales.

La tienda usa una lista vertical desplazable en móvil y en ventanas de hasta 800 píxeles de alto. Desliza o usa la rueda del mouse para alcanzar todas las mejoras. Consulta [Móvil y rendimiento](movil-y-rendimiento.md) para módulos, comprobaciones y límites de validación.
