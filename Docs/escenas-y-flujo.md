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
- **Tienda** abre las mejoras de daño de la pistola usando el saldo guardado.
- **Ajustes** muestra el control de volumen general. El valor se aplica a `AudioListener.volume` y se guarda con PlayerPrefs bajo `Lindavista.MasterVolume`.
- Los botones de regreso cierran los paneles; desde la selección se vuelve a `MainMenu`.

### `CharacterSelection`

- En la edición anterior se presentó a Marco y César de frente y se ampliaron las cuatro portadas. La escena y su constructor en el repositorio actual todavía conservan los sprites de caminar y el tamaño anterior; ver [Menús y selección](menus-y-seleccion.md).
- El perfil de `ProgressionService` decide los niveles seleccionables y los candados. Un perfil nuevo tiene únicamente el nivel 1 desbloqueado.
- Al iniciar, guarda los índices elegidos en `Lindavista.SelectedCharacter` y `Lindavista.SelectedLevel`, y carga `Gameplay`.
- El código inicia la selección en el índice 0 para personaje y nivel. La escena asigna los botones y tarjetas a esos índices.

### `Gameplay`

- Es la escena principal del juego: tiene la cámara con seguimiento, el escenario urbano y los objetos raíz `Player_Marco` y `Player_Cesar`.
- Al cargarla, `SelectedCharacterBootstrap` activa Marco o César según `Lindavista.SelectedCharacter`; sin preferencia, activa a Marco. Una selección de nivel bloqueado se corrige al nivel 1.
- `SelectedLevelEnvironment` lee `Lindavista.SelectedLevel` al iniciar y configura los elementos visuales de esa variante.
- Los HUD de vida, saldo y pistola se crean en tiempo de ejecución para el jugador activo.
- **Inicio y oleada:** la cámara abre en el tramo 5 para mostrar la formación de enemigos del nivel elegido, luego hace un paneo hacia la entrada. Al empezar el paneo, `E5_Enemigos` activa un enemigo cada 1,65 segundos para que avance hacia la posición inicial del jugador (X=-6,72 en la escena actual). El personaje queda bloqueado durante la presentación. Los conteos y prefabs se configuran en el mismo spawner para los cuatro niveles.
- **Derrota:** la salud del personaje en cero o la llegada de un enemigo a la posición inicial activan la pantalla de derrota. Se bloquean las acciones y se puede reiniciar `Gameplay` o salir a `MainMenu`.
- **Victoria:** tras la última muerte y la liberación completa de la oleada se permite recoger monedas; **FINALIZAR NIVEL** guarda la finalización y desbloquea el siguiente nivel. Los resultados incluyen acceso a las mejoras de pistola.
- `Testing` conserva las cuatro variantes de enemigo para probar combate y animaciones. Consulta [Progresión](progresion-nivel-1.md) para el guardado y las recompensas.

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
