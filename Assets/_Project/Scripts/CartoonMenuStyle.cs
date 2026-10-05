using UnityEngine;
using UnityEngine.UI;

public static class CartoonMenuStyle
{
    private static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
        rect.anchoredPosition=position; rect.sizeDelta=size;
    }
    public static void Apply(Transform canvas)
    {
        string[] names={ "Play Button", "Store Button", "Settings Button" };
        string[] labels={ "JUGAR", "TIENDA", "AJUSTES" };
        for(int i=0;i<3;i++)
        {
            var target=canvas.Find(names[i]); if(target==null) continue;
            var image=target.GetComponent<Image>(); image.sprite=CartoonUI.Wood; image.preserveAspect=false;
            Center((RectTransform)target,new Vector2(190,95-i*185),new Vector2(510,180));
            var label=target.Find("Unified Label");
            var text=label != null ? label.GetComponent<Text>() : ProgressionUI.Text(target,"Unified Label",labels[i],66,TextAnchor.MiddleCenter);
            text.text=labels[i]; text.fontSize=66; text.resizeTextForBestFit=false;
            ProgressionUI.Percent(text.rectTransform,.13f,.22f,.87f,.80f); CartoonUI.Typography(text,true);
            text.GetComponent<Outline>().effectDistance=new Vector2(4,-4);
        }
        var title=canvas.Find("Game Title"); var subtitle=canvas.Find("Game Subtitle");
        if(title==null || subtitle==null) return;
        var old=canvas.Find("Title Backplate");
        var plate=old != null ? old.GetComponent<Image>() : ProgressionUI.Image(canvas,"Title Backplate",Color.white);
        plate.sprite=CartoonUI.Wood; plate.color=new Color(.45f,.34f,.24f); plate.preserveAspect=false;
        Center(plate.rectTransform,new Vector2(190,345),new Vector2(1080,290));
        plate.transform.SetSiblingIndex(1);
        var t=title.GetComponent<Text>(); t.text="LINDAVISTA"; t.fontSize=122; CartoonUI.Typography(t,true); t.color=new Color(1,.85f,.35f); t.GetComponent<Outline>().effectDistance=new Vector2(5,-5);
        Center(t.rectTransform,new Vector2(190,382),new Vector2(880,135));
        var s=subtitle.GetComponent<Text>(); s.text="UNDER ATTACK"; s.fontSize=64; CartoonUI.Typography(s,true); s.color=new Color(1,.37f,.17f);
        Center(s.rectTransform,new Vector2(190,292),new Vector2(790,85));
    }
}
