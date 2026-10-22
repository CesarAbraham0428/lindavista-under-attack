# Nivel 4 — jefe final

## Presentación aprobada

Pantalla de juego a las 03:00, franjas negras, `Boss_Base` grande a la derecha,
cartel de madera y diálogo a la izquierda. Se conserva el PNG original; el boceto
generado solo es referencia de composición, no se incorpora como fondo del juego.
Una oración completa por clic izquierdo, toque, espacio o Enter; cuatro avances.
Se oculta el HUD durante el diálogo y se consume la última pulsación antes de jugar.

## Encuentro

Los 24 enemigos configurados del nivel (8/6/5/5) salen gradualmente, con un máximo
de siete simultáneos. Se conserva la puerta como objetivo y la IA coordinada.
La eliminación del último enemigo inicia una espera de dos segundos y la entrada
del camión desde la derecha de la cámara. No se declara victoria entre ambas fases.

El camión usa las animaciones existentes y tiene 300 de vida, daño de 1 por impacto,
ráfagas de tres disparos, aviso de 0.9 segundos y recuperación de 2.8 segundos.
A mitad de vida dispara cinco veces y recupera algo más rápido. Apunta a la posición
registrada al iniciar el aviso: no persigue mágicamente al jugador durante la ráfaga.
Los proyectiles viajan, comprueban el segmento recorrido y se reciclan; no aplican
daño instantáneo al disparar. Se anulan al morir el jefe o terminar la partida.

La barra roja aparece abajo y centrada solo durante el encuentro. Muestra vida y
aviso de ráfaga/recuperación. El jefe muerto deja monedas y permite la misma fase
de recogida y salida que los otros niveles; completar esa fase guarda el nivel 4.

## Ajustes y prueba

- Prefab `Assets/_Project/Prefabs/BossTruck.prefab`: EnemyHealth (vida), BossTruck
  (cadencia, aviso, recuperación, velocidad y daño), EnemyLootDrop (recompensa).
- Spawner de Gameplay: Level 4 (máximo de enemigos, intervalo y espera del jefe).
- Herramientas: `Tools > Lindavista > Prepare level 4 assets` recupera referencias
  faltantes sin sobrescribir assets configurados ni modificar `Boss_Base.png.meta`.
- Entrar desde selección de nivel, habiendo desbloqueado el cuarto nivel. No se
  modifica el progreso real para desbloquearlo automáticamente.

## Direction contract

THESIS: presentación cinematográfica dentro del mundo gráfico existente, sin
reemplazar el arte ni convertirla en un menú genérico.

OWN-WORLD: tinta oscura, crema, dorado, madera, retrato original negro/dorado.

STORY: el jefe amenaza en cuatro oraciones; después manda a sus hombres y por último
entra en su vehículo, con una ventana clara para esquivar y contraatacar.

FIRST VIEWPORT: bandas negras arriba/abajo, retrato derecho aproximadamente 45%,
diálogo inferior izquierdo aproximadamente 56%, contador 1/4 y continuación inferior.

FORM: composición fijada por el usuario y aprobada en boceto; sin sorteo de conceptos.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

Se hereda el sistema visual existente; no se reemplaza su identidad global.
