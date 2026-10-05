# LINDAVISTA: UNDER ATTACK

**LINDAVISTA — UNDER ATTACK** es un prototipo de acción 2D de desplazamiento lateral ambientado en las calles de Linda Vista. Permite elegir a Marco o César, combatir con pistola y mejorarla recogiendo monedas enemigas. El proyecto también prepara variantes visuales del mismo nivel para distintos horarios.

Esta documentación describe lo que está implementado hoy en las escenas y scripts. Distingue los sistemas jugables de los elementos que por ahora son decoración o contenido de prueba.

## Recorrido del juego

1. `MainMenu` presenta el título, permite abrir la selección de personaje y contiene paneles de tienda y ajustes.
2. `CharacterSelection` permite escoger personaje y una de cuatro variantes de nivel.
3. `Gameplay` carga la calle, activa al personaje seleccionado y aplica la variante ambiental.
4. `Testing` sirve como escena de laboratorio: contiene a Marco y César, cuatro enemigos de prueba y las herramientas de HUD/controles disponibles para experimentar.

## Documentación

- [Menús y selección](Docs/menus-y-seleccion.md): retratos frontales, tamaño y calidad de las portadas, y reconstrucción de la escena.
- [Estado de los sistemas](Docs/estado-sistemas-juego.md): análisis de vida, tienda, armas, daño y puntos por balancear.
- [Escenas y flujo](Docs/escenas-y-flujo.md): recorrido entre menús, `Gameplay` y `Testing`.
- [Controles](Docs/controles.md): teclado, mouse y controles táctiles que existen actualmente.
- [Combate y vida](Docs/combate-y-vida.md): salud, daño, armas, proyectiles y comportamiento enemigo.
- [Progresión de la pistola](Docs/progresion-nivel-1.md): monedas, mejoras, guardado, cargador y victoria del nivel 1.
- [Niveles y entorno](Docs/niveles-y-entorno.md): horarios, cámara, tramos, muros, baches y elementos estáticos.
- [Animaciones](Docs/animaciones.md): estados de Marco, César y los enemigos; clips disponibles y conexiones con el código.
- [Estructura y estado](Docs/estructura-y-estado.md): carpetas del proyecto, versiones y límites conocidos.

## Estado resumido

- `Gameplay` abre con una vista de la oleada en el tramo 5 y desplaza la cámara hacia la entrada; los enemigos avanzan paulatinamente hacia el jugador. El nivel elegido define los tipos y cantidades mediante prefabs reutilizables.
- `Testing` contiene las cuatro variantes de enemigo y permite probar daño al jugador.
- Los HUD de vida, monedas y pistola se construyen durante Play Mode en `Gameplay` y `Testing`. El daño de prueba con **Q** está limitado a `Testing`. La pistola tiene dos mejoras; la tienda se abre desde el menú y el resultado de la partida.
- Baches, grafitis, muros agrietados y marcas de impactos forman parte del arte colocado en la escena. Los grupos ambientales `Daño_Nivel_2` a `Daño_Nivel_4` también son visuales: actualmente no infligen daño.
- La tienda permite mejorar la pistola de 2 a 3 y 4 de daño. El saldo, las mejoras y los niveles completados se guardan entre partidas.

## Convención de prueba

Para una mecánica nueva, primero se trabaja en `Testing`; cuando el resultado está validado, se integra en `Gameplay`. La diferencia entre ambas escenas y sus contenidos se detalla en [Escenas y flujo](Docs/escenas-y-flujo.md).
