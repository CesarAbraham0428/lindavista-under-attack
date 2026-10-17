using UnityEngine;
using UnityEngine.UI;

public sealed class PistolUpgradeShop : MonoBehaviour
{
    private static PistolUpgradeShop active;
    private static readonly Color Muted = new Color(.43f, .39f, .28f);
    private Text wallet, health, feedback;
    private Button healthBuy, speedBuy, consumableBuy;
    private Text consumableCount;
    private readonly Image[] ammoFill = new Image[3];
    private Image[] life;
    private bool cesar;
    private Image portrait;
    private Text speed;
    private readonly Image[] speedBars = new Image[3];
    private readonly Image[] weaponIcons = new Image[3];
    private readonly Text[] weaponNames = new Text[3];
    private readonly Button[] characterTabs = new Button[2];
    private readonly Text[] stats = new Text[3];
    private readonly Button[] ammo = new Button[3], upgrade = new Button[3];
    private readonly Image[,] bars = new Image[3, 6];
    private Sprite fillSprite;
    private ShopCharacterAssets retratos;
    public static bool IsOpen => active != null;
    public static void Open() { if (active == null && !BossIntroduction.IsActive) new GameObject("Character and Weapons Shop").AddComponent<PistolUpgradeShop>(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = null; }

    private static Text Label(Transform parent, string text, int size, float l, float b, float r, float t)
    {
        var label = ProgressionUI.Text(parent, text, text, size);
        CartoonUI.Typography(label, size >= 34);
        ProgressionUI.Percent(label.rectTransform, l, b, r, t);
        return label;
    }
    private static Image Frame(Transform parent, string name, Color color, float l, float b, float r, float t)
    {
        var image = ProgressionUI.Image(parent, name, color);
        CartoonUI.Frame(image, color);
        ProgressionUI.Percent(image.rectTransform, l, b, r, t);
        return image;
    }
    private static void Coin(Transform parent, float l, float b, float r, float t)
    {
        var coin = ProgressionUI.Image(parent, "Coin", Color.white);
        coin.sprite = ProgressionVisuals.Coin;
        coin.preserveAspect = true;
        ProgressionUI.Percent(coin.rectTransform, l, b, r, t);
    }
    private static Text Currency(Transform parent, string name, int size, float l, float b, float r, float t)
    {
        var row = ProgressionUI.Image(parent, name, Color.clear);
        ProgressionUI.Percent(row.rectTransform, l, b, r, t);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.spacing = size * .2f;
        Coin(row.transform, 0, 0, 1, 1);
        var icon = row.transform.Find("Coin").gameObject.AddComponent<LayoutElement>();
        icon.preferredWidth = icon.preferredHeight = size * 1.2f;
        var value = Label(row.transform, "0", size, 0, 0, 1, 1);
        value.gameObject.name = "Cost";
        value.alignment = TextAnchor.MiddleCenter;
        value.horizontalOverflow = HorizontalWrapMode.Overflow;
        return value;
    }
    private static Button Buy(Transform parent, string text, UnityEngine.Events.UnityAction action,
        float l, float b, float r, float t, bool purchase = false, bool gold = false)
    {
        var button = ProgressionUI.Button(parent, text, action);
        CartoonUI.ButtonStyle(button);
        button.image.color = gold ? new Color(.78f, .39f, .12f) : new Color(.35f, .48f, .19f);
        ProgressionUI.Percent((RectTransform)button.transform, l, b, r, t);
        var label = button.GetComponentInChildren<Text>();
        label.resizeTextMaxSize = 32;
        if (purchase)
        {
            ProgressionUI.Percent(label.rectTransform, .04f, .45f, .96f, .96f);
            Currency(button.transform, "Price", 30, .16f, .06f, .84f, .44f);
        }
        return button;
    }
    private static void PurchaseState(Button button, string title, int cost, bool available)
    {
        button.transform.Find("Label").GetComponent<Text>().text = title;
        var price = button.transform.Find("Price");
        price.gameObject.SetActive(cost > 0);
        price.Find("Cost").GetComponent<Text>().text = cost.ToString("N0");
        ProgressionUI.Percent(button.transform.Find("Label").GetComponent<RectTransform>(),
            .04f, cost > 0 ? .45f : .08f, .96f, .96f);
        button.interactable = available && cost > 0 && ProgressionService.Coins >= cost && ProgressionService.CanSave;
    }
    private void Awake()
    {
        active = this;
        MobileControlsHUD.CancelActiveTouches();
        cesar = CharacterWeapons.SelectedCesar;
        MenuMusicPlayer.NotifyShop(true);
        retratos = Resources.Load<ShopCharacterAssets>("ShopCharacterAssets");
        fillSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
        var canvas = ProgressionUI.Canvas(transform, "Shop Canvas", 400);
        var overlay = ProgressionUI.Image(canvas.transform, "Overlay", new Color(.035f, .02f, .015f, .82f), true);
        ProgressionUI.Percent(overlay.rectTransform, 0, 0, 1, 1);
        var zona = AreaSeguraUI.Crear(canvas.transform);
        var contenido = ContenidoAdaptableUI.Crear(zona, new Vector2(1920f, 1080f));
        var panel = Frame(contenido, "Shop Panel", new Color(.24f, .135f, .07f), .035f, .045f, .965f, .95f);
        Frame(panel.transform, "Inset board", new Color(.32f, .21f, .115f), .012f, .015f, .988f, .982f);
        var banner = ProgressionUI.Image(panel.transform, "Store wooden heading", Color.white);
        banner.sprite = CartoonUI.Wood;
        ProgressionUI.Percent(banner.rectTransform, .018f, .845f, .42f, 1.035f);
        var heading = Label(panel.transform, "ARSENAL", 64, .07f, .885f, .37f, .989f);
        heading.color = ProgressionUI.Gold;
        heading.alignment = TextAnchor.MiddleCenter;
        var purse = Frame(panel.transform, "Wallet frame", new Color(.13f, .16f, .12f), .74f, .895f, .975f, .978f);
        wallet = Currency(purse.transform, "Balance", 42, .055f, .08f, .945f, .92f);
        var character = Frame(panel.transform, "Character", new Color(.18f, .24f, .19f), .025f, .185f, .285f, .86f);
        Frame(character.transform, "Portrait backdrop", new Color(.27f, .34f, .25f), .025f, .44f, .975f, .965f);
        characterTabs[0] = Buy(character.transform, "MARCO", () => SelectCharacter(false), .045f, .87f, .49f, .975f);
        characterTabs[1] = Buy(character.transform, "CÉSAR", () => SelectCharacter(true), .51f, .87f, .955f, .975f);
        portrait = ProgressionUI.Image(character.transform, "Portrait", Color.white);
        portrait.preserveAspect = true;
        ProgressionUI.Percent(portrait.rectTransform, .025f, .445f, .975f, .855f);
        speed = Label(character.transform, "", 27, .06f, .17f, .94f, .22f);
        speed.alignment = TextAnchor.MiddleCenter;
        var speedMeter = Frame(character.transform, "Speed meter", new Color(.10f, .14f, .105f), .07f, .13f, .93f, .17f);
        for (int j = 0; j < speedBars.Length; j++)
        {
            speedBars[j] = ProgressionUI.Image(speedMeter.transform, "Speed " + (j + 1), Muted);
            speedBars[j].sprite = CartoonUI.FrameSprite;
            speedBars[j].type = Image.Type.Sliced;
        }
        speedBuy = Buy(character.transform, "MEJORAR VELOCIDAD", () => Result(ProgressionService.TryUpgradeSpeed(cesar)),
            .065f, .015f, .935f, .125f, true, true);
        health = Label(character.transform, "", 34, .08f, .38f, .92f, .44f);
        var lifeMeter = Frame(character.transform, "Health meter", new Color(.10f, .14f, .105f), .07f, .335f, .93f, .38f);
        life = new Image[CharacterStats.MaximumHealth(true)];
        for (int j = 0; j < life.Length; j++)
        {
            life[j] = ProgressionUI.Image(lifeMeter.transform, "Health " + (j + 1), Color.white);
        }
        healthBuy = Buy(character.transform, "MEJORAR VIDA", () => Result(ProgressionService.TryUpgradeHealth(cesar)),
            .065f, .225f, .935f, .33f, true, true);
        var art = CharacterWeapons.Artwork(cesar);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float bottom = .675f - i * .205f;
            var row = Frame(panel.transform, "Weapon " + i, new Color(.46f, .32f, .19f), .305f, bottom, .975f, bottom + .185f);
            var frame = Frame(row.transform, "Icon Frame", new Color(.83f, .71f, .47f), .018f, .105f, .19f, .905f);
            var icon = ProgressionUI.Image(frame.transform, "Weapon Icon", Color.white);
            weaponIcons[i] = icon;
            icon.sprite = art != null ? art.IconAt(i) : ProgressionVisuals.Pistol;
            icon.preserveAspect = true;
            ProgressionUI.Percent(icon.rectTransform, .07f, .12f, .93f, .88f);
            weaponNames[i] = Label(row.transform, CharacterWeapons.Name(i, cesar), 37, .215f, .735f, .72f, .975f);
            int tiers = WeaponDefinition.ForCharacter(i, cesar).MaxUpgrade + 1;
            for (int j = 0; j < 6; j++)
            {
                bars[i, j] = ProgressionUI.Image(row.transform, "Level " + j, Muted);
                bars[i, j].sprite = CartoonUI.FrameSprite;
                bars[i, j].type = Image.Type.Sliced;
                float step = .25f / tiers;
                ProgressionUI.Percent(bars[i, j].rectTransform, .725f + j * step, .815f, .725f + (j + 1) * step - .006f, .92f);
                bars[i, j].gameObject.SetActive(j < tiers);
            }
            stats[i] = Label(row.transform, "", 29, .215f, .58f, .975f, .745f);
            var meter = Frame(row.transform, "Ammo meter", new Color(.12f, .14f, .10f), .215f, .47f, .975f, .57f);
            ammoFill[i] = ProgressionUI.Image(meter.transform, "Ammo fill", new Color(.76f, .85f, .38f));
            ammoFill[i].sprite = fillSprite;
            ammoFill[i].type = Image.Type.Filled;
            ammoFill[i].fillMethod = Image.FillMethod.Horizontal;
            ammoFill[i].fillOrigin = 0;
            ProgressionUI.Percent(ammoFill[i].rectTransform, 0, 0, 1, 1);
            // Fixed inset keeps the fill inside the frame at every aspect ratio and amount.
            ammoFill[i].rectTransform.offsetMin = new Vector2(9, 5);
            ammoFill[i].rectTransform.offsetMax = new Vector2(-9, -5);
            ammo[i] = Buy(row.transform, "MUNICIÓN", () => Result(ProgressionService.TryBuyAmmo(index, cesar)), .215f, .055f, .595f, .435f, true);
            upgrade[i] = Buy(row.transform, "MEJORAR ARMA", () => Result(ProgressionService.TryUpgradeWeapon(index, cesar)), .615f, .055f, .975f, .435f, true, true);
        }
        var consumables = Frame(panel.transform, "Health consumables", new Color(.25f, .34f, .22f), .305f, .13f, .975f, .255f);
        var pill = ProgressionUI.Image(consumables.transform, "Pastillas", Color.white);
        var consumableAssets = HealthConsumableAssets.Load();
        pill.sprite = consumableAssets != null ? consumableAssets.icon : null;
        pill.preserveAspect = true;
        ProgressionUI.Percent(pill.rectTransform, .01f, .06f, .15f, .94f);
        Label(consumables.transform, "PASTILLAS · VIDA +1", 34, .17f, .53f, .65f, .94f);
        consumableCount = Label(consumables.transform, "", 29, .17f, .07f, .65f, .52f);
        consumableBuy = Buy(consumables.transform, "COMPRAR PASTILLA",
            () => Result(ProgressionService.TryBuyHealthConsumable(cesar)), .68f, .09f, .98f, .91f, true);
        feedback = Label(panel.transform, "Compras e inventario por personaje. Cada pastilla recupera 1 de vida.", 27, .305f, .085f, .975f, .125f);
        Buy(panel.transform, "VOLVER", () => Destroy(gameObject), .025f, .025f, .25f, .115f);
#if UNITY_EDITOR
        Buy(panel.transform, "+1000 monedas · prueba", () => Result(ProgressionService.TryCredit(1000)), .735f, .025f, .975f, .095f);
#endif
        if (Application.isMobilePlatform || Screen.height <= 800)
            ConfigurarDistribucionCompacta(panel.rectTransform, character.rectTransform);
        ProgressionService.Changed += Refresh;
        Refresh();
    }
    private void Refresh()
    {
        wallet.text = ProgressionService.Coins.ToString("N0");
        var characters = retratos;
        portrait.sprite = characters != null ? (cesar ? characters.cesar : characters.marco) : null;
        portrait.enabled = portrait.sprite != null;
        characterTabs[0].interactable = cesar;
        characterTabs[1].interactable = !cesar;
        int speedLevel = ProgressionService.SpeedUpgradeFor(cesar);
        int maximumSpeed = CharacterStats.MaximumSpeedBars(cesar);
        speed.text = "VELOCIDAD " + (speedLevel + 1) + " / " + maximumSpeed + "  ·  " +
            Mathf.RoundToInt(CharacterStats.SpeedMultiplierAt(cesar, speedLevel) * 100f) + "%";
        for (int j = 0; j < speedBars.Length; j++)
        {
            speedBars[j].gameObject.SetActive(j < maximumSpeed);
            float step = .95f / maximumSpeed;
            ProgressionUI.Percent(speedBars[j].rectTransform, .025f + j * step, .18f,
                .025f + (j + 1) * step - .012f, .82f);
            speedBars[j].color = j <= speedLevel ? ProgressionUI.Gold : Muted;
        }
        int sc = ShopBalance.SpeedCost(speedLevel, cesar);
        PurchaseState(speedBuy, sc > 0 ? "MEJORAR VELOCIDAD" : "VELOCIDAD MÁXIMA", sc, true);
        int hp = ProgressionService.MaxHealthFor(cesar);
        int maximum = CharacterStats.MaximumHealth(cesar);
        health.text = "VIDA  " + hp + " / " + maximum;
        for (int j = 0; j < life.Length; j++)
        {
            life[j].gameObject.SetActive(j < maximum);
            float step = .95f / maximum;
            ProgressionUI.Percent(life[j].rectTransform, .025f + j * step, .20f, .025f + (j + 1) * step - .008f, .80f);
            life[j].color = j < hp ? new Color(.97f, .38f, .23f) : Muted;
        }
        int hc = ShopBalance.HealthCost(ProgressionService.HealthUpgradeFor(cesar));
        PurchaseState(healthBuy, hc > 0 ? "MEJORAR VIDA +1" : "VIDA AL MÁXIMO", hc, true);
        int pills = ProgressionService.HealthConsumables(cesar);
        bool roomForPill = pills < ShopBalance.MaxHealthConsumables;
        consumableCount.text = pills + " de " + ShopBalance.MaxHealthConsumables;
        PurchaseState(consumableBuy, roomForPill ? "COMPRAR PASTILLA" : "LÍMITE: 10 PASTILLAS",
            roomForPill ? ShopBalance.HealthConsumableCost : 0, roomForPill);
        var art = CharacterWeapons.Artwork(cesar);
        for (int i = 0; i < 3; i++)
        {
            var definition = WeaponDefinition.ForCharacter(i, cesar);
            int level = ProgressionService.WeaponUpgrade(i, cesar), cost = definition.UpgradeCost(level);
            int amount = ProgressionService.Ammo(i, cesar);
            weaponNames[i].text = CharacterWeapons.Name(i, cesar);
            weaponIcons[i].sprite = art != null ? art.IconAt(i) : ProgressionVisuals.Pistol;
            int capacity = ShopBalance.CapacityFor(i, cesar);
            stats[i].text = "Munición " + amount + "/" + capacity + "  ·  Daño " + definition.DamageAt(level) +
                (definition.IsPiercing ? "  ·  Atraviesa " + definition.TargetsAt(level) : "");
            ammoFill[i].fillAmount = Mathf.Clamp01((float)amount / capacity);
            bool fits = amount + ShopBalance.Pack[i] <= capacity;
            PurchaseState(ammo[i], fits ? "MUNICIÓN +" + ShopBalance.Pack[i] : "SIN ESPACIO", ShopBalance.AmmoCost[i], fits);
            PurchaseState(upgrade[i], cost > 0 ? "DAÑO +1" : "MEJORA MÁXIMA", cost, true);
            int tiers = definition.MaxUpgrade + 1;
            for (int j = 0; j < 6; j++)
            {
                bars[i, j].gameObject.SetActive(j < tiers);
                float step = .25f / tiers;
                ProgressionUI.Percent(bars[i, j].rectTransform, .725f + j * step, .815f, .725f + (j + 1) * step - .006f, .92f);
                bars[i, j].color = j <= level ? ProgressionUI.Gold : Muted;
            }
        }
        if (!ProgressionService.CanSave) feedback.text = ProgressionService.LastError;
    }
    private void SelectCharacter(bool value)
    {
        cesar = value;
        Refresh();
        feedback.text = "Compras para " + (cesar ? "César" : "Marco") + ".";
    }
    private void ConfigurarDistribucionCompacta(RectTransform panel, RectTransform personaje)
    {
        var desplazamiento = ProgressionUI.Image(panel, "Compras desplazables", Color.clear, true);
        ProgressionUI.Percent(desplazamiento.rectTransform, .025f, .19f, .975f, .86f);
        desplazamiento.gameObject.AddComponent<RectMask2D>();
        var lista = new GameObject("Contenido de compras", typeof(RectTransform)).GetComponent<RectTransform>();
        lista.SetParent(desplazamiento.transform, false);
        lista.anchorMin = new Vector2(0f, 1f);
        lista.anchorMax = Vector2.one;
        lista.pivot = new Vector2(.5f, 1f);
        lista.sizeDelta = new Vector2(-28f, 1920f);
        lista.anchoredPosition = new Vector2(-14f, 0f);
        var scroll = desplazamiento.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = desplazamiento.rectTransform;
        scroll.content = lista;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
        personaje.SetParent(lista, false);
        ColocarFilaCompacta(personaje, 0f, 600f);
        ProgressionUI.Percent((RectTransform)personaje.Find("Portrait backdrop"), .01f, .025f, .35f, .98f);
        ProgressionUI.Percent(portrait.rectTransform, .015f, .035f, .345f, .79f);
        ProgressionUI.Percent((RectTransform)characterTabs[0].transform, .02f, .81f, .175f, .97f);
        ProgressionUI.Percent((RectTransform)characterTabs[1].transform, .185f, .81f, .34f, .97f);
        ProgressionUI.Percent(health.rectTransform, .39f, .80f, .96f, .95f);
        ProgressionUI.Percent((RectTransform)life[0].transform.parent, .39f, .68f, .96f, .78f);
        ProgressionUI.Percent((RectTransform)healthBuy.transform, .39f, .48f, .96f, .65f);
        ProgressionUI.Percent(speed.rectTransform, .39f, .34f, .96f, .45f);
        ProgressionUI.Percent((RectTransform)speedBars[0].transform.parent, .39f, .25f, .96f, .32f);
        ProgressionUI.Percent((RectTransform)speedBuy.transform, .39f, .055f, .96f, .23f);
        var pastillas = (RectTransform)panel.Find("Health consumables");
        pastillas.SetParent(lista, false);
        ColocarFilaCompacta(pastillas, 620f, 260f);
        AmpliarBotonCompacto(consumableBuy);
        AmpliarTextoCompacto(consumableCount, 36);
        AmpliarTextoCompacto(health, 40);
        AmpliarTextoCompacto(speed, 36);
        AmpliarTextoCompacto(wallet, 48);
        AmpliarTextoCompacto(feedback, 32);
        AmpliarBotonCompacto(healthBuy);
        AmpliarBotonCompacto(speedBuy);
        foreach (var pestana in characterTabs) AmpliarBotonCompacto(pestana);
        for (int i = 0; i < 3; i++)
        {
            var fila = (RectTransform)panel.Find("Weapon " + i);
            fila.SetParent(lista, false);
            ColocarFilaCompacta(fila, 900f + i * 340f, 320f);
            AmpliarTextoCompacto(weaponNames[i], 44);
            AmpliarTextoCompacto(stats[i], 36);
            AmpliarBotonCompacto(ammo[i]);
            AmpliarBotonCompacto(upgrade[i]);
        }
        var pista = ProgressionUI.Text(panel, "Ayuda de desplazamiento", "DESLIZA PARA VER TODAS LAS MEJORAS  ↕", 27, TextAnchor.MiddleCenter);
        CartoonUI.Typography(pista);
        ProgressionUI.Percent(pista.rectTransform, .27f, .025f, .72f, .10f);
        ProgressionUI.Percent(feedback.rectTransform, .025f, .12f, .975f, .18f);
        var carril = ProgressionUI.Image(desplazamiento.transform, "Carril de desplazamiento", new Color(0f, 0f, 0f, .25f), true);
        carril.rectTransform.anchorMin = new Vector2(1f, 0f);
        carril.rectTransform.anchorMax = Vector2.one;
        carril.rectTransform.pivot = new Vector2(1f, .5f);
        carril.rectTransform.sizeDelta = new Vector2(18f, 0f);
        var asa = ProgressionUI.Image(carril.transform, "Indicador de desplazamiento", ProgressionUI.Gold, true);
        ProgressionUI.Percent(asa.rectTransform, 0f, 0f, 1f, 1f);
        var barra = carril.gameObject.AddComponent<Scrollbar>();
        barra.handleRect = asa.rectTransform;
        barra.targetGraphic = asa;
        barra.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = barra;
        scroll.verticalNormalizedPosition = 1f;
    }

