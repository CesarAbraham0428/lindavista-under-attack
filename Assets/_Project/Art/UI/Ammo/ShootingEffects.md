# Efectos de disparo

Atlas generado con la herramienta integrada ImageGen, con alfa transparente:
`Resources/ShotProjectileAtlas.png`. Los iconos de munición originales siguen
sirviendo al HUD. Este atlas es arte separado para los proyectiles del mundo.

- Pistola: bala corta de cobre, fogonazo, estela breve y chispas de impacto.
- SMG: bala más fina, velocidad de 32 unidades/s y fogonazo más pequeño por disparo.
- RPG: cohete de 1.96 unidades (40 % más grande que 1.4), velocidad de 14 unidades/s, fuego de propulsión
  y humo. Al chocar reproduce nueve fotogramas cartoon de fuego y humo durante
  0.54 s y con una anchura de celda de 4.4 unidades (el doble que antes), conserva
  el daño directo y aplica medio daño en un radio de 0.95 unidades.
  Cada enemigo recibe daño una sola vez y los colliders sólidos bloquean el daño cercano.
- Los enemigos con pistola/SMG usan los mismos efectos. Su daño sigue siendo
  instantáneo según su implementación original; el vuelo añadido es visual.

`ShotVisual` recorta las cuatro regiones del atlas y elimina márgenes transparentes.
El importador debe conservar **Read/Write**, alfa, sin mipmaps y sin compresión.
No hay que configurar prefabs: los efectos se crean desde los disparos existentes.
`ShotEffect` limita las partículas activas a 180 y elimina los efectos al terminar
su duración o la partida. Los fogonazos duran 65–110 ms; el humo del RPG, 400 ms.

`PlayerMuzzle` define el punto del cañón para Marco y César, las tres armas y
los ocho fotogramas de disparo. Convierte coordenadas del sprite a coordenadas
del mundo considerando pivote, píxeles por unidad, escala y reflejo. No usa el
centro del cuerpo ni desplaza la salida según el ángulo de apuntado.
`PlayerActions` orienta el personaje hacia el objetivo durante el disparo y
evalúa el primer fotograma de la animación antes de emitir el proyectil en
`LateUpdate`, evitando usar el sprite anterior durante la transición de 80 ms.
El pivote de los proyectiles queda en la cola; el arte y el fogonazo comienzan
en el cañón. El escape del RPG sigue esa cola al aumentar su tamaño.
Las velocidades de SMG y RPG se ajustan en `WeaponDefinition.At`. Daño directo, gasto
de munición, radio físico y cadencia se mantienen. El barrido del RPG cubre desde
la cola hasta la siguiente posición de la punta, incluidos enemigos a quemarropa;
su impacto queda sobre la superficie alcanzada. Como la calle
no tiene collider, también detecta la línea de los pies del jugador al disparar
hacia abajo. El alcance agotado no provoca una explosión.

`RocketImpact` carga `Resources/RpgImpactAtlas.png` con alfa transparente, sin
mipmaps ni compresión y sin requerir Read/Write. Mantiene el pivote de las nueve
celdas y desvanece los últimos fotogramas de humo. Se limita a 24 impactos activos.
Arte y prompt de ImageGen: [RpgImpactGeneration.md](RpgImpactGeneration.md).

## Verificación

El impacto RPG se verificó en Unity 6000.3.23f1 en un proyecto temporal aislado:
141 comprobaciones aprobadas de daño directo y de área, colliders duplicados,
paredes, suelo físico y calle pintada, contacto de la punta y a quemarropa,
orden entre enemigos y suelo, nueve cuadros con alfa, desvanecimiento, pausa
y duración, incluida una captura de cámara con los nueve cuadros. Se revisó
visualmente el render con transparencia. La prueba usa un perfil temporal y no
modifica escenas de juego; guarda la captura en `Library/RpgImpactChecks/preview.png`.
Para repetirla, guardar las escenas abiertas y ejecutar **Lindavista > Verify
RPG impact (isolated Play Mode)**. Restaura las escenas al finalizar.

Verificación previa de los efectos base mediante el MCP local el 10 de octubre de 2026:
compilación sin errores, prueba del atlas y tamaños aprobada, y 36 disparos
reales en Play Mode con ambos personajes, las tres armas, izquierda/derecha y
cuatro diagonales. La prueba usa un perfil temporal y comprueba el punto real
de salida, el sprite de disparo, orientación, escala no uniforme, munición y
cadencia. Revisada también la salida visual de las seis combinaciones y un
acercamiento del RPG mediante capturas de cámara.

Después de importar, ejecutar **Lindavista > Verify shooting visuals**. Comprueba
la carga del atlas, los cuatro sprites distintos, tamaños, dirección y estelas.
En Play Mode, ejecutar **Lindavista > Verify player muzzle origins (Play Mode)**
desde MainMenu sin otros proyectiles activos para repetir los 36 disparos.
En Gameplay, probar César y Marco con las tres armas, apuntando a izquierda,
derecha y diagonales, impactos contra enemigos/paredes, ráfagas y fin de partida.

## Prompt utilizado (herramienta integrada ImageGen)

Use case: stylized-concept. Asset type: Unity 2D projectile sprite atlas. Create one square transparent PNG atlas in a strict 2 by 2 equal-cell grid, no lines, no labels. All four isolated sprites centered in their respective cells, generous transparent margins, never crossing cell borders. Top left: copper pistol bullet in flight, only the rounded copper projectile with no cartridge casing, horizontal pointing RIGHT. Top right: longer slim copper SMG bullet in flight, no casing, pointing RIGHT. Bottom left: olive-green RPG rocket with gold pointed warhead and dark rear fins, horizontal pointing RIGHT, no fire or smoke attached. Bottom right: compact irregular warm white and orange muzzle flash burst, horizontal pointing RIGHT, origin at center-left of its cell. Crisp hand-painted 2D game art with dark outlines and metallic highlights, readable at small scale, warm brass and olive colors matching a cartoon action game. Orthographic side view, no perspective, no text, no background, no shadows beyond objects. Actual transparent alpha background.
