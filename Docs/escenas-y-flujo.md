# Escenas y flujo

Las cuatro escenas del proyecto están en `Assets/_Project/Scenes`. Unity las incluye actualmente en `EditorBuildSettings`, incluida la escena de pruebas.

## Flujo principal

```text
MainMenu → CharacterSelection → Gameplay
    ↑              │
    └── volver ────┘

Testing es una escena independiente para pruebas.
```

### `MainMenu`

- El botón **Jugar** abre `CharacterSelection`.
- **Tienda** abre un panel cuyo mensaje actual es “PRÓXIMAMENTE”.
- **Ajustes** muestra el control de volumen general. El valor se aplica a `AudioListener.volume` y se guarda con PlayerPrefs bajo `Lindavista.MasterVolume`.
- Los botones de regreso cierran los paneles; desde la selección se vuelve a `MainMenu`.

### `CharacterSelection`

- Presenta las tarjetas de Marco y César y cuatro tarjetas de nivel.
- La configuración serializada `levelUnlocked` decide qué tarjetas se pueden seleccionar y cuándo se muestra el candado.
- Al iniciar, guarda los índices elegidos en `Lindavista.SelectedCharacter` y `Lindavista.SelectedLevel`, y carga `Gameplay`.
- El código inicia la selección en el índice 0 para personaje y nivel. La escena asigna los botones y tarjetas a esos índices.

### `Gameplay`

- Es la escena principal del juego: tiene la cámara con seguimiento, el escenario urbano y los objetos raíz `Player_Marco` y `Player_Cesar`.
- Al cargarla, `SelectedCharacterBootstrap` activa Marco o César según `Lindavista.SelectedCharacter`. Si esa preferencia no existe, se conservan los estados guardados en la escena; el estado por defecto es Marco activo y César inactivo.
- `SelectedLevelEnvironment` lee `Lindavista.SelectedLevel` al iniciar y configura los elementos visuales de esa variante.
- El HUD de vida se crea en tiempo de ejecución para el jugador activo.
- **Inicio y oleada:** la cámara abre en el tramo 5 para mostrar la formación de enemigos del nivel elegido, luego hace un paneo hacia la entrada. Al empezar el paneo, `E5_Enemigos` activa un enemigo cada 1,65 segundos para que avance hacia la posición inicial del jugador (X=-6,72 en la escena actual). El personaje queda bloqueado durante la presentación. Los conteos y prefabs se configuran en el mismo spawner para los cuatro niveles.
- **Derrota:** la salud del personaje en cero o la llegada de un enemigo a la posición inicial activan la pantalla de derrota. Se bloquean las acciones y se puede reiniciar `Gameplay` o salir a `MainMenu`.
- `Testing` conserva las cuatro variantes de enemigo para probar combate y animaciones. La secuencia de inicio en `Gameplay` requiere una comprobación visual en Play Mode.

### `Testing`

- Escena de laboratorio para probar cambios antes de llevarlos a `Gameplay`.
- Tiene a Marco y César activos a la vez, cuatro enemigos (`Enemy1` a `Enemy4`) y el mismo tipo de escenario largo.
- El HUD de vida muestra una fila por cada jugador activo. **Q** quita vida para probar el daño; no simula un impacto real de un enemigo.
- El HUD de controles táctiles también está configurado para esta escena: aparece en plataformas móviles y se puede previsualizar en el Editor con **F9**.

## Convención de desarrollo

1. Probar el comportamiento aislado en `Testing`.
2. Ajustar código y contenido hasta validar el resultado.
3. Integrar la versión aprobada en `Gameplay` y comprobar el flujo con el personaje y nivel seleccionados.
4. Mantener `Testing` como laboratorio para iteraciones futuras.
