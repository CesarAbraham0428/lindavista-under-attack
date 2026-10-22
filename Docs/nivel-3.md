# Nivel 3 — encuentros nocturnos

El nivel 3 conserva su ambiente de las 9 p. m., escenario, controles y armas de Marco y César. Reutiliza la escena `Gameplay` y no requiere una escena nueva ni cambios en el guardado real.

## Composición y aprendizaje

Se mantienen los **18 enemigos** existentes: seis básicos, cinco con pistola, cuatro con escudo y tres con metralleta. Orden predeterminado:

`Básico, Pistola, Básico, Pistola, Escudo, Básico, Metralleta, Pistola, Escudo, Básico, Pistola, Metralleta, Básico, Escudo, Pistola, Escudo, Básico, Metralleta`.

Los primeros tres enemigos de la presentación son conocidos. La metralleta aparece en las posiciones 7, 12 y 18; en la composición predeterminada hay al menos un enemigo normal entre encuentros de escudo/metralleta. El orden no cambia las cantidades. Si se personalizan los conteos en el Inspector, se omiten los encuentros sin unidades disponibles y se intercalan al final las unidades adicionales; esa composición personalizada puede no conservar las separaciones de la predeterminada.

## Ritmo y escudos

- Máximo de **siete enemigos vivos** de la oleada. Si se alcanza el límite, los refuerzos esperan sin perderse del total.
- Intervalo normal de **3,2 segundos**; después de liberar un escudo, **4,8 segundos**; después de una metralleta, **4,5 segundos**.
- Se conserva el requisito de espacio libre en el punto de aparición, por lo que los intervalos son mínimos y la espera puede ser mayor.
- Solo en el nivel 3, el escudo se abre con **cinco impactos frontales** consecutivos dentro de la ventana existente, durante **dos segundos**. El impacto que lo rompe sigue sin restar vida; los siguientes sí.
- Se mantienen salud, daño, alcance y ráfaga originales de los enemigos. No se aumenta el daño de la metralleta.
- El nivel 2 conserva sus cuatro impactos/2,5 segundos; el nivel 4 y `Testing` conservan los seis impactos/1,5 segundos del prefab.

La recompensa monetaria base sigue siendo **200 monedas recogibles** (30 + 50 + 60 + 60), además de los suministros existentes. Tras eliminar la oleada se permite recoger antes de finalizar. **FINALIZAR NIVEL** guarda la victoria y desbloquea el nivel 4. Una derrota detiene los enemigos y cancela los refuerzos pendientes.

## Ajustes y pruebas

Fuera de Play Mode, abre `Gameplay` y selecciona `Escenario/Escenario_Tramo_5/E5_Enemigos`. La sección **Level 3: shield and SMG encounters** de `LevelOneEnemySpawner` permite ajustar el límite de enemigos, los tres intervalos y la apertura del escudo. El orden de encuentros está definido en `LevelThreeEncounterOrder` en el mismo script.

`Tools > Lindavista > Verify level 3 configuration` revisa la configuración real mediante una escena de previsualización y un perfil temporal. No modifica la escena abierta ni el progreso del jugador. `Verify level 2 configuration` mantiene las comprobaciones del nivel anterior.

Para valorar la dificultad, juega el nivel con ambos personajes y observa si puedes reconocer las ráfagas, abrir los escudos y recoger suministros sin que los encuentros se vuelvan confusos. Las pruebas automáticas de lógica no sustituyen esa valoración ni una prueba en un teléfono físico.

## Validación del 17 de octubre de 2026

En una copia aislada de Unity y con perfiles temporales pasaron 69 comprobaciones de regresión de configuración del nivel 2, 76 de configuración del nivel 3, 590 comprobaciones deterministas en Play Mode y 138 de combate direccional. Se verificaron ambos personajes, las oleadas de los niveles 1–3, intervalos por tipo, límite y reanudación de refuerzos, escudo por nivel, munición, monedas, recogida antes de victoria, desbloqueo del nivel 4 y derrota en la entrada. Las comprobaciones de combate incluyen los cuatro tipos de enemigo, ambos sentidos de avance y la cancelación de ráfagas al rebasar al jugador.

La prueba determinista avanza manualmente la secuencia de liberación de enemigos y comprueba los intervalos configurados; no mide una partida completa en tiempo real ni modifica el progreso del jugador.
