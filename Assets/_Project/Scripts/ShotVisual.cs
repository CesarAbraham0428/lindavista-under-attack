using UnityEngine;

/// <summary>Weapon-specific flight and transient effects, independent of damage and ammo.</summary>
public sealed class ShotVisual : MonoBehaviour
{
    public const float RocketWidth = 1.96f;
    private static readonly Sprite[] sprites = new Sprite[4];
    private static bool atlasPreparado;
    private static Sprite softDot;
    private int weapon;
    private float nextSmoke;
    private bool enemyFlight, impactAtEnd;
    private Vector2 flightDirection;
    private float travelRemaining;
    private static readonly SceneCombatPool<ShotVisual> enemyShots = new("Enemy shot visual", 64);
    private TrailRenderer trail;
    private bool pooledEnemy, retired;
    private static int activeEnemyShots;

    public static ShotVisual Attach(GameObject shot, int index)
    {
        if (!shot.TryGetComponent<ShotVisual>(out var visual)) visual = shot.AddComponent<ShotVisual>();
        visual.weapon = Mathf.Clamp(index, 0, 2);
        visual.enemyFlight = false;
        visual.pooledEnemy = false;
        visual.retired = false;
        visual.nextSmoke = 0f;
        visual.enabled = true;
        if (!shot.TryGetComponent<SpriteRenderer>(out var renderer)) renderer = shot.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSprite(visual.weapon);
        renderer.color = Color.white;
        renderer.sortingOrder = 30;
        // Keep physical collision radius separate from the size of the art.
        float width = visual.weapon == 2 ? RocketWidth : visual.weapon == 1 ? 0.24f : 0.2f;
        shot.transform.localScale = Vector3.one * width / renderer.sprite.bounds.size.x;
        if (visual.weapon != 2)
        {
            if (visual.trail == null) visual.trail = shot.GetComponent<TrailRenderer>();
            if (visual.trail == null) visual.trail = shot.AddComponent<TrailRenderer>();
            var trail = visual.trail;
            trail.Clear();
            trail.emitting = true;
            trail.sharedMaterial = renderer.sharedMaterial;
            trail.time = visual.weapon == 1 ? 0.035f : 0.025f;
            trail.startWidth = 0.025f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.06f;
            trail.startColor = new Color(1f, 0.76f, 0.4f, 0.55f);
            trail.endColor = new Color(1f, 0.5f, 0.2f, 0f);
            trail.sortingOrder = 29;
        }
        return visual;
    }

    public static void EnemyShot(Vector2 origin, Vector2 direction, float distance, int weapon, bool hit)
    {
        if (activeEnemyShots >= 64 || GameFlowController.IsMatchEnded) return;
        var visual = enemyShots.Rent();
        var shot = visual.gameObject;
        shot.transform.position = origin;
        shot.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        Attach(shot, weapon);
        visual.enabled = true;
        visual.pooledEnemy = true;
        visual.enemyFlight = true;
        visual.flightDirection = direction;
        visual.travelRemaining = Mathf.Max(0f, distance);
        visual.impactAtEnd = hit;
        activeEnemyShots++;
        shot.SetActive(true);
        Muzzle(origin, direction, weapon);
    }

    private void Update()
    {
        if (retired) return;
        if (GameFlowController.IsMatchEnded)
        {
            if (pooledEnemy) RetireEnemy();
            else GetComponent<CombatProjectile>()?.Cancel();
            return;
        }
        if (enemyFlight)
        {
            float step = Mathf.Min(32f * Time.deltaTime, travelRemaining);
            transform.position += (Vector3)(flightDirection * step);
            travelRemaining -= step;
            if (travelRemaining <= 0f)
            {
                if (impactAtEnd) Impact(transform.position, flightDirection, weapon);
                RetireEnemy();
                return;
            }
        }
        if (weapon != 2 || Time.time < nextSmoke) return;
        nextSmoke = Time.time + 0.025f;
        // Rocket pivot is at its tail, so art and exhaust start at the barrel.
        Vector2 rear = (Vector2)transform.position + (Vector2)transform.right * 0.05f;
        Puff(rear, -(Vector2)transform.right * 0.35f + Vector2.up * 0.12f,
            new Color(0.55f, 0.54f, 0.5f, 0.45f), 0.13f, 0.38f, 0.4f);
        Puff(rear, -(Vector2)transform.right * 0.2f,
            new Color(1f, 0.5f, 0.12f, 0.85f), 0.12f, 0.025f, 0.07f);
    }

    private void RetireEnemy()
    {
        retired = true;
        if (pooledEnemy) enemyShots.Return(this);
        // Player projectile lifetime is owned exclusively by CombatProjectile.
    }

    private void OnDisable()
    {
        if (trail != null) { trail.emitting = false; trail.Clear(); }
        if (!pooledEnemy) return;
        pooledEnemy = false;
        activeEnemyShots = Mathf.Max(0, activeEnemyShots - 1);
    }

