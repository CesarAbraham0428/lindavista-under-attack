# Disparo penetrante del francotirador de César

Los PNG se generaron con la herramienta integrada de generación de imágenes, con transparencia alfa. Las referencias adjuntas se usaron para la estela del primer ejemplo y el estilo cartoon del personaje y su rifle.

- `Assets/_Project/Art/Effects/Resources/SniperTracer.png`: estela horizontal hacia la derecha, núcleo blanco y contorno dorado. `SniperShotVisual` recorta los márgenes transparentes al crear el sprite y coloca el pivote en la punta derecha.
- `Assets/_Project/Art/Effects/Resources/SniperBloodImpact.png`: salpicadura cartoon roja, sin anatomía, orientada con el disparo. Solo se muestra si el enemigo pierde vida; un escudo que bloquea produce las chispas existentes.

La estela crece entre la boca del cañón y la punta durante el avance, con un máximo visual de 8 unidades y un grosor de 0.14. Nunca aparece delante de la bala ni detrás de la posición de salida. Al terminar el disparo permanece inmóvil y desaparece en 0.1 segundos. La sangre dura 0.18 segundos. Los tiempos respetan la pausa.

En `CesarFrancotiradorDefinition.asset`, `range` conserva 26 unidades, `basePiercingTargets` vale 2, `maxPiercingTargets` vale 4 y `upgradesPerExtraTarget` vale 2. Los niveles de mejora 0–1 alcanzan 2 enemigos, 2–3 alcanzan 3 y 4–5 alcanzan 4. El límite cuenta enemigos distintos, incluyendo los que bloquean con escudo. El disparo conserva daño completo y se detiene al último objetivo permitido, al llegar a su alcance o al encontrar un collider sólido. Los fondos pintados sin collider no bloquean el disparo. El SMG de Marco conserva su comportamiento.

La tienda muestra el número de enemigos que atraviesa el francotirador. La penetración utiliza las mejoras de arma existentes y no cambia el formato del guardado.

La hoja actual de César empieza con tres poses de levantar el rifle. El disparo evalúa el cuarto cuadro (normalizado 0.38), que ya apunta y muestra el fogonazo. `PlayerMuzzle` tiene las coordenadas del cañón de esta hoja, en lugar de reutilizar las de la antigua metralleta. Los sprites y clips originales se conservan.

Verificación: `Lindavista > Verify sniper piercing (isolated Play Mode)`. Utiliza una escena y perfil temporales, restaura las escenas abiertas y exporta una vista previa a `Library/SniperShotChecks/preview.png`.

Resultado: 73 comprobaciones del francotirador aprobadas, incluido el disparo desde `PlayerActions` con el Animator real, la boca del cañón, munición y cadencia. También pasaron la verificación existente de efectos y 143 comprobaciones de regresión del RPG. Vista previa de la estela en vuelo: `Captures/sniper-piercing-preview.png`.

## Prompts utilizados

### Estela

Use case: stylized-concept. Asset type: production transparent 2D Unity game projectile sprite, SINGLE asset.
Primary request: draw one long, extremely thin sniper bullet tracer traveling horizontally LEFT TO RIGHT, matching the smooth hand-drawn cartoon/chibi game art of the attached boy holding the tan sniper rifle. Reference image 1 is only the pale yellow piercing shot effect reference; reference images 2 and 3 supply the existing cartoon game's visual style. Do not copy characters or scenery.
Subject: a narrow straight warm ivory-white light streak, honey-yellow crisp edge and restrained orange halo, fine taper to a transparent point at LEFT tail, a small white-hot pointed bullet-shaped luminous tip at RIGHT. Long clean silhouette around 18:1 aspect ratio. Keep it straight and elegant, NOT a thick laser tube. Slight hand-painted highlights. No detached sparks.
Composition: single isolated horizontal sprite across the middle of a wide transparent canvas, about 85% canvas width and 5% height, generous fully transparent margins. Everything points right. True alpha transparency including soft glow, no black matte, no checkerboard painted in. No muzzle flash, no weapon, no shell casing, no background, no letters, no labels, no frame, no multiple versions. This will be tightly sliced and scaled as a finite moving tracer in game.

### Impacto

Use case: stylized-concept. Asset type: single transparent 2D Unity sprite for a brief cartoon blood hit effect.
Primary request: create one small stylized bright red impact splatter that appears when a sniper bullet pierces a cartoon enemy. Match the clean smooth hand-drawn cartoon/chibi style of the original attached boy holding the tan sniper, NOT pixel art. A single compact asymmetrical scarlet burst, 6 to 8 tapered streaks and a few separate rounded red droplets, mostly spraying LEFT TO RIGHT along the shot with some short diagonals. Flat cel-shaded red with restrained dark crimson underside and a few vermilion highlights. Crisp readable at very small size, modest impact splash, no anatomy or body parts. Width about 1.5 times height. Center the impact origin near middle of the canvas. Leave generous transparent margins, every droplet fully inside canvas.
True transparent alpha background, no black matte, no painted checkerboard. One isolated effect only. No white or yellow sparks, no muzzle flash, no gun, no people, no monster, no ground puddle, no scene, no text, no labels, no grid, no frame.

