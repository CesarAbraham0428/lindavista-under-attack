# Menús y selección

## Cambios documentados

En la edición anterior de esta escena, Marco y César se mostraron de frente con los retratos `MarcoFront.png` y `CesarFront.png` de `Assets/_Project/Art/UI/Polished`; se seleccionó la rebanada principal de cada archivo para evitar el borde transparente del atlas. Las tarjetas de personaje se estrecharon para abrir espacio a las tarjetas de nivel.

Las cuatro portadas (`portada_1_pm.png`, `portada_6_pm.png`, `portada_9_pm.png` y `portada_3_am.png`) se ampliaron de 244 × 366 a 302 × 452 unidades de interfaz, alrededor de un 24 % más en cada dimensión. También se desactivó la compresión de textura en los perfiles predeterminado, Standalone, Android y WebGL, manteniendo intacta la resolución de los archivos fuente.

El botón de tienda del menú principal abre `PistolUpgradeShop`, que es la tienda funcional compartida con la pantalla de resultado. El panel visual de tienda que crea el constructor no es el contenido interactivo que ve el jugador.

## Estado de los archivos actuales

Al documentar estos cambios, el estado del repositorio ya no coincide con la escena que se mostró tras editarlos: no está la carpeta `Assets/_Project/Art/UI/Polished`, `MenuSceneBuilder` todavía usa `Marco_Walk_8f.png` y `Cesar_Walk_8f.png`, conserva las portadas de 244 × 366, y los metadatos de las portadas mantienen la compresión. Por eso, el rediseño quedó documentado como cambio realizado en la sesión anterior, pero no está presente en el código ni se puede reconstruir desde los archivos actuales. Para volver a aplicarlo, hay que restaurar primero los sprites frontales a `Polished` y después actualizar el constructor y los metadatos.

## Reconstruir la escena

La escena se guarda en `Assets/_Project/Scenes/CharacterSelection.unity`. Su constructor es `MenuSceneBuilder.BuildCharacterSelection()` en `Assets/_Project/Editor/MenuSceneBuilder.cs`. El constructor de ambos menús está disponible como **Tools > Lindavista > Build menu screens**. La versión actual reconstruye las tarjetas anteriores, porque aún no incorpora los cambios enumerados arriba.

Al modificar retratos o portadas, revisa los import settings de los PNG y vuelve a generar la escena para mantener su jerarquía y referencias sincronizadas con el constructor.

## Pantalla de derrota

`GameFlowController` construye la pantalla al perder, tanto por agotar la vida como por la llegada de un enemigo a la entrada. Reutiliza el sprite principal de `Art/UI/Menu/cartel_vacio.png` mediante `Resources/ResultScreenAssets.asset`: un cartel inclinado para **¡PERDISTE!**, un botón grande **REINTENTAR** y dos botones secundarios **TIENDA** y **MENÚ**. La tipografía usa letras claras, contorno oscuro y sombra; el fondo de la partida se oscurece.

El mensaje es **¡Inténtalo otra vez!**. La derrota no muestra estadísticas ni saldo; la tienda sigue mostrando el monedero cuando se abre. `DefeatBackdrop` toma una captura de la partida y su HUD, aplica desenfoque una sola vez y la coloca debajo de una capa roja oscura. Los carteles y botones permanecen nítidos. La textura se libera al salir de la pantalla. El contenido se adapta al área segura y a las dimensiones de la pantalla. Los botones conservan sus acciones reales y admiten mouse, toque y navegación de teclado.
