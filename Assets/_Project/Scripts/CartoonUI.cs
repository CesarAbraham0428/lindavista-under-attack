using UnityEngine;
using UnityEngine.UI;

public static class CartoonUI
{
    public static readonly Color Cream = new Color(1f, .94f, .76f);
    public static readonly Color Ink = new Color(.10f, .065f, .045f);
    private static Sprite rounded;
    public static Font Font => Resources.Load<Font>("UI/LilitaOne");
    public static Sprite Wood => Resources.Load<Sprite>("UI/WoodSign");
    public static Sprite FrameSprite
    {
        get
        {
            if (rounded != null) return rounded;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(12 - x, x - 51), dy = Mathf.Max(12 - y, y - 51);
                float d = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
                pixels[y * size + x] = d > 12 ? Color.clear : d > 8 || x < 4 || x > 59 || y < 4 || y > 59 ? new Color(.22f,.18f,.12f) : Color.white;
            }
            texture.SetPixels(pixels); texture.Apply(); texture.filterMode = FilterMode.Bilinear;
            rounded = Sprite.Create(texture, new Rect(0,0,size,size), Vector2.one*.5f, 100, 0, SpriteMeshType.FullRect, Vector4.one*16);
            rounded.name = "Cartoon rounded frame";
            return rounded;
        }
    }
    public static void Frame(Image image, Color color)
    {
        image.sprite = FrameSprite; image.type = Image.Type.Sliced; image.color = color;
        var shadow = image.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0,0,0,.5f); shadow.effectDistance = new Vector2(0,-6);
    }
    public static void Typography(Text text, bool heading = false)
    {
        if (Font != null) { text.font = Font; text.fontStyle = FontStyle.Normal; }
        text.color = Cream;
        if (heading)
        {
            var edge = text.gameObject.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            edge.effectColor = Ink; edge.effectDistance = new Vector2(2,-2);
        }
    }
    public static void ButtonStyle(Button button)
    {
        Frame(button.image, new Color(.38f,.53f,.19f));
        button.colors = new ColorBlock { normalColor=Color.white, highlightedColor=new Color(1.12f,1.12f,.95f), pressedColor=new Color(.76f,.82f,.63f), selectedColor=Color.white, disabledColor=new Color(.55f,.53f,.45f,.9f), colorMultiplier=1, fadeDuration=.12f };
        Typography(button.GetComponentInChildren<Text>(), true);
    }
}
