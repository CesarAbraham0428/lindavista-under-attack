using UnityEngine;
using UnityEngine.UI;

public sealed class PistolUpgradeShop : MonoBehaviour
{
    private static PistolUpgradeShop active;
    private static readonly Color Muted = new Color(.43f, .39f, .28f);
    private Text wallet, health, feedback;
    private Button healthBuy;
    private readonly Image[] ammoFill = new Image[3];
    private readonly Image[] life = new Image[10];
    private readonly Text[] stats = new Text[3];
    private readonly Button[] ammo = new Button[3], upgrade = new Button[3];
    private readonly Image[,] bars = new Image[3, 6];
    private Sprite fillSprite;
    public static bool IsOpen => active != null;
    public static void Open() { if (active == null) new GameObject("Character and Weapons Shop").AddComponent<PistolUpgradeShop>(); }
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
            var price = ProgressionUI.Image(button.transform, "Price", Color.clear);
            ProgressionUI.Percent(price.rectTransform, .16f, .06f, .84f, .44f);
            Coin(price.transform, .08f, .04f, .30f, .96f);
            var value = Label(price.transform, "Cost", 30, .32f, 0, .94f, 1);
            value.gameObject.name = "Cost";
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
        MenuMusicPlayer.NotifyShop(true);
        fillSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
        var canvas = ProgressionUI.Canvas(transform, "Shop Canvas", 400);
        var overlay = ProgressionUI.Image(canvas.transform, "Overlay", new Color(.035f, .02f, .015f, .82f), true);
        ProgressionUI.Percent(overlay.rectTransform, 0, 0, 1, 1);
        var panel = Frame(canvas.transform, "Shop Panel", new Color(.24f, .135f, .07f), .035f, .045f, .965f, .95f);
        Frame(panel.transform, "Inset board", new Color(.32f, .21f, .115f), .012f, .015f, .988f, .982f);
        var banner = ProgressionUI.Image(panel.transform, "Store wooden heading", Color.white);
        banner.sprite = CartoonUI.Wood;
        ProgressionUI.Percent(banner.rectTransform, .018f, .845f, .42f, 1.035f);
        var heading = Label(panel.transform, "ARSENAL", 64, .07f, .885f, .37f, .989f);
        heading.color = ProgressionUI.Gold;
        heading.alignment = TextAnchor.MiddleCenter;
        var purse = Frame(panel.transform, "Wallet frame", new Color(.13f, .16f, .12f), .74f, .895f, .975f, .978f);
        Coin(purse.transform, .055f, .12f, .23f, .88f);
        wallet = Label(purse.transform, "", 42, .27f, .08f, .95f, .92f);
        var character = Frame(panel.transform, "Character", new Color(.18f, .24f, .19f), .025f, .185f, .285f, .86f);
        Frame(character.transform, "Portrait backdrop", new Color(.27f, .34f, .25f), .045f, .315f, .955f, .965f);
        bool marco = PlayerPrefs.GetInt("Lindavista.SelectedCharacter", 0) == 0;
        var portrait = ProgressionUI.Image(character.transform, "Portrait", Color.white);
        var characters = Resources.Load<ShopCharacterAssets>("ShopCharacterAssets");
        portrait.sprite = characters != null ? (marco ? characters.marco : characters.cesar) : null;
        portrait.preserveAspect = true;
        portrait.enabled = portrait.sprite != null;
        ProgressionUI.Percent(portrait.rectTransform, .09f, .33f, .91f, .95f);
        health = Label(character.transform, "", 34, .08f, .235f, .92f, .305f);
        var lifeMeter = Frame(character.transform, "Health meter", new Color(.10f, .14f, .105f), .07f, .175f, .93f, .233f);
        for (int j = 0; j < life.Length; j++)
        {
            life[j] = ProgressionUI.Image(lifeMeter.transform, "Health " + (j + 1), Color.white);
            ProgressionUI.Percent(life[j].rectTransform, .025f + j * .095f, .20f, .025f + j * .095f + .077f, .80f);
        }
        healthBuy = Buy(character.transform, "MEJORAR VIDA", () => Result(ProgressionService.TryUpgradeHealth()),
            .065f, .025f, .935f, .155f, true, true);
        var art = CharacterWeapons.Artwork(!marco);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float bottom = .65f - i * .23f;
            var row = Frame(panel.transform, CharacterWeapons.Name(i, !marco), new Color(.46f, .32f, .19f), .305f, bottom, .975f, bottom + .21f);
            var frame = Frame(row.transform, "Icon Frame", new Color(.83f, .71f, .47f), .018f, .105f, .19f, .905f);
            var icon = ProgressionUI.Image(frame.transform, "Weapon Icon", Color.white);
            icon.sprite = art != null ? art.IconAt(i) : ProgressionVisuals.Pistol;
            icon.preserveAspect = true;
            ProgressionUI.Percent(icon.rectTransform, .07f, .12f, .93f, .88f);
            Label(row.transform, CharacterWeapons.Name(i, !marco), 37, .215f, .735f, .72f, .975f);
            int tiers = WeaponDefinition.At(i).MaxUpgrade + 1;
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
            ammo[i] = Buy(row.transform, "MUNICIÓN", () => Result(ProgressionService.TryBuyAmmo(index)), .215f, .055f, .595f, .435f, true);
            upgrade[i] = Buy(row.transform, "MEJORAR ARMA", () => Result(ProgressionService.TryUpgradeWeapon(index)), .615f, .055f, .975f, .435f, true, true);
        }
        feedback = Label(panel.transform, "Cada compra de munición añade un paquete completo.", 27, .305f, .12f, .975f, .18f);
        Buy(panel.transform, "VOLVER", () => Destroy(gameObject), .025f, .025f, .25f, .115f);