    private static void AmpliarBotonCompacto(Button boton)
    {
        foreach (var texto in boton.GetComponentsInChildren<Text>(true))
            AmpliarTextoCompacto(texto, texto.name == "Cost" ? 38 : 40);
    }

    private static void AmpliarTextoCompacto(Text texto, int tamano)
    {
        texto.fontSize = texto.resizeTextMaxSize = tamano;
        texto.resizeTextMinSize = 24;
        var currency = texto.transform.parent.GetComponent<HorizontalLayoutGroup>();
        if (currency != null)
        {
            currency.spacing = tamano * .2f;
            var icon = currency.transform.Find("Coin").GetComponent<LayoutElement>();
            icon.preferredWidth = icon.preferredHeight = tamano * 1.2f;
        }
    }

    private static void ColocarFilaCompacta(RectTransform fila, float desdeArriba, float alto)
    {
        fila.anchorMin = new Vector2(0f, 1f);
        fila.anchorMax = Vector2.one;
        fila.pivot = new Vector2(.5f, 1f);
        fila.sizeDelta = new Vector2(0f, alto);
        fila.anchoredPosition = new Vector2(0f, -desdeArriba);
    }
    private void Result(bool success) { feedback.text = success ? "¡Compra completada y guardada!" : ProgressionService.LastError ?? "No se pudo completar la compra."; }
    private void OnDestroy()
    {
        ProgressionService.Changed -= Refresh;
        if (active == this) active = null;
        MenuMusicPlayer.NotifyShop(false);
        if (fillSprite != null) Destroy(fillSprite);
    }
}
