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
    private float ultimaFila = -1f;
    private int ultimasMonedas = -1;
    private string ultimoAviso;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Gameplay" && active == null)
            new GameObject("Progression HUD").AddComponent<ProgressionHUD>();
    }
    private void Awake()
    {
        active = this;
        var canvas = ProgressionUI.Canvas(transform, "Progression Canvas", 115);
        safeArea = AreaSeguraUI.Crear(canvas.transform, "Safe Area");
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
        CartoonUI.ButtonStyle(finish);
        finish.image.sprite = CartoonUI.Wood;
        finish.image.type = Image.Type.Simple;
        finish.image.color = Color.white;
        ProgressionUI.Top((RectTransform)finish.transform, new Vector2(.5f, 1f),
            new Vector2(0f, -PlayerHealthHUD.SecondaryTop + 12f), new Vector2(576f, 120f));
        var finishLabel = finish.GetComponentInChildren<Text>();
        finishLabel.fontSize = finishLabel.resizeTextMaxSize = 34;
        ProgressionUI.Percent(finishLabel.rectTransform, .12f, .18f, .88f, .82f);
        collection = finish.gameObject;
        collection.SetActive(false);
        notice = ProgressionUI.Text(safeArea, "Notice", "", 32, TextAnchor.MiddleCenter);
        CartoonUI.Typography(notice, true);
        notice.GetComponent<Outline>().effectColor = Color.black;
        ProgressionUI.Top(notice.rectTransform, new Vector2(.5f, 0f),
            new Vector2(0f, 156f), new Vector2(900f, 88f));
        ProgressionService.Changed += Refresh;
        Refresh();
    }
    private void Update()
    {
        if (ultimaFila != PlayerHealthHUD.SecondaryTop)
        {
            ultimaFila = PlayerHealthHUD.SecondaryTop;
            walletPanel.anchoredPosition = new Vector2(24, -ultimaFila);
            ((RectTransform)collection.transform).anchoredPosition = new Vector2(0f, -ultimaFila + 12f);
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
        if (ultimasMonedas != coins)
        {
            ultimasMonedas = coins;
            wallet.text = coins.ToString();
            wallet.color = coins > 0 ? ProgressionUI.Gold : new Color(0.65f, 0.65f, 0.65f);
            coinIcon.sprite = coins > 0 ? coinArtwork.ColorSprite : coinArtwork.GraySprite;
            coinIcon.color = coins > 0 ? Color.white : new Color(0.65f, 0.65f, 0.65f);
        }
        bool collecting = GameFlowController.IsCollectionPhase && !GameFlowController.IsMatchEnded;
        if (collection.activeSelf != collecting) collection.SetActive(collecting);
        string aviso = ProgressionService.LastError ?? (collecting ? "¡Oleada eliminada!\nRecoge tu botín antes de finalizar." : "");
        if (ultimoAviso != aviso) { ultimoAviso = aviso; notice.text = aviso; }
    }
    private void OnDestroy()
    {
        ProgressionService.Changed -= Refresh;
        coinArtwork?.Dispose();
        if (active == this) active = null;
    }
}
