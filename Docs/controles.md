# Controles

El juego usa el paquete **Input System** de Unity. La tabla describe las acciones implementadas en teclado y mouse.

## Teclado y mouse

| Acción | Control | Comportamiento |
| --- | --- | --- |
| Mover a la izquierda | `A` o `←` | Camina hacia la izquierda. |
| Mover a la derecha | `D` o `→` | Camina hacia la derecha, hasta el límite horizontal configurado. |
| Apuntar | Mouse | Actualiza la dirección y el punto objetivo mientras el cursor está dentro del área visible del juego. |
| Disparar | Mantener clic izquierdo | Dispara repetidamente el arma seleccionada, sujeto a su cadencia. |
| Elegir pistola | `1` | Selecciona pistola. |
| Elegir subfusil | `2` | Selecciona SMG. |
| Elegir lanzacohetes | `3` | Selecciona RPG. |
| Recargar | `R` | Activa la animación de recarga del arma elegida. |
| Probar daño | `Q` | En `Testing` y `Gameplay`, intenta quitar 1 punto de vida al personaje activo y activa la animación de daño. Es una entrada de depuración. |
| Forzar derrota | `E` | Activa la derrota y bloquea el movimiento. Es una entrada de depuración. |

El puntero de apuntado es una pequeña cruz amarilla. El código limita la distancia de apuntado y de los disparos a 18 unidades por defecto. El clic solo dispara cuando el cursor se encuentra dentro del viewport del juego.

## Controles táctiles

`MobileControlsHUD` construye botones en pantalla para movimiento izquierda/derecha, selección de armas, recarga y una zona de apuntado/disparo con arrastre. El joystick de apuntado mantiene el disparo mientras se presiona.

Actualmente el script solo crea esos controles al cargar la escena `Testing`. En un dispositivo móvil se muestran automáticamente; en el Editor, **F9** los alterna para previsualizar la interfaz. `Gameplay` todavía no crea este HUD táctil.
