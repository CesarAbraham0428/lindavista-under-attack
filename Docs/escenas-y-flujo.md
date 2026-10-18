# Escenas y flujo

Las tres escenas del proyecto están en `Assets/_Project/Scenes` y forman la lista de `EditorBuildSettings`. La antigua escena de laboratorio `Testing` se eliminó; también se retiró de la build y del código que la reconocía.

## Flujo principal

```text
MainMenu → CharacterSelection → Gameplay
    ↑              │
    └── volver ────┘
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
- **Inicio y oleada:** la cámara abre en el tramo 5 para mostrar solo tres enemigos separados por 3 unidades, luego hace un paneo hacia la entrada. Al empezar el paneo, `E5_Enemigos` libera esos enemigos uno por uno; después crea el resto de la oleada gradualmente desde el extremo derecho de la formación inicial. El intervalo mínimo es de 3,2 segundos y se espera a que haya 3 unidades libres alrededor del punto de aparición. Los enemigos avanzan hacia la posición inicial del jugador (X=-6,72 en la escena actual). El personaje queda bloqueado durante la presentación. Los conteos y prefabs se conservan para los cuatro niveles; la cantidad inicial, separación, intervalo y espacio libre se configuran en el Inspector del spawner.
- **Derrota:** la salud del personaje en cero o la llegada de un enemigo a la posición inicial activan la pantalla de derrota. Se bloquean las acciones y se puede reiniciar `Gameplay` o salir a `MainMenu`.
- **Victoria:** tras la última muerte y la liberación completa de la oleada se permite recoger monedas; **FINALIZAR NIVEL** guarda la finalización y desbloquea el siguiente nivel. Los resultados incluyen acceso a las mejoras de pistola.
- Consulta [Progresión](progresion-nivel-1.md) para el guardado y las recompensas.

## Convención de desarrollo

1. Probar el cambio en `Gameplay` con el personaje y el nivel afectados.
2. Cuando pueda verificarse automáticamente, añadir o actualizar una comprobación en **Tools > Lindavista**; varias se ejecutan en una escena vacía de Play Mode sin tocar el perfil real.
3. Comprobar el flujo completo desde `MainMenu`.
