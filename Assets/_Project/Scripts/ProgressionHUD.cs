using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ProgressionHUD : MonoBehaviour
{
    private static ProgressionHUD active;
    private RectTransform safeArea;
    private Text wallet, weapon, magazine, notice;
    private GameObject collection;
    private PlayerWeaponController player;
    private float refreshAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if ((scene.name == "Gameplay" || scene.name == "Testing") && active == null)
            new GameObject("Progression HUD").AddComponent<ProgressionHUD>();
    }
    private void Awake()
    {
        active = this;
        var canvas = ProgressionUI.Canvas(transform, "Progression Canvas", 115);
        safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        safeArea.SetParent(canvas.transform, false);
        var panel = ProgressionUI.Image(safeArea, "Inventory Panel", ProgressionUI.PanelColor);
        ProgressionUI.Top(panel.rectTransform, Vector2.one, new Vector2(-24, -24), new Vector2(470, 168));
        var coinIcon = ProgressionUI.Image(panel.transform, "Coin Icon", Color.white);
        coinIcon.sprite = ProgressionVisuals.Coin;
        coinIcon.preserveAspect = true;
        ProgressionUI.Top(coinIcon.rectTransform, new Vector2(0, 1), new Vector2(16, -12), new Vector2(38, 38));
        wallet = ProgressionUI.Text(panel.transform, "Coins", "", 32);
        wallet.color = ProgressionUI.Gold;
        ProgressionUI.Top(wallet.rectTransform, new Vector2(0, 1), new Vector2(64, -10), new Vector2(390, 42));
        var pistol = ProgressionUI.Image(panel.transform, "Pistol Icon", Color.white);
        pistol.sprite = ProgressionVisuals.Pistol;
        pistol.preserveAspect = true;
        ProgressionUI.Top(pistol.rectTransform, new Vector2(0, 1), new Vector2(16, -62), new Vector2(52, 42));
        weapon = ProgressionUI.Text(panel.transform, "Owned Weapon", "", 28);
        ProgressionUI.Top(weapon.rectTransform, new Vector2(0, 1), new Vector2(78, -57), new Vector2(370, 42));
        magazine = ProgressionUI.Text(panel.transform, "Magazine", "", 25);
        ProgressionUI.Top(magazine.rectTransform, new Vector2(0, 1), new Vector2(20, -105), new Vector2(430, 42));
        var finish = ProgressionUI.Button(safeArea, "FINALIZAR NIVEL", GameFlowController.FinishLevel);
        ProgressionUI.Percent((RectTransform)finish.transform, 0.34f, 0.03f, 0.66f, 0.13f);
        collection = finish.gameObject;
        collection.SetActive(false);
        notice = ProgressionUI.Text(safeArea, "Notice", "", 30, TextAnchor.MiddleCenter);
        ProgressionUI.Percent(notice.rectTransform, 0.15f, 0.14f, 0.85f, 0.21f);
        ProgressionService.Changed += Refresh;
        Refresh();
    }
    private void Update()
    {
        if (Screen.width > 0 && Screen.height > 0)
        {
            Rect area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
        if (Time.unscaledTime >= refreshAt)
        {
            if (player == null || !player.gameObject.activeInHierarchy) BindPlayer();
            Refresh();
            refreshAt = Time.unscaledTime + 0.25f;
        }
    }
    private void BindPlayer()
    {
        if (player != null) player.Changed -= Refresh;
        player = FindFirstObjectByType<PlayerWeaponController>();
        if (player != null) player.Changed += Refresh;
    }
    private void Refresh()
    {
        wallet.text = "MONEDAS: " + ProgressionService.Coins;
        weapon.text = "PISTOLA · NIVEL " + (ProgressionService.PistolUpgrade + 1) +
            " · DAÑO " + WeaponDefinition.Pistol.DamageAt(ProgressionService.PistolUpgrade);
        magazine.text = player == null ? "" : player.IsReloading ? "RECARGANDO…" :
            "CARGADOR " + player.Magazine + "/" + player.Definition.MagazineSize + " · RESERVA ∞";
        bool collecting = GameFlowController.IsCollectionPhase && !GameFlowController.IsMatchEnded;
        collection.SetActive(collecting);
        notice.text = ProgressionService.LastError ?? (collecting ? "¡Oleada eliminada! Recoge tus monedas antes de finalizar." : "");
    }
    private void OnDestroy()
    {
        ProgressionService.Changed -= Refresh;
        if (player != null) player.Changed -= Refresh;
        if (active == this) active = null;
    }
}
