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
| Otras armas | `2` / `3` | Bloqueadas en la entrega de progresión de pistola. |
| Recargar | `R` | Rellena el cargador de 12 balas tras 1,20 s; reserva ilimitada. Al disparar vacío se inicia recarga automática. |
| Probar daño | `Q` | Solo en `Testing`: intenta quitar 1 punto de vida. |
| Forzar derrota | `E` | Solo en `Testing`: intenta quitar la vida restante, respetando la protección temporal. |

El puntero de apuntado es una pequeña cruz amarilla. El código limita la distancia de apuntado y de los disparos a 18 unidades por defecto. El clic solo dispara cuando el cursor se encuentra dentro del viewport del juego.

## Controles táctiles

`MobileControlsHUD` construye botones en pantalla para movimiento izquierda/derecha, selección de armas, recarga y una zona de apuntado/disparo con arrastre. El joystick de apuntado mantiene el disparo mientras se presiona.

Los controles se crean en `Testing` y `Gameplay`. En un dispositivo móvil se muestran automáticamente; en el Editor, **F9** los alterna para previsualizar la interfaz. Solo aparece la pistola en la selección de armas.