#if UNITY_EDITOR
        Buy(panel.transform, "+1000 monedas · prueba", () => Result(ProgressionService.TryCredit(1000)), .735f, .025f, .975f, .095f);
#endif
        ProgressionService.Changed += Refresh;
        Refresh();
    }
    private void Refresh()
    {
        wallet.text = ProgressionService.Coins.ToString("N0");
        int hp = Mathf.Clamp(5 + ProgressionService.HealthUpgrade, 5, life.Length);
        health.text = "VIDA  " + hp + " / " + life.Length;
        for (int j = 0; j < life.Length; j++) life[j].color = j < hp ? new Color(.97f, .38f, .23f) : Muted;
        int hc = ShopBalance.HealthCost(ProgressionService.HealthUpgrade);
        PurchaseState(healthBuy, hc > 0 ? "MEJORAR VIDA +1" : "VIDA AL MÁXIMO", hc, true);
        for (int i = 0; i < 3; i++)
        {
            int level = ProgressionService.WeaponUpgrade(i), cost = WeaponDefinition.At(i).UpgradeCost(level);
            int amount = ProgressionService.Ammo(i);
            var definition = WeaponDefinition.ForCharacter(i, CharacterWeapons.SelectedCesar);
            stats[i].text = "Munición " + amount + "/" + ShopBalance.Capacity[i] + "  ·  Daño " + definition.DamageAt(level) +
                (definition.IsPiercing ? "  ·  Atraviesa " + definition.TargetsAt(level) : "");
            ammoFill[i].fillAmount = Mathf.Clamp01((float)amount / ShopBalance.Capacity[i]);
            bool fits = amount + ShopBalance.Pack[i] <= ShopBalance.Capacity[i];
            PurchaseState(ammo[i], fits ? "MUNICIÓN +" + ShopBalance.Pack[i] : "SIN ESPACIO", ShopBalance.AmmoCost[i], fits);
            PurchaseState(upgrade[i], cost > 0 ? "DAÑO +1" : "MEJORA MÁXIMA", cost, true);
            for (int j = 0; j < 6; j++) bars[i, j].color = j <= level ? ProgressionUI.Gold : Muted;
        }
        if (!ProgressionService.CanSave) feedback.text = ProgressionService.LastError;
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
