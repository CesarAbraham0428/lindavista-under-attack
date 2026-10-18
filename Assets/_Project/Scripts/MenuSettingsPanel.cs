using UnityEngine;
using UnityEngine.UI;

/// <summary>Sound settings and control reference, serialized in the menu scene.</summary>
public sealed class MenuSettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider masterVolume;
    [SerializeField] private Slider musicVolume;
    [SerializeField] private Text masterValue;
    [SerializeField] private Text musicValue;
    [SerializeField] private Button[] trackButtons;
    [SerializeField] private Text[] trackNumbers;
    [SerializeField] private Image[] trackAccents;
    [SerializeField] private Button soundTab;
    [SerializeField] private Button controlsTab;
    [SerializeField] private GameObject soundContent;
    [SerializeField] private GameObject controlsContent;
    private MenuMusicPlayer player;
    private bool bound;
    private bool mobileLayout;
    private Button displayTab;
    private GameObject displayContent;
    private Button fluidMode;
    private Button batteryMode;
    public Slider MasterVolume => masterVolume;
    public Button BackButton { get; private set; }

    private void Awake()
    {
        if (masterVolume == null || musicVolume == null) return;
        player = MenuMusicPlayer.Instance;
        masterVolume.onValueChanged.AddListener(UpdateMasterValue);
        musicVolume.onValueChanged.AddListener(SetMusicVolume);
        if (soundTab != null) soundTab.onClick.AddListener(() => ShowSection(false));
        if (controlsTab != null) controlsTab.onClick.AddListener(() => ShowSection(true));
        for (int i = 0; i < trackButtons.Length; i++)
        {
            int index = i;
            trackButtons[i].onClick.AddListener(() => player.SelectTrack(index));
        }
        bound = true;
    }

    private void OnEnable()
    {
        ConfigurarControlesMoviles(Application.isMobilePlatform);
        ShowSection(false);
        if (!bound || player == null) return;
        player.Changed += Refresh;
        Refresh();
    }

    public void ConfigurarControlesMoviles(bool movil)
    {
        mobileLayout = movil;
        ConfigurarPantalla(movil && (Application.platform == RuntimePlatform.Android || Application.isEditor));
        AjustarSonido(movil);
        if (controlsContent == null) return;
        var teclado = controlsContent.transform.Find("Keyboard and Mouse");
        var tactil = controlsContent.transform.Find("Touch Controls");
        if (teclado != null) teclado.gameObject.SetActive(!movil);
        if (tactil == null) return;
        ProgressionUI.Percent((RectTransform)tactil, movil ? 0f : .595f, 0f, 1f, 1f);
        ConfigurarAyudaTactil(tactil, movil);
        if (!movil) return;
        foreach (var texto in tactil.GetComponentsInChildren<Text>(true))
        {
            int tamano = texto.gameObject.name == "Hint" ? 30 : 35;
            texto.fontSize = texto.resizeTextMaxSize = tamano;
            texto.resizeTextMinSize = 24;
        }
    }

    private static void ConfigurarAyudaTactil(Transform touch, bool movil)
    {
        var help = touch.Find("Mobile Actions");
        if (help == null && movil)
        {
            help = ProgressionUI.Image(touch, "Mobile Actions", Color.clear).transform;
            ProgressionUI.Percent((RectTransform)help, 0f, 0f, 1f, .86f);
            string[] captions = { "MOVERSE · ‹  ›", "APUNTAR Y DISPARAR", "RECARGAR · REC", "CAMBIAR ARMA", "RECUPERAR VIDA · +1", "PAUSA / ATRÁS" };
            string[] hints = { "Mantén las flechas de la izquierda.", "Mantén y arrastra en el área derecha.", "Toca REC para recargar el arma.", "Toca el icono de un arma que tengas.", "Toca USAR +1. Necesitas pastillas y estar herido.", "Pausa la partida o vuelve al panel anterior." };
            for (int i = 0; i < captions.Length; i++)
            {
                float left = i < 3 ? .025f : .515f;
                float bottom = .68f - (i % 3) * .33f;
                var row = ProgressionUI.Image(help, "Action " + i, new Color(.11f, .15f, .13f));
                row.sprite = Resources.Load<Sprite>("UI/SettingsFrame");
                row.type = Image.Type.Sliced;
                ProgressionUI.Percent(row.rectTransform, left, bottom, left + .46f, bottom + .30f);
                var title = ProgressionUI.Text(row.transform, "Action", captions[i], 35, TextAnchor.MiddleLeft);
                CartoonUI.Typography(title);
                title.resizeTextMinSize = 28;
                title.color = ProgressionUI.Gold;
                ProgressionUI.Percent(title.rectTransform, .045f, .53f, .955f, .96f);
                var hint = ProgressionUI.Text(row.transform, "Hint", hints[i], 30, TextAnchor.MiddleLeft);
                CartoonUI.Typography(hint);
                hint.resizeTextMinSize = 26;
                hint.color = new Color(.78f, .81f, .70f);
                ProgressionUI.Percent(hint.rectTransform, .045f, .05f, .955f, .54f);
            }
        }
        for (int i = 0; i < touch.childCount; i++)
        {
            var child = touch.GetChild(i);
            if (child.name == "Heading") continue;
            child.gameObject.SetActive(child == help ? movil : !movil);
        }
    }

    private void ConfigurarPantalla(bool movil)
    {
        if (soundTab == null || controlsTab == null) return;
        if (movil && displayTab == null)
        {
            var board = soundTab.transform.parent;
            displayTab = ProgressionUI.Button(board, "PANTALLA", () => ShowSection(2));
            displayTab.gameObject.name = "Display Tab";
            displayTab.image.sprite = soundTab.image.sprite;
            displayTab.image.type = soundTab.image.type;
            var caption = displayTab.GetComponentInChildren<Text>();
            CartoonUI.Typography(caption, true);
            caption.fontSize = caption.resizeTextMaxSize = 38;
            caption.resizeTextMinSize = 28;
            displayContent = ProgressionUI.Image(board, "Display Content", Color.clear).gameObject;
            ProgressionUI.Percent((RectTransform)displayContent.transform, .055f, .185f, .945f, .665f);
            var title = ProgressionUI.Text(displayContent.transform, "Performance Heading", "ELIGE CÓMO JUGAR", 42, TextAnchor.MiddleCenter);
            CartoonUI.Typography(title, true);
            ProgressionUI.Percent(title.rectTransform, 0, .76f, 1, 1);
            fluidMode = CrearModo("FLUIDO", "Hasta 60 FPS · prioriza la fluidez", false, .015f, .485f);
            batteryMode = CrearModo("AHORRO DE BATERÍA", "Hasta 30 FPS · prioriza la batería", true, .515f, .985f);
            var hint = ProgressionUI.Text(displayContent.transform, "Performance Hint", "Se aplica al instante y se guarda para la próxima partida.", 30, TextAnchor.MiddleCenter);
            CartoonUI.Typography(hint);
            ProgressionUI.Percent(hint.rectTransform, .02f, .02f, .98f, .20f);
            displayContent.SetActive(false);
        }
        if (displayTab != null) displayTab.gameObject.SetActive(movil);
        if (displayContent != null && !movil) displayContent.SetActive(false);
        ProgressionUI.Percent((RectTransform)soundTab.transform, .055f, movil ? .69f : .715f, movil ? .345f : .485f, .825f);
        ProgressionUI.Percent((RectTransform)controlsTab.transform, movil ? .365f : .515f, movil ? .69f : .715f, movil ? .635f : .945f, .825f);
        if (movil && displayTab != null)
            ProgressionUI.Percent((RectTransform)displayTab.transform, .655f, .69f, .945f, .825f);
        if (soundContent != null)
            ProgressionUI.Percent((RectTransform)soundContent.transform, .055f, .185f, .945f, movil ? .665f : .69f);
        if (controlsContent != null)
            ProgressionUI.Percent((RectTransform)controlsContent.transform, .055f, .185f, .945f, movil ? .665f : .69f);
        RefreshModes();
    }

    private Button CrearModo(string caption, string hint, bool ahorro, float left, float right)
    {
        var button = ProgressionUI.Button(displayContent.transform, caption, () =>
        {
            ConfiguracionMovil.EstablecerModoAhorro(ahorro);
            RefreshModes();
        });
        button.gameObject.name = ahorro ? "Battery Mode" : "Fluid Mode";
        CartoonUI.ButtonStyle(button);
        ProgressionUI.Percent((RectTransform)button.transform, left, .26f, right, .73f);
        var text = button.GetComponentInChildren<Text>();
        text.fontSize = text.resizeTextMaxSize = 40;
        text.resizeTextMinSize = 30;
        ProgressionUI.Percent(text.rectTransform, .04f, .43f, .96f, .94f);
        var description = ProgressionUI.Text(button.transform, "Description", hint, 28, TextAnchor.MiddleCenter);
        CartoonUI.Typography(description);
        description.resizeTextMinSize = 24;
        ProgressionUI.Percent(description.rectTransform, .04f, .06f, .96f, .42f);
        return button;
    }

    private void RefreshModes()
    {
        if (fluidMode == null || batteryMode == null) return;
        bool ahorro = ConfiguracionMovil.ModoAhorro;
        StyleTab(fluidMode, !ahorro);
        StyleTab(batteryMode, ahorro);
        fluidMode.interactable = ahorro;
        batteryMode.interactable = !ahorro;
    }

    private void AjustarSonido(bool movil)
    {
        if (soundContent == null) return;
        foreach (var slider in new[] { masterVolume, musicVolume })
        {
            if (slider == null) continue;
            var section = slider.transform.parent;
            var rect = (RectTransform)section;
            rect.anchorMin = new Vector2(rect.anchorMin.x, movil ? .50f : .635f);
            rect.anchorMax = new Vector2(rect.anchorMax.x, 1f);
            ProgressionUI.Percent((RectTransform)slider.transform, .055f, .08f, .945f, movil ? .65f : .43f);
            var caption = section.Find("Caption") as RectTransform;
            var value = section.Find("Value") as RectTransform;
            if (caption != null) ProgressionUI.Percent(caption, .045f, movil ? .68f : .67f, .78f, .95f);
            if (value != null) ProgressionUI.Percent(value, .78f, movil ? .68f : .67f, .955f, .95f);
            var hint = section.Find("Hint");
            if (hint != null) hint.gameObject.SetActive(!movil);
        }
        var heading = soundContent.transform.Find("Soundtrack Heading") as RectTransform;
        if (heading != null) ProgressionUI.Percent(heading, 0f, movil ? .375f : .495f, 1f, movil ? .485f : .605f);
        foreach (var button in trackButtons)
        {
            if (button == null) continue;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(rect.anchorMin.x, movil ? .025f : .13f);
            rect.anchorMax = new Vector2(rect.anchorMax.x, movil ? .36f : .48f);
        }
        var playback = soundContent.transform.Find("Playback Hint");
        if (playback != null) playback.gameObject.SetActive(!movil);
    }

    private void OnDisable()
    {
        if (player != null) player.Changed -= Refresh;
        if (Application.isPlaying) PlayerPrefs.Save();
    }

    private void UpdateMasterValue(float value) { masterValue.text = Mathf.RoundToInt(value * 100f) + "%"; }
    private void SetMusicVolume(float value) { player.SetMusicVolume(value); }

    public void ShowSection(bool controls)
    {
        ShowSection(controls ? 1 : 0);
    }

    private void ShowSection(int section)
    {
        if (soundContent != null) soundContent.SetActive(section == 0);
        if (controlsContent != null) controlsContent.SetActive(section == 1);
        if (displayContent != null) displayContent.SetActive(mobileLayout && section == 2);
        StyleTab(soundTab, section == 0);
        StyleTab(controlsTab, section == 1);
        StyleTab(displayTab, section == 2);
        RefreshModes();
    }

    private static void StyleTab(Button tab, bool selected)
    {
        if (tab == null) return;
        tab.image.color = selected ? new Color(.36f, .46f, .22f) : new Color(.14f, .18f, .16f);
        var caption = tab.GetComponentInChildren<Text>();
        if (caption != null) caption.color = selected ? ProgressionUI.Gold : CartoonUI.Cream;
    }

    private void Refresh()
    {
        masterVolume.SetValueWithoutNotify(AudioListener.volume);
        musicVolume.SetValueWithoutNotify(player.MusicVolume);
        UpdateMasterValue(AudioListener.volume);
        musicValue.text = Mathf.RoundToInt(player.MusicVolume * 100f) + "%";
        for (int i = 0; i < trackButtons.Length; i++)
        {
            bool available = player.Library != null && i < player.Library.Count && player.Library.tracks[i] != null;
            bool selected = available && i == player.SelectedTrack;
            trackButtons[i].interactable = available;
            trackButtons[i].image.color = selected ? new Color(.36f, .46f, .22f) : new Color(.16f, .20f, .18f);
            trackAccents[i].color = selected ? ProgressionUI.Gold : new Color(.42f, .48f, .39f);
            trackNumbers[i].color = selected ? ProgressionUI.Gold : CartoonUI.Cream;
        }
    }

