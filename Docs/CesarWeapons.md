# Armas por personaje

Marco conserva pistola, metralleta y lanzacohetes. César usa exclusivamente escopeta (1), francotirador (2) y lanzagranadas (3). También se seleccionan desde los botones del HUD; R recarga. Se conserva la mecánica de disparo mantenido y recarga automática al vaciar el cargador.

Los tres espacios de mejoras y reserva de munición siguen compartidos entre personajes, igual que el perfil de progreso anterior. La tienda muestra los nombres, iconos y daño del personaje seleccionado. Los precios, paquetes de munición, monedas y progreso guardado conservan el funcionamiento anterior.

Balance inicial editable en `Assets/_Project/Resources/Cesar*Definition.asset`:

| Arma | Daño base | Cargador | Intervalo | Recarga |
| --- | --- | --- | --- | --- |
| Escopeta | 4 | 6 | 0.85 s | 2 s |
| Francotirador | 8 | 5 | 1.1 s | 2.2 s |
| Lanzagranadas | 10 | 4 | 1.2 s | 2 s |

La munición máxima del lanzagranadas de César es de 20 balas en total; cada recarga carga 4. El cartucho disparado por su escopeta mide 0.4 unidades de ancho (el doble del tamaño anterior), conservando el punto de salida y el radio de colisión.

El disparo de escopeta empieza en el tercer cuadro de la hoja existente (`Cesar_Escopeta_Fire_2`), con el arma horizontal. Se evalúa esa pose antes de emitir el cartucho desde el extremo del cañón, medido en (334, 228) sobre la celda de 384 × 512. Así se evita emitir desde la pose inicial con el arma baja. Se conserva el apuntado hacia el cursor y el reflejo al disparar a la izquierda.

La escopeta usa por ahora un proyectil, sin dispersión nueva. El francotirador tiene una estela dorada propia y atraviesa 2 enemigos distintos de base, 3 desde la segunda mejora y 4 desde la cuarta, dentro de sus 26 unidades de alcance. Se detiene ante obstáculos sólidos y muestra una pequeña salpicadura cartoon al causar daño. Ajustes, assets y prompts en [Art/SniperEffects.md](Art/SniperEffects.md).

## Assets

- Animaciones: `Assets/_Project/Animations/Players/Cesar/`.
- Hojas del personaje: `Assets/_Project/Art/Characters/Players/`.
- Iconos de armas: `Assets/_Project/Art/Weapons/`.
- Munición: `Assets/_Project/Art/UI/Ammo/`.
- Audio original: `Assets/_Project/Audio/SFX/Weapons/`.
- Audio preparado para disparo/recarga: `Assets/_Project/Audio/SFX/Weapons/Prepared/Cesar_*.wav`.
- Referencias de HUD y audio: `CesarWeaponHUDAssets.asset` y `CesarWeaponAudioLibrary.asset`, en Resources.

Los archivos de Screenshots se movieron mediante AssetDatabase para conservar sus GUID. Se conservaron los rectángulos de los recortes originales y se ajustaron escala/pivote para alinear los pies con el personaje existente. La secuencia de los cuadros se lee por su posición en la hoja, porque algunos nombres de los recortes no siguen el orden visual.

## Hoja generada

Se creó la recarga que faltaba del lanzagranadas con la herramienta integrada de generación de imágenes, usando la hoja de disparo como referencia. Archivo final: `Assets/_Project/Art/Characters/Players/Cesar_Lanzagranadas_Reload.png`. Ocho cuadros con transparencia, en cuatro columnas y dos filas.

Prompt utilizado:

