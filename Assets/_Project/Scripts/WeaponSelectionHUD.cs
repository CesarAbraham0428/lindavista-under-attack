using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Visual weapon selection; combat availability is managed separately.</summary>
public sealed class WeaponSelectionHUD : MonoBehaviour
{
    private static WeaponSelectionHUD active;
    private readonly Image[] rims = new Image[3];
    private readonly Image[] backgrounds = new Image[3];
    private readonly Image[] icons = new Image[3];
    private readonly Button[] buttons = new Button[3];
    private readonly Text[] ammoCounts = new Text[3];
    private Image[] cartridges = new Image[0];
    private readonly HUDSpriteVariants[] ammoArtwork = new HUDSpriteVariants[3];
    private RectTransform ammoStrip;
    private Text ammoStatus;
    private PlayerWeaponController weapons;
    private RectTransform safeArea;
    private Sprite circle;
    private PlayerActions player;
    private int selected;
    private float nextPlayerRefresh;

    public static int SelectedWeapon => active != null ? active.selected : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if ((scene.name == "Testing" || scene.name == "Gameplay") && active == null)
            new GameObject("Weapon Selection HUD").AddComponent<WeaponSelectionHUD>();
    }

    private void Awake()
    {
        active = this;
        ProgressionUI.EnsureEventSystem();
        var artwork = Resources.Load<WeaponHUDAssets>("WeaponHUDAssets");
        for (int i = 0; i < ammoArtwork.Length; i++)
            ammoArtwork[i] = new HUDSpriteVariants(artwork != null ? artwork.AmmoAt(i) : null);
        circle = MakeCircle();
        var canvas = ProgressionUI.Canvas(transform, "Weapon Selection Canvas", 116);
        safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        safeArea.SetParent(canvas.transform, false);
        UpdateSafeArea();

        string[] names = { "Pistol", "SMG", "RPG" };
        Vector2[] sizes = { new Vector2(124, 96), new Vector2(148, 96), new Vector2(158, 74) };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            rims[i] = ProgressionUI.Image(safeArea, names[i] + " Circle", Color.white, true);
            rims[i].sprite = circle;
            // Keep the hit area circular, including where the weapon overhangs the rim.
            rims[i].alphaHitTestMinimumThreshold = 0.5f;
            ProgressionUI.Top(rims[i].rectTransform, new Vector2(0, 1),
                new Vector2(32, -220 - i * 164), new Vector2(132, 132));
            buttons[i] = rims[i].gameObject.AddComponent<Button>();
            buttons[i].targetGraphic = rims[i];
            buttons[i].transition = Selectable.Transition.None;
            buttons[i].navigation = new Navigation { mode = Navigation.Mode.None };
            buttons[i].onClick.AddListener(() => Select(index));

            backgrounds[i] = ProgressionUI.Image(rims[i].transform, "Circle Fill", Color.white);
            backgrounds[i].sprite = circle;
            Center(backgrounds[i].rectTransform, new Vector2(119, 119));
            icons[i] = ProgressionUI.Image(rims[i].transform, names[i] + " Icon", Color.white);
            icons[i].sprite = artwork != null ? artwork.IconAt(i) : null;
            icons[i].preserveAspect = true;
            icons[i].enabled = icons[i].sprite != null;
            Center(icons[i].rectTransform, sizes[i]);
            ammoCounts[i] = ProgressionUI.Text(rims[i].transform, "Ammo Count", "—", 44,
                TextAnchor.MiddleRight);
            ProgressionUI.Top(ammoCounts[i].rectTransform, new Vector2(0, 1),
                new Vector2(38, -94), new Vector2(98, 50));
            var outline = ammoCounts[i].gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.95f);
            outline.effectDistance = new Vector2(2, -2);
        }
        var ammoPanel = ProgressionUI.Image(safeArea, "Selected Weapon Ammo", Color.clear);
        ProgressionUI.Top(ammoPanel.rectTransform, new Vector2(0, 1),
            new Vector2(190, -114), new Vector2(248, 76));
        ammoStrip = new GameObject("Cartridges", typeof(RectTransform)).GetComponent<RectTransform>();
        ammoStrip.SetParent(ammoPanel.transform, false);
        ProgressionUI.Top(ammoStrip, new Vector2(0, 1), new Vector2(0, 0), new Vector2(248, 46));
        ammoStatus = ProgressionUI.Text(ammoPanel.transform, "Ammo Status", "", 20);
        ProgressionUI.Top(ammoStatus.rectTransform, new Vector2(0, 1),
            new Vector2(0, -47), new Vector2(248, 29));
        var statusOutline = ammoStatus.gameObject.AddComponent<Outline>();
        statusOutline.effectColor = Color.black;
        statusOutline.effectDistance = new Vector2(1, -1);
        RefreshSelection();
    }

    private void Update()
    {
        UpdateSafeArea();
        if (Time.unscaledTime >= nextPlayerRefresh)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                player = FindFirstObjectByType<PlayerActions>();
            BindAmmo();
            nextPlayerRefresh = Time.unscaledTime + 0.5f;
        }

        bool canSelect = CanSelect();
        foreach (Button button in buttons)
            button.interactable = canSelect;
        if (!canSelect || Keyboard.current == null) return;
        if (Keyboard.current.digit1Key.wasPressedThisFrame) Select(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) Select(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) Select(2);
    }

    public static void Select(int index)
    {
        if (active == null || index < 0 || index >= 3 || !active.CanSelect()) return;
        active.selected = index;
        active.RefreshSelection();
    }

    private bool CanSelect() => player != null && !player.IsInputLocked && !player.IsDefeated &&
        !GameFlowController.IsMatchEnded && !PistolUpgradeShop.IsOpen;

    private void RefreshSelection()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            bool highlighted = selected == i;
            rims[i].color = highlighted ? new Color(1f, 0.89f, 0.65f) : new Color(0.40f, 0.36f, 0.27f);
            backgrounds[i].color = highlighted ? new Color(0.48f, 0.62f, 0.34f, 0.96f) :
                new Color(0.15f, 0.20f, 0.16f, 0.92f);
            icons[i].color = highlighted ? Color.white : new Color(0.46f, 0.46f, 0.46f, 1f);
            ammoCounts[i].color = highlighted ? Color.white : new Color(0.65f, 0.65f, 0.65f);
        }
        RefreshAmmo();
    }

    private void BindAmmo()
    {
        var current = player != null ? player.Weapons : null;
        if (weapons == current) return;
        if (weapons != null) weapons.Changed -= RefreshAmmo;
        weapons = current;
        if (weapons != null) weapons.Changed += RefreshAmmo;
        RefreshAmmo();
    }

    private void RefreshAmmo()
    {
        if (ammoStatus == null) return;
        string[] names = { "PISTOLA", "METRALLETA", "RPG" };
        for (int i = 0; i < ammoCounts.Length; i++)
            ammoCounts[i].text = weapons != null && weapons.EquippedWeapon == i
                ? weapons.Magazine.ToString() : "—";

        bool available = weapons != null && weapons.EquippedWeapon == selected;
        int capacity = available ? weapons.Definition.MagazineSize : 1;
        if (cartridges.Length != capacity) BuildCartridges(capacity);
        int remaining = available ? weapons.Magazine : 0;
        for (int i = 0; i < cartridges.Length; i++)
        {
            bool filled = i < remaining;
            cartridges[i].sprite = filled ? ammoArtwork[selected].ColorSprite : ammoArtwork[selected].GraySprite;
            cartridges[i].color = filled ? Color.white : new Color(0.65f, 0.65f, 0.65f);
        }
        ammoStatus.text = names[selected] + (available
            ? "  " + remaining + "/" + capacity + (weapons.IsReloading ? " · RECARGANDO…" : "")
            : " · NO DISPONIBLE");
    }

    private void BuildCartridges(int capacity)
    {
        foreach (Transform child in ammoStrip) Destroy(child.gameObject);
        cartridges = new Image[capacity];
        if (capacity == 0) return;
        float step = Mathf.Min(28f, 248f / capacity);
        float gap = Mathf.Min(3f, step * 0.15f);
        for (int i = 0; i < capacity; i++)
        {
            cartridges[i] = ProgressionUI.Image(ammoStrip, "Cartridge " + (i + 1), Color.white);
            cartridges[i].preserveAspect = true;
            ProgressionUI.Top(cartridges[i].rectTransform, new Vector2(0, 1),
                new Vector2(i * step, 0), new Vector2(step - gap, 46));
        }
    }

    private void UpdateSafeArea()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        Rect area = Screen.safeArea;
        safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static Sprite MakeCircle()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Weapon HUD Circle";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), Vector2.one * 63.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(63.5f - distance));
            }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
        if (weapons != null) weapons.Changed -= RefreshAmmo;
        foreach (var artwork in ammoArtwork) artwork?.Dispose();
        if (circle != null)
        {
            Destroy(circle.texture);
            Destroy(circle);
        }
    }
}