#if UNITY_EDITOR
    private static Image Box(Transform parent, string name, Color color, float l, float b, float r, float t, bool blocks = false)
    {
        var image = ProgressionUI.Image(parent, name, color, blocks);
        image.sprite = Resources.Load<Sprite>("UI/SettingsFrame");
        image.type = Image.Type.Sliced;
        ProgressionUI.Percent(image.rectTransform, l, b, r, t);
        return image;
    }

    private static Text Label(Transform parent, string name, string text, int size, float l, float b, float r, float t, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var label = ProgressionUI.Text(parent, name, text, size, align);
        CartoonUI.Typography(label, size >= 38);
        ProgressionUI.Percent(label.rectTransform, l, b, r, t);
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    private static Button Tab(Transform parent, string name, string caption, float l, float r)
    {
        var image = Box(parent, name, new Color(.14f, .18f, .16f), l, .715f, r, .825f, true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Label(image.transform, "Label", caption, 38, .05f, .12f, .95f, .88f, TextAnchor.MiddleCenter);
        return button;
    }

    private static Slider Volume(Transform parent, string name, string caption, string hint, float l, float r, out Text readout)
    {
        var section = Box(parent, name + " Section", new Color(.14f, .18f, .16f), l, .635f, r, 1f);
        Label(section.transform, "Caption", caption, 31, .045f, .67f, .78f, .95f);
        readout = Label(section.transform, "Value", "100%", 30, .78f, .67f, .955f, .95f, TextAnchor.MiddleRight);
        readout.color = ProgressionUI.Gold;
        Label(section.transform, "Hint", hint, 25, .045f, .47f, .955f, .66f).color = new Color(.78f, .81f, .70f);
        var target = ProgressionUI.Image(section.transform, name, Color.clear, true);
        ProgressionUI.Percent(target.rectTransform, .055f, .10f, .945f, .43f);
        var slider = target.gameObject.AddComponent<Slider>();
        Box(target.transform, "Track", new Color(.055f, .07f, .055f), 0, .32f, 1, .68f);
        var fillArea = ProgressionUI.Image(target.transform, "Fill Area", Color.clear);
        ProgressionUI.Percent(fillArea.rectTransform, .015f, .32f, .985f, .68f);
        var fill = Box(fillArea.transform, "Fill", ProgressionUI.Gold, 0, 0, 1, 1);
        var handleArea = ProgressionUI.Image(target.transform, "Handle Area", Color.clear);
        ProgressionUI.Percent(handleArea.rectTransform, .025f, 0, .975f, 1);
        var handle = Box(handleArea.transform, "Handle", CartoonUI.Cream, 0, 0, 0, 1, true);
        handle.rectTransform.sizeDelta = new Vector2(38, 0);
        slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        return slider;
    }

    private static void ControlRow(Transform parent, string action, string key, string hint, float bottom, float top)
    {
        var row = Box(parent, action, new Color(.11f, .15f, .13f), .035f, bottom, .965f, top);
        Label(row.transform, "Action", action, 28, .035f, .51f, .57f, .94f);
        Label(row.transform, "Key", key, 30, .59f, .51f, .965f, .94f, TextAnchor.MiddleRight).color = ProgressionUI.Gold;
        Label(row.transform, "Hint", hint, 25, .035f, .08f, .965f, .48f).color = new Color(.78f, .81f, .70f);
    }

    private static void BuildControls(Transform parent)
    {
        var keyboard = Box(parent, "Keyboard and Mouse", new Color(.14f, .18f, .16f), 0, 0, .57f, 1);
        Label(keyboard.transform, "Heading", "TECLADO Y RATÓN", 35, .04f, .865f, .96f, .98f, TextAnchor.MiddleCenter);
        ControlRow(keyboard.transform, "Moverse", "A / D", "También: flechas izquierda y derecha.", .67f, .825f);
        ControlRow(keyboard.transform, "Apuntar", "RATÓN", "Mueve el ratón hacia tu objetivo.", .51f, .665f);
        ControlRow(keyboard.transform, "Disparar", "CLIC IZQUIERDO", "Mantén pulsado para seguir disparando.", .35f, .505f);
        ControlRow(keyboard.transform, "Recargar", "R", "Recarga el arma equipada.", .19f, .345f);
        ControlRow(keyboard.transform, "Cambiar arma", "1 / 2 / 3", "1: Pistola   2: SMG   3: RPG (si las tienes).", .03f, .185f);

        var touch = Box(parent, "Touch Controls", new Color(.14f, .18f, .16f), .595f, 0, 1, 1);
        Label(touch.transform, "Heading", "PANTALLA TÁCTIL", 35, .04f, .865f, .96f, .98f, TextAnchor.MiddleCenter);
        ControlRow(touch.transform, "Moverse", "<   >", "Mantén las flechas de la izquierda.", .63f, .825f);
        ControlRow(touch.transform, "Apuntar y disparar", "ÁREA DERECHA", "Mantén y arrastra el dedo.", .43f, .625f);
        ControlRow(touch.transform, "Recargar", "REC", "Toca el botón de recarga.", .23f, .425f);
        ControlRow(touch.transform, "Cambiar arma", "ICONOS", "Toca el icono del arma que tengas.", .03f, .225f);
    }

    /// <summary>Called by the editor integration and menu builder, keeping the saved UI reproducible.</summary>
    public void BuildLayout(MenuMusicLibrary library)
    {
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
        GetComponent<Image>().color = new Color(.018f, .027f, .022f, .88f);
        var board = Box(transform, "Radio Panel", Color.white, .045f, .055f, .955f, .945f, true);
        board.sprite = Resources.Load<Sprite>("UI/SettingsBoard");
        var shadow = board.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .65f); shadow.effectDistance = new Vector2(0, -12);

        var title = ProgressionUI.Image(board.transform, "Wooden Settings Heading", Color.white);
        title.sprite = CartoonUI.Wood;
        ProgressionUI.Percent(title.rectTransform, .335f, .835f, .665f, .99f);
        Label(title.transform, "Title", "AJUSTES", 64, .13f, .23f, .87f, .8f, TextAnchor.MiddleCenter);
        soundTab = Tab(board.transform, "Sound Tab", "SONIDO", .055f, .485f);
        controlsTab = Tab(board.transform, "Controls Tab", "CONTROLES", .515f, .945f);
        soundContent = ProgressionUI.Image(board.transform, "Sound Content", Color.clear).gameObject;
        ProgressionUI.Percent((RectTransform)soundContent.transform, .055f, .185f, .945f, .69f);
        controlsContent = ProgressionUI.Image(board.transform, "Controls Content", Color.clear).gameObject;
        ProgressionUI.Percent((RectTransform)controlsContent.transform, .055f, .185f, .945f, .69f);
        BuildControls(controlsContent.transform);

        masterVolume = Volume(soundContent.transform, "Master Volume Slider", "VOLUMEN GENERAL", "Música y efectos del juego", 0, .485f, out masterValue);
        musicVolume = Volume(soundContent.transform, "Music Volume Slider", "VOLUMEN DE MÚSICA", "Solo menús y tienda", .515f, 1, out musicValue);
        Label(soundContent.transform, "Soundtrack Heading", "ELIGE LA PISTA", 32, 0, .495f, 1, .605f, TextAnchor.MiddleCenter);
        trackButtons = new Button[2]; trackNumbers = new Text[2]; trackAccents = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            var card = Box(soundContent.transform, "Soundtrack " + (i + 1), new Color(.16f, .20f, .18f), i == 0 ? 0 : .515f, .13f, i == 0 ? .485f : 1, .48f, true);
            var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card;
            var colors = button.colors; colors.highlightedColor = new Color(1.18f, 1.15f, 1.05f); colors.pressedColor = new Color(.72f, .76f, .62f); colors.selectedColor = Color.white; button.colors = colors;
            trackButtons[i] = button;
            var vinyl = ProgressionUI.Image(card.transform, "Vinyl Icon", Color.white);
            vinyl.sprite = Resources.Load<Sprite>("UI/MusicVinyl");
            vinyl.preserveAspect = true;
            ProgressionUI.Percent(vinyl.rectTransform, .34f, .13f, .66f, .99f);
            trackNumbers[i] = Label(card.transform, "Number", (i + 1).ToString(), 52, .045f, .62f, .15f, .92f, TextAnchor.MiddleCenter);
            trackAccents[i] = ProgressionUI.Image(card.transform, "Selection Stripe", ProgressionUI.Gold);
            ProgressionUI.Percent(trackAccents[i].rectTransform, .10f, .12f, .90f, .145f);
        }
        Label(soundContent.transform, "Playback Hint", "Menús y tienda con música. La partida, sin soundtrack.", 25, 0, 0, 1, .11f, TextAnchor.MiddleCenter).color = new Color(.75f, .78f, .68f);
        var back = ProgressionUI.Image(board.transform, "Back Button", Color.white, true);
        back.sprite = CartoonUI.Wood;
        ProgressionUI.Percent(back.rectTransform, .375f, .015f, .625f, .16f);
        BackButton = back.gameObject.AddComponent<Button>(); BackButton.targetGraphic = back;
        Label(back.transform, "Label", "VOLVER", 41, .13f, .23f, .87f, .8f, TextAnchor.MiddleCenter);
        ShowSection(false);
    }
#endif
}
