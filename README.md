# LINDAVISTA: UNDER ATTACK

**LINDAVISTA — UNDER ATTACK** es un prototipo de acción 2D de desplazamiento lateral ambientado en las calles de Linda Vista. Permite elegir a Marco o César, combatir con pistola y mejorarla recogiendo monedas enemigas. El proyecto también prepara variantes visuales del mismo nivel para distintos horarios.

Esta documentación describe lo que está implementado hoy en las escenas y scripts. Distingue los sistemas jugables de los elementos que por ahora son decoración o contenido de prueba.

## Recorrido del juego

1. `MainMenu` presenta el título, permite abrir la selección de personaje y contiene paneles de tienda y ajustes.
2. `CharacterSelection` permite escoger personaje y una de cuatro variantes de nivel.
3. `Gameplay` carga la calle, activa al personaje seleccionado y aplica la variante ambiental.

La antigua escena de laboratorio `Testing` se eliminó; las pruebas se hacen en `Gameplay` y con las comprobaciones del menú **Tools > Lindavista**.

## Documentación

- [Menús y selección](Docs/menus-y-seleccion.md): retratos frontales, tamaño y calidad de las portadas, y reconstrucción de la escena.
- [Estado de los sistemas](Docs/estado-sistemas-juego.md): análisis de vida, tienda, armas, daño y puntos por balancear.
- [Escenas y flujo](Docs/escenas-y-flujo.md): recorrido entre menús y `Gameplay`.
- [Controles](Docs/controles.md): teclado, mouse y controles táctiles que existen actualmente.
- [Adaptación a Android](Docs/adaptacion-android.md): configuración Android, controles, rendimiento y resultados de la validación del 17 de octubre; sin generar APK por indicación del usuario.
- [Móvil y rendimiento](Docs/movil-y-rendimiento.md): detalle de guardado agrupado, optimizaciones y herramientas de comprobación.
- [Revisión de rendimiento del 17 de octubre](Docs/rendimiento-2026-10-17.md): mediciones antes/después en el Editor, cambios, riesgos y verificaciones pendientes en dispositivo.
- [Rendimiento en Windows del 18 de octubre](Docs/rendimiento-escritorio-2026-10-18.md): escala de render, cadencia, URP y texturas en PC, cierre de pendientes de Android y build de Windows.
- [Combate y vida](Docs/combate-y-vida.md): salud, daño, armas, proyectiles y comportamiento enemigo.
- [Progresión de la pistola](Docs/progresion-nivel-1.md): monedas, mejoras, guardado, cargador y victoria del nivel 1.
- [Niveles y entorno](Docs/niveles-y-entorno.md): horarios, cámara, tramos, muros, baches y elementos estáticos.
- [Animaciones](Docs/animaciones.md): estados de Marco, César y los enemigos; clips disponibles y conexiones con el código.
- [Estructura y estado](Docs/estructura-y-estado.md): carpetas del proyecto, versiones y límites conocidos.

## Estado resumido

- `Gameplay` abre con una vista de la oleada en el tramo 5 y desplaza la cámara hacia la entrada; los enemigos avanzan paulatinamente hacia el jugador. El nivel elegido define los tipos y cantidades mediante prefabs reutilizables.
- Los HUD de vida, monedas y pistola se construyen durante Play Mode en `Gameplay`. La pistola tiene dos mejoras; la tienda se abre desde el menú y el resultado de la partida.
- Baches, grafitis, muros agrietados y marcas de impactos forman parte del arte colocado en la escena. Los grupos ambientales `Daño_Nivel_2` a `Daño_Nivel_4` también son visuales: actualmente no infligen daño.
- La tienda permite mejorar la pistola de 2 a 3 y 4 de daño. El saldo, las mejoras y los niveles completados se guardan entre partidas.

## Convención de prueba

Una mecánica nueva se prueba en `Gameplay`, con el personaje y el nivel afectados, y se acompaña de una comprobación en **Tools > Lindavista** cuando su comportamiento puede verificarse automáticamente. El recorrido de escenas se detalla en [Escenas y flujo](Docs/escenas-y-flujo.md).
