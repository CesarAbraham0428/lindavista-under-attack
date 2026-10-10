# Efectos de disparo

Atlas generado con la herramienta integrada ImageGen, con alfa transparente:
`Resources/ShotProjectileAtlas.png`. Los iconos de munición originales siguen
sirviendo al HUD. Este atlas es arte separado para los proyectiles del mundo.

- Pistola: bala corta de cobre, fogonazo, estela breve y chispas de impacto.
- SMG: bala más fina, velocidad de 32 unidades/s y fogonazo más pequeño por disparo.
- RPG: cohete de 1.1 unidades (antes 0.65), velocidad de 14 unidades/s, fuego de propulsión,
  humo y explosión visual al impactar. La explosión conserva el daño directo
  existente; no añade daño de área.
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
Las velocidades de SMG y RPG se ajustan en `WeaponDefinition.At`. Daño, gasto
de munición, radio físico y cadencia se mantienen.

## Verificación

Verificado en Unity 6000.3.23f1 mediante el MCP local el 10 de octubre de 2026:
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
