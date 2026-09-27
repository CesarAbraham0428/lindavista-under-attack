# LINDAVISTA: UNDER ATTACK

**LINDAVISTA — UNDER ATTACK** es un prototipo de acción 2D de desplazamiento lateral ambientado en las calles de Linda Vista. La propuesta jugable permite elegir a Marco o César, recorrer un escenario urbano y usar pistola, subfusil o lanzacohetes frente a amenazas. El proyecto también prepara variantes visuales del mismo nivel para distintos horarios.

Esta documentación describe lo que está implementado hoy en las escenas y scripts. Distingue los sistemas jugables de los elementos que por ahora son decoración o contenido de prueba.

## Recorrido del juego

1. `MainMenu` presenta el título, permite abrir la selección de personaje y contiene paneles de tienda y ajustes.
2. `CharacterSelection` permite escoger personaje y una de cuatro variantes de nivel.
3. `Gameplay` carga la calle, activa al personaje seleccionado y aplica la variante ambiental.
4. `Testing` sirve como escena de laboratorio: contiene a Marco y César, cuatro enemigos de prueba y las herramientas de HUD/controles disponibles para experimentar.

## Documentación

- [Escenas y flujo](Docs/escenas-y-flujo.md): recorrido entre menús, `Gameplay` y `Testing`.
- [Controles](Docs/controles.md): teclado, mouse y controles táctiles que existen actualmente.
- [Combate y vida](Docs/combate-y-vida.md): salud, daño, armas, proyectiles y comportamiento enemigo.
- [Niveles y entorno](Docs/niveles-y-entorno.md): horarios, cámara, tramos, muros, baches y elementos estáticos.
- [Animaciones](Docs/animaciones.md): estados de Marco, César y los enemigos; clips disponibles y conexiones con el código.
- [Estructura y estado](Docs/estructura-y-estado.md): carpetas del proyecto, versiones y límites conocidos.

## Estado resumido

- `Gameplay` abre con una vista de la oleada en el tramo 5 y desplaza la cámara hacia la entrada; los enemigos avanzan paulatinamente hacia el jugador. El nivel elegido define los tipos y cantidades mediante prefabs reutilizables.
- `Testing` contiene las cuatro variantes de enemigo y permite probar daño al jugador.
- El HUD de vida se construye durante Play Mode en `Gameplay` y `Testing`. El daño de prueba se activa con **Q**.
- Baches, grafitis, muros agrietados y marcas de impactos forman parte del arte colocado en la escena. No se generan al disparar ni se mueven por el nivel.
- La tienda todavía muestra “PRÓXIMAMENTE”.

## Convención de prueba

Para una mecánica nueva, primero se trabaja en `Testing`; cuando el resultado está validado, se integra en `Gameplay`. La diferencia entre ambas escenas y sus contenidos se detalla en [Escenas y flujo](Docs/escenas-y-flujo.md).