    internal static void Prewarm()
    {
        GetSprite(0);
        GetSoftDot();
        enemyShots.Prewarm(6, effect => Attach(effect.gameObject, 0));
    }

    public static void Muzzle(Vector2 position, Vector2 direction, int weapon)
    {
        float length = weapon == 2 ? 0.5f : weapon == 1 ? 0.28f : 0.34f;
        var effect = ShotEffect.Create("Muzzle flash", GetSprite(3), position, Vector2.zero,
            Color.white, length, length * 0.5f, weapon == 2 ? 0.11f : 0.065f);
        if (effect != null)
            effect.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        Puff(position, Vector2.up * 0.25f, new Color(0.6f, 0.57f, 0.51f, 0.2f), 0.09f, 0.22f, 0.2f);
    }

    public static void Impact(Vector2 position, Vector2 direction, int weapon)
    {
        if (weapon == 2)
        {
            RocketImpact.Create(position);
            return;
        }
        Puff(position, Vector2.zero, new Color(1f, 0.9f, 0.65f), 0.13f, 0.025f, 0.065f);
        for (int i = 0; i < 4; i++)
        {
            Vector2 spark = Quaternion.Euler(0, 0, Random.Range(-65f, 65f)) * -direction;
            Puff(position, spark * Random.Range(1f, 2.5f), new Color(1f, 0.65f, 0.22f), 0.045f, 0.01f, 0.14f);
        }
        Puff(position, Vector2.up * 0.3f, new Color(0.6f, 0.58f, 0.55f, 0.3f), 0.1f, 0.25f, 0.24f);
    }

    /// <summary>Readable contact flash, not an explosion and never an extra damage event.</summary>
    public static void TargetImpact(Vector2 position, Vector2 direction, int weapon, bool blocked)
    {
        Impact(position, direction, weapon);
        float size = blocked ? 0.72f : weapon == 1 ? 0.85f : 0.62f;
        Color tint = blocked ? new Color(0.62f, 0.86f, 1f) : new Color(1f, 0.84f, 0.48f);
        var flash = ShotEffect.Create(blocked ? "Shield contact flash" : "Enemy contact flash",
            GetSprite(3), position + direction * 0.12f, Vector2.zero,
            tint, size, size * 0.65f, 0.18f, 40);
        if (flash != null)
            flash.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(-direction.y, -direction.x) * Mathf.Rad2Deg);
    }

    private static void Puff(Vector2 position, Vector2 velocity, Color color, float size, float endSize, float lifetime)
    {
        ShotEffect.Create("Shot particle", GetSoftDot(), position, velocity, color, size, endSize, lifetime,
            color.r < 0.8f ? 28 : 31);
    }

    private static Sprite GetSoftDot()
    {
        if (softDot == null)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                    pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f));
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            softDot = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32);
        }
        return softDot;
    }

    public static Sprite GetSprite(int index)
    {
        index = Mathf.Clamp(index, 0, sprites.Length - 1);
        PrepararAtlas();
        return sprites[index] != null ? sprites[index] : ProgressionVisuals.Projectile;
    }

    private static void PrepararAtlas()
    {
        if (atlasPreparado) return;
        atlasPreparado = true;
        var texture = Resources.Load<Texture2D>("ShotProjectileAtlas");
        if (texture == null || !texture.isReadable) return;
        // Una sola copia temporal de píxeles sirve para recortar los cuatro efectos.
        var pixels = texture.GetPixels32();
        for (int indice = 0; indice < sprites.Length; indice++)
            RecortarSprite(texture, pixels, indice);
    }

    private static void RecortarSprite(Texture2D texture, Color32[] pixels, int index)
    {
        // The rocket is wider than half the generated atlas; regions keep it intact.
        Rect region = index == 0 ? new Rect(0, .5f, .5f, .5f) : index == 1 ? new Rect(.5f, .5f, .5f, .5f) :
            index == 2 ? new Rect(0, 0, .57f, .5f) : new Rect(.58f, 0, .42f, .5f);
        int left = (int)(region.xMin * texture.width), right = (int)(region.xMax * texture.width);
        int bottom = (int)(region.yMin * texture.height), top = (int)(region.yMax * texture.height);
        int minX = right, minY = top, maxX = left, maxY = bottom;
        for (int y = bottom; y < top; y++)
            for (int x = left; x < right; x++)
                if (pixels[y * texture.width + x].a > 24)
                {
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
        if (maxX <= minX || maxY <= minY) return;
        sprites[index] = Sprite.Create(texture, new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
            new Vector2(0, .5f), 256f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RevisarAtlasAlIniciar()
    {
        activeEnemyShots = 0;
        atlasPreparado = true;
        // Al entrar en Play sin recargar el dominio pueden persistir referencias destruidas.
        for (int indice = 0; indice < sprites.Length; indice++)
            if (sprites[indice] == null) { atlasPreparado = false; break; }
    }
}


