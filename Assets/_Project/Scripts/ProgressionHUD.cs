using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ProgressionHUD : MonoBehaviour
{
    public const float WalletWidth = 184f;
    private static ProgressionHUD active;
    private RectTransform safeArea;
    private Text wallet, notice;
    private Image coinIcon;
    private RectTransform walletPanel;
    private HUDSpriteVariants coinArtwork;
    private GameObject collection;
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
        walletPanel = ProgressionUI.Image(safeArea, "Wallet Panel",
            new Color(0.07f, 0.08f, 0.07f, 0.94f)).rectTransform;
        ProgressionUI.Top(walletPanel, new Vector2(0, 1),
            new Vector2(24, -PlayerHealthHUD.SecondaryTop), new Vector2(WalletWidth, 64));
        var artwork = Resources.Load<WeaponHUDAssets>("WeaponHUDAssets");
        coinArtwork = new HUDSpriteVariants(artwork != null && artwork.Coin != null ? artwork.Coin : ProgressionVisuals.Coin);
        coinIcon = ProgressionUI.Image(walletPanel, "Coin Icon", Color.white);
        coinIcon.preserveAspect = true;
        ProgressionUI.Top(coinIcon.rectTransform, new Vector2(0, 1), new Vector2(8, -8), new Vector2(48, 48));
        wallet = ProgressionUI.Text(walletPanel, "Coins", "", 40);
        Font displayFont = Resources.Load<Font>("UI/LilitaOne");
        if (displayFont != null)
        {
            wallet.font = displayFont;
            wallet.fontStyle = FontStyle.Normal;
        }
        wallet.color = ProgressionUI.Gold;
        ProgressionUI.Top(wallet.rectTransform, new Vector2(0, 1), new Vector2(68, -4), new Vector2(WalletWidth - 84f, 56));
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
        walletPanel.anchoredPosition = new Vector2(24, -PlayerHealthHUD.SecondaryTop);
        if (Screen.width > 0 && Screen.height > 0)
        {
            Rect area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
        if (Time.unscaledTime >= refreshAt)
        {
            Refresh();
            refreshAt = Time.unscaledTime + 0.25f;
        }
    }
    private void Refresh()
    {
        int coins = ProgressionService.Coins;
        wallet.text = coins.ToString();
        wallet.color = coins > 0 ? ProgressionUI.Gold : new Color(0.65f, 0.65f, 0.65f);
        coinIcon.sprite = coins > 0 ? coinArtwork.ColorSprite : coinArtwork.GraySprite;
        coinIcon.color = coins > 0 ? Color.white : new Color(0.65f, 0.65f, 0.65f);
        bool collecting = GameFlowController.IsCollectionPhase && !GameFlowController.IsMatchEnded;
        collection.SetActive(collecting);
        notice.text = ProgressionService.LastError ?? (collecting ? "¡Oleada eliminada! Recoge tus monedas antes de finalizar." : "");
    }
    private void OnDestroy()
    {
        ProgressionService.Changed -= Refresh;
        coinArtwork?.Dispose();
        if (active == this) active = null;
    }
}