> Use case: illustration-story. Asset type: Unity 2D game sprite sheet, 8 frames of Cesar RELOADING his grenade launcher. Reference image: existing Cesar grenade launcher firing sheet, use ONLY as identity, clothing, launcher and illustration style reference. Create a NEW reload sprite sheet on TRUE TRANSPARENT alpha background. Exact same brown spiky hair, black rectangular glasses, dark olive hoodie with white lining, black shirt, dark cargo pants, chunky gray/white sneakers, black drum grenade launcher. Side-facing right, full body, standing feet in place. Exactly 4 columns and 2 rows, 8 equally sized cells, same character height, same feet baseline relative to each cell, same camera and scale across all cells, ample transparent gutters. Reload sequence left to right top row then bottom row: 1 ready aiming right; 2 lower launcher and look at drum; 3 left hand opens/releases drum; 4 drum swung/open with hand loading grenade; 5 insert another grenade into drum; 6 close drum; 7 lock and raise launcher; 8 ready aiming right same as frame1. Keep feet and head stable, plausible anatomy. Crisp outlined shaded chibi/cartoon style matching reference closely. No gunfire, no muzzle flash, no smoke, no explosions, no projectiles flying, no words, no numbers, no watermark, no grid lines, no background.

## Verificación y mantenimiento

`Tools > Lindavista > Set up Cesar weapons` reconstruye los recortes, clips, referencias y estados de César desde las hojas existentes de cuatro columnas y dos filas. Conserva los identificadores de los recortes que mantienen su nombre. Normaliza la altura y alinea los pivotes con los pies de cada cuadro, usando el proveedor de sprites de Unity. Las definiciones de balance ya creadas se conservan. No modifica el controlador de Marco.

`Tools > Lindavista > Verify Cesar weapon assets` verifica cuadros, exclusividad de los assets y referencias. En Play Mode, `CesarWeaponChecks.BeginRuntime()` comprueba disparo, consumo de munición, cadencia, recarga completa, sonidos y cancelación al cambiar de arma con personajes temporales y un archivo de progreso aislado.

Verificación realizada: 46 comprobaciones de assets, 46 de integración en Play Mode, 16 comprobaciones existentes de combate y 30 de guardado. Revisión visual de César equipado, su recarga de lanzagranadas y su tienda. Capturas en `Captures/cesar-*.png`. No se realizaron compras en el perfil del jugador durante las pruebas.

### Reparación después de sustituir las hojas

Los clips conservaban referencias a sub-sprites eliminados al reemplazar las imágenes. Además, la recarga del francotirador tenía solo seis recortes, los de escopeta no cubrían el ancho completo y las seis hojas tenían escala/pivotes incompatibles con el personaje.

Se reconstruyeron las seis secuencias de disparo/recarga con ocho cuadros y las tres poses equipadas a partir de los PNG actuales, sin modificar las imágenes. El generador ahora corrige los recortes de las seis hojas antes de enlazar los clips, de modo que también permite reparar futuras sustituciones que conserven esta distribución.

Verificación de esta reparación: 196 comprobaciones de assets y 94 en Play Mode, incluyendo la reproducción de los 48 cuadros mediante el Animator real, disparo, recarga y cambio de arma. Las pruebas usan un perfil temporal. Capturas de las seis acciones en el escenario: `Captures/cesar-fixed-*.png`.

### Audio de recarga de la escopeta

`Escopeta_Recarga_y_Disparo.mp3` contiene primero el mecanismo de recarga y después el disparo. Los WAV preparados conservan esa separación: recarga de 0.00 a 0.36 s del original y disparo a partir de 0.60 s.

La recarga se reproducía con pitch 0.18 para llenar los dos segundos de la animación, lo que deformaba y apagaba el sonido mecánico. La opción `naturalReloads[0]` en `CesarWeaponAudioLibrary` ahora mantiene pitch 1 y programa esos 0.36 s hacia el final de la recarga, coincidiendo con el movimiento de armado. Termina 0.05 s antes de completarse la recarga. Cambiar de arma, interrumpir la recarga o desactivar al jugador cancela también el sonido pendiente. Las demás armas conservan su ajuste de audio.

Verificación: 101 comprobaciones en Play Mode con un perfil temporal, incluyendo tono original, reproducción efectiva en el momento previsto y cancelación del sonido programado al cambiar de arma.
