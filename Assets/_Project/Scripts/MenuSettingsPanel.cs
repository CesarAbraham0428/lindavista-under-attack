using UnityEngine;
using UnityEngine.UI;

/// <summary>Radio styled settings view. The layout is serialized in the menu scene.</summary>
public sealed class MenuSettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider masterVolume;
    [SerializeField] private Slider musicVolume;
    [SerializeField] private Text masterValue;
    [SerializeField] private Text musicValue;
    [SerializeField] private Button[] trackButtons;
    [SerializeField] private Text[] trackStates;
    [SerializeField] private Image[] trackAccents;
    private MenuMusicPlayer player;
    private bool bound;
    public Slider MasterVolume => masterVolume;
    public Button BackButton { get; private set; }

    private void Awake()
    {
        if (masterVolume == null || musicVolume == null) return;
        player = MenuMusicPlayer.Instance;
        masterVolume.onValueChanged.AddListener(UpdateMasterValue);
        musicVolume.onValueChanged.AddListener(SetMusicVolume);
        for (int i = 0; i < trackButtons.Length; i++)
        {
            int index = i;
            trackButtons[i].onClick.AddListener(() => player.SelectTrack(index));
        }
        bound = true;
    }

    private void OnEnable()
    {
        if (!bound || player == null) return;
        player.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (player != null) player.Changed -= Refresh;
        if (Application.isPlaying) PlayerPrefs.Save();
    }

    private void UpdateMasterValue(float value) { masterValue.text = Mathf.RoundToInt(value * 100f) + "%"; }
    private void SetMusicVolume(float value) { player.SetMusicVolume(value); }

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
            trackStates[i].text = !available ? "NO DISPONIBLE" : selected ? "SONANDO EN LOS MENÚS" : "TOCA PARA ELEGIR";
            trackStates[i].color = selected ? ProgressionUI.Gold : new Color(.76f, .79f, .68f);
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

    private static Slider Volume(Transform parent, string name, string caption, float l, float r, out Text readout)
    {
        var section = Box(parent, name + " Section", new Color(.14f, .18f, .16f), l, .60f, r, .765f);
        Label(section.transform, "Caption", caption, 31, .045f, .56f, .78f, .92f);
        readout = Label(section.transform, "Value", "100%", 30, .78f, .56f, .955f, .92f, TextAnchor.MiddleRight);
        readout.color = ProgressionUI.Gold;
        var target = ProgressionUI.Image(section.transform, name, Color.clear, true);
        ProgressionUI.Percent(target.rectTransform, .055f, .10f, .945f, .51f);
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

    /// <summary>Called by the editor integration and menu builder, keeping the saved UI reproducible.</summary>
    public void BuildLayout(MenuMusicLibrary library)
    {
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
        GetComponent<Image>().color = new Color(.018f, .027f, .022f, .88f);
        var board = Box(transform, "Radio Panel", Color.white, .045f, .055f, .955f, .945f, true);
        board.sprite = Resources.Load<Sprite>("UI/SettingsBoard");
        var shadow = board.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .65f); shadow.effectDistance = new Vector2(0, -12);

        for (int i = 0; i < 2; i++)
        {
            var speaker = ProgressionUI.Image(board.transform, "Speaker " + i, Color.white);
            speaker.sprite = Resources.Load<Sprite>("UI/SettingsSpeaker"); speaker.preserveAspect = true;
            ProgressionUI.Percent(speaker.rectTransform, i == 0 ? .055f : .805f, .795f, i == 0 ? .195f : .945f, .97f);
        }
        var title = ProgressionUI.Image(board.transform, "Wooden Settings Heading", Color.white);
        title.sprite = CartoonUI.Wood;
        ProgressionUI.Percent(title.rectTransform, .335f, .805f, .665f, .98f);
        Label(title.transform, "Title", "AJUSTES", 64, .13f, .23f, .87f, .8f, TextAnchor.MiddleCenter);
        Label(board.transform, "Subtitle", "EL SONIDO DEL BARRIO", 24, .2f, .765f, .8f, .812f, TextAnchor.MiddleCenter).color = new Color(.78f, .82f, .64f);
        masterVolume = Volume(board.transform, "Master Volume Slider", "VOLUMEN GENERAL", .055f, .485f, out masterValue);
        musicVolume = Volume(board.transform, "Music Volume Slider", "VOLUMEN DE MÚSICA", .515f, .945f, out musicValue);
        Label(board.transform, "Soundtrack Heading", "ELIGE TU SOUNDTRACK", 32, .055f, .525f, .945f, .585f, TextAnchor.MiddleCenter);
        trackButtons = new Button[2]; trackStates = new Text[2]; trackAccents = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            var card = Box(board.transform, "Soundtrack " + (i + 1), new Color(.16f, .20f, .18f), i == 0 ? .055f : .515f, .24f, i == 0 ? .485f : .945f, .52f, true);
            var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card;
            var colors = button.colors; colors.highlightedColor = new Color(1.18f, 1.15f, 1.05f); colors.pressedColor = new Color(.72f, .76f, .62f); colors.selectedColor = Color.white; button.colors = colors;
            trackButtons[i] = button;
            var badge = Box(card.transform, "Track Number Badge", new Color(.10f, .14f, .12f), .045f, .56f, .19f, .89f);
            Label(badge.transform, "Number", "0" + (i + 1), 41, 0, 0, 1, 1, TextAnchor.MiddleCenter).color = ProgressionUI.Gold;
            Label(card.transform, "Track Title", library.titles[i], 43, .23f, .52f, .95f, .94f);
            Label(card.transform, "Artist", library.artists[i] + " · Instrumental", 26, .235f, .34f, .95f, .54f).color = new Color(.78f, .81f, .70f);
            trackAccents[i] = ProgressionUI.Image(card.transform, "Selection Stripe", ProgressionUI.Gold);
            ProgressionUI.Percent(trackAccents[i].rectTransform, .045f, .24f, .95f, .265f);
            trackStates[i] = Label(card.transform, "Selection State", i == 0 ? "SONANDO EN LOS MENÚS" : "TOCA PARA ELEGIR", 25, .045f, .055f, .95f, .22f, TextAnchor.MiddleCenter);
        }
        Label(board.transform, "Playback Hint", "Menús y tienda con música. La partida, sin soundtrack.", 25, .06f, .16f, .94f, .23f, TextAnchor.MiddleCenter).color = new Color(.75f, .78f, .68f);
        var back = ProgressionUI.Image(board.transform, "Back Button", Color.white, true);
        back.sprite = CartoonUI.Wood;
        ProgressionUI.Percent(back.rectTransform, .375f, .015f, .625f, .16f);
        BackButton = back.gameObject.AddComponent<Button>(); BackButton.targetGraphic = back;
        Label(back.transform, "Label", "VOLVER", 41, .13f, .23f, .87f, .8f, TextAnchor.MiddleCenter);
    }
#endif
}
