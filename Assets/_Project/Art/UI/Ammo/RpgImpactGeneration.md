# Impacto cartoon del RPG

Herramienta: ImageGen integrada (ChatGPT Imagen), alfa transparente. Atlas final:
`Resources/RpgImpactAtlas.png`, 1254 × 1254, nueve celdas en una cuadrícula 3 × 3,
ordenadas de izquierda a derecha y de arriba abajo. Se conserva el alfa generado;
Unity recorta las celdas al cargar y no modifica la imagen fuente.

El cohete mide 1.96 unidades (40 % más que las anteriores 1.4). `CombatProjectile` barre la punta y
reproduce `RocketImpact` sobre el contacto. El flipbook dura 0.54 segundos,
incluye un destello pequeño, fuego naranja y humo gris cálido que desaparece.
La anchura de la celda en el mundo es 4.4 unidades (el doble que las anteriores 2.2); el dibujo tiene márgenes
transparentes y su tamaño visible es menor. Se limita a 24 impactos simultáneos.
Respeta la pausa y se limpia al terminar la partida.

El impacto directo conserva el daño y el escudo del enemigo. El RPG también
aplica la mitad del daño a enemigos a 0.95 unidades del contacto, una vez por
enemigo aunque tenga varios colliders. Las paredes con collider sólido bloquean
ese daño. La explosión no daña jugadores ni monedas. El suelo de las escenas
es arte sin collider: el disparo captura la línea de los pies del jugador y
detecta al cruzarla hacia abajo. La decoración sigue siendo visual; cualquier
collider sólido agregado al escenario sí intercepta el cohete.

## Prompt final

```text
Use case: stylized-concept
Asset type: production-ready 2D Unity game VFX animation sprite sheet.
Primary request: a SMALL, restrained cartoon RPG rocket impact, brief warm orange fire burst turning into rounded gray-brown smoke and fading. Match the clean outlined, cel-shaded cartoon/chibi action game style visible in the reference character and projectile images. This is an impact VFX only.
Composition: square 1024x1024 canvas with EXACTLY 9 equal square cells in a precise 3-column by 3-row grid (each cell one frame); chronological animation read left to right, top to bottom. Same fixed center/origin at the center of every cell; generous transparent padding around each frame; absolutely no art crossing cell boundaries. Consistent camera, scale and alignment. Frame 1 tiny yellow-orange flash. Frame 2 small orange fire bloom. Frame 3 compact peak with scalloped lobes and warm yellow center, never a dramatic large blast. Frame 4 orange glow surrounded by rounded gray smoke. Frame 5 softer gray-brown smoke puff with one fading ember. Frame 6 gray smoke puff, no fire. Frame 7 smaller separated soft-edged smoke lobes. Frame 8 very faint thinning smoke. Frame 9 just a couple faint wisps almost dissolved.
Style: hand-drawn cartoon, dark warm thin outlines on fire and main smoke, clean cel shading with two or three tones per shape, readable at small size, restrained effect. Muted warm gray smoke, orange and golden yellow fire. No photorealistic textures.
Constraints: genuinely transparent alpha background throughout including between cells, no backdrop, no checkerboard artwork, no floor, no rocket, no people, no debris, no text, no labels, no border/grid lines, no numbers, no watermark. All 9 frames visible, one distinct connected animation phase per cell.
```

