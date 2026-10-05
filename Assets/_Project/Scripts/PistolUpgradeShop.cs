using UnityEngine;
using UnityEngine.UI;
public sealed class PistolUpgradeShop : MonoBehaviour
{
    private static PistolUpgradeShop active;
    private Text wallet, health, feedback;
    private Button healthBuy;
    private readonly Image[] ammoFill = new Image[3];
    private readonly Text[] stats = new Text[3];
    private readonly Button[] ammo = new Button[3], upgrade = new Button[3];
    private readonly Image[,] bars = new Image[3,6];
    public static bool IsOpen => active != null;
    public static void Open() { if (active == null) new GameObject("Character and Weapons Shop").AddComponent<PistolUpgradeShop>(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = null; }
    private static Text Label(Transform p, string text, int size, float l, float b, float r, float t)
    {
        var v = ProgressionUI.Text(p, text, text, size); CartoonUI.Typography(v, size >= 34); ProgressionUI.Percent(v.rectTransform,l,b,r,t); return v;
    }
    private static Button Buy(Transform p, string text, UnityEngine.Events.UnityAction action, float l,float b,float r,float t)
    {
        var v = ProgressionUI.Button(p,text,action); CartoonUI.ButtonStyle(v);
        v.GetComponentInChildren<Text>().resizeTextMaxSize = 32;
        ProgressionUI.Percent((RectTransform)v.transform,l,b,r,t); return v;
    }
    private void Awake()
    {
        active = this;
        var canvas = ProgressionUI.Canvas(transform,"Shop Canvas",400);
        var overlay = ProgressionUI.Image(canvas.transform,"Overlay",new Color(0.025f,0.035f,0.03f,0.88f),true);
        ProgressionUI.Percent(overlay.rectTransform,0,0,1,1);
        var panel = ProgressionUI.Image(canvas.transform,"Shop Panel",new Color(0.22f,0.12f,0.055f));
        CartoonUI.Frame(panel,new Color(.23f,.17f,.11f));
        ProgressionUI.Percent(panel.rectTransform,0.04f,0.045f,0.96f,0.955f);
        var banner=ProgressionUI.Image(panel.transform,"Store wooden heading",Color.white);
        banner.sprite=CartoonUI.Wood;
        ProgressionUI.Percent(banner.rectTransform,.02f,.86f,.41f,1.01f);
        Label(panel.transform,"ARSENAL",58,0.07f,0.88f,0.38f,0.98f).color = ProgressionUI.Gold;
        var purse=ProgressionUI.Image(panel.transform,"Wallet frame",Color.white); CartoonUI.Frame(purse,new Color(.15f,.17f,.12f));
        ProgressionUI.Percent(purse.rectTransform,.72f,.90f,.975f,.976f);
        var coin=ProgressionUI.Image(purse.transform,"Coin",Color.white); coin.sprite=ProgressionVisuals.Coin; coin.preserveAspect=true;
        ProgressionUI.Percent(coin.rectTransform,.05f,.18f,.19f,.82f);
        wallet = Label(purse.transform,"",34,.23f,.12f,.93f,.88f);
        var character = ProgressionUI.Image(panel.transform,"Character",new Color(0.12f,0.17f,0.15f));
        CartoonUI.Frame(character,new Color(.21f,.28f,.24f));
        ProgressionUI.Percent(character.rectTransform,0.025f,0.18f,0.285f,0.86f);
        var namePlate=ProgressionUI.Image(character.transform,"Character nameplate",Color.white); CartoonUI.Frame(namePlate,new Color(.13f,.18f,.15f));
        ProgressionUI.Percent(namePlate.rectTransform,.05f,.86f,.95f,.97f);
        bool marco = PlayerPrefs.GetInt("Lindavista.SelectedCharacter",0) == 0;
        Label(character.transform,marco ? "MARCO" : "CÉSAR",38,0.1f,0.86f,0.90f,0.97f);
        var portrait = ProgressionUI.Image(character.transform,"Portrait",Color.white);
        var characters = Resources.Load<ShopCharacterAssets>("ShopCharacterAssets");
        portrait.sprite = characters != null ? (marco ? characters.marco : characters.cesar) : null; portrait.preserveAspect = true; portrait.enabled = portrait.sprite != null;
        ProgressionUI.Percent(portrait.rectTransform,0.06f,0.29f,0.94f,0.85f);
        health = Label(character.transform,"",30,0.08f,0.16f,0.92f,0.28f);
        healthBuy = Buy(character.transform,"Mejorar vida",()=>Result(ProgressionService.TryUpgradeHealth()),0.07f,0.03f,0.93f,0.15f);
        var art = Resources.Load<WeaponHUDAssets>("WeaponHUDAssets");
        for(int i=0;i<3;i++)
        {
            int index=i; float bottom=0.65f-i*0.23f;
            var row=ProgressionUI.Image(panel.transform,ShopBalance.Names[i],new Color(.34f,.26f,.17f));
            CartoonUI.Frame(row,new Color(.37f,.28f,.18f));
            ProgressionUI.Percent(row.rectTransform,0.305f,bottom,0.975f,bottom+0.21f);
            var frame=ProgressionUI.Image(row.transform,"Icon Frame",new Color(.79f,.71f,.52f));
            CartoonUI.Frame(frame,new Color(.79f,.71f,.52f));
            ProgressionUI.Percent(frame.rectTransform,0.015f,0.10f,0.19f,0.90f);
            var icon=ProgressionUI.Image(frame.transform,"Weapon Icon",Color.white); icon.sprite=art != null ? art.IconAt(i) : ProgressionVisuals.Pistol; icon.preserveAspect=true;
            ProgressionUI.Percent(icon.rectTransform,0.05f,0.1f,0.95f,0.9f);
            Label(row.transform,ShopBalance.Names[i],34,0.21f,0.73f,0.70f,0.98f);
            int tiers=WeaponDefinition.At(i).MaxUpgrade+1;
            for(int j=0;j<6;j++) { bars[i,j]=ProgressionUI.Image(row.transform,"Level "+j,Color.gray); bars[i,j].sprite=CartoonUI.FrameSprite; bars[i,j].type=Image.Type.Sliced;
                float step=.25f/tiers; ProgressionUI.Percent(bars[i,j].rectTransform,.72f+j*step,.80f,.72f+(j+1)*step-.006f,.91f); bars[i,j].gameObject.SetActive(j<tiers); }
            var meter=ProgressionUI.Image(row.transform,"Ammo meter",new Color(.12f,.13f,.10f)); meter.sprite=CartoonUI.FrameSprite; meter.type=Image.Type.Sliced;
            ProgressionUI.Percent(meter.rectTransform,.22f,.49f,.975f,.55f);
            ammoFill[i]=ProgressionUI.Image(meter.transform,"Ammo fill",new Color(.67f,.77f,.36f));
            ProgressionUI.Percent(ammoFill[i].rectTransform,.01f,.20f,.99f,.80f);
            stats[i]=Label(row.transform,"",28,0.22f,0.56f,0.98f,0.74f);
            ammo[i]=Buy(row.transform,"Comprar munición",()=>Result(ProgressionService.TryBuyAmmo(index)),0.21f,0.05f,0.60f,0.46f);
            upgrade[i]=Buy(row.transform,"Mejorar",()=>Result(ProgressionService.TryUpgradeWeapon(index)),0.62f,0.05f,0.98f,0.46f);
        }
        feedback=Label(panel.transform,"Cada compra incluye un cargador completo.",26,0.03f,0.11f,0.97f,0.18f);
        Buy(panel.transform,"VOLVER",()=>Destroy(gameObject),0.025f,0.025f,0.25f,0.105f);
#if UNITY_EDITOR
        Buy(panel.transform,"+1000 monedas · prueba",()=>Result(ProgressionService.TryCredit(1000)),0.735f,0.025f,0.975f,0.095f);
#endif
        ProgressionService.Changed += Refresh; Refresh();
    }
    private void Refresh()
    {
        wallet.text=ProgressionService.Coins.ToString("N0");
        health.text="VIDA  "+(5+ProgressionService.HealthUpgrade)+" / 10\nNivel "+(ProgressionService.HealthUpgrade+1);
        int hc=ShopBalance.HealthCost(ProgressionService.HealthUpgrade);
        healthBuy.GetComponentInChildren<Text>().text=hc>0 ? "MEJORAR VIDA\n"+hc+" MONEDAS" : "VIDA AL MÁXIMO";
        healthBuy.interactable=hc>0 && ProgressionService.Coins>=hc && ProgressionService.CanSave;
        for(int i=0;i<3;i++)
        {
            int level=ProgressionService.WeaponUpgrade(i), cost=WeaponDefinition.At(i).UpgradeCost(level);
            stats[i].text="Munición "+ProgressionService.Ammo(i)+"/"+ShopBalance.Capacity[i]+"  ·  Daño "+WeaponDefinition.At(i).DamageAt(level);
            ProgressionUI.Percent(ammoFill[i].rectTransform,.01f,.20f,.01f+.98f*ProgressionService.Ammo(i)/ShopBalance.Capacity[i],.80f);
            bool fits=ProgressionService.Ammo(i)+ShopBalance.Pack[i]<=ShopBalance.Capacity[i];
            ammo[i].GetComponentInChildren<Text>().text=fits ? "CARGADOR +"+ShopBalance.Pack[i]+"\n"+ShopBalance.AmmoCost[i]+" MONEDAS" : "SIN ESPACIO\nCargador completo";
            ammo[i].interactable=fits && ProgressionService.Coins>=ShopBalance.AmmoCost[i] && ProgressionService.CanSave;
            upgrade[i].GetComponentInChildren<Text>().text=cost>0 ? "DAÑO +1\n"+cost+" MONEDAS" : "MEJORA MÁXIMA";
            upgrade[i].interactable=cost>0 && ProgressionService.Coins>=cost && ProgressionService.CanSave;
            for(int j=0;j<6;j++) bars[i,j].color=j<=level ? ProgressionUI.Gold : Color.gray;
        }
        if(!ProgressionService.CanSave) feedback.text=ProgressionService.LastError;
    }
    private void Result(bool success) { feedback.text=success ? "¡Compra completada y guardada!" : ProgressionService.LastError ?? "No se pudo completar la compra."; }
    private void OnDestroy() { ProgressionService.Changed-=Refresh; if(active==this) active=null; }
}
