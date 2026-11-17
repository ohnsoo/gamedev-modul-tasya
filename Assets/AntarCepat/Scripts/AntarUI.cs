using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pembuat UI gaya kartun (dipakai Main Menu). Sama dengan gaya HUD di AntarGame:
/// panel krem, garis tepi gelap, bayangan, kilap, font bulat.
/// </summary>
public static class AntarUI
{
    public static readonly Color GARIS = new Color32(0x2B, 0x1D, 0x3A, 255);
    public static readonly Color KRIM = new Color32(0xFF, 0xF7, 0xE6, 255);
    public static readonly Color KUNING = new Color32(0xFF, 0xC9, 0x2E, 255);
    public static readonly Color HIJAU = new Color32(0x2E, 0xC4, 0x6A, 255);
    public static readonly Color ORANYE = new Color32(0xFF, 0x8A, 0x2B, 255);
    public static readonly Color BIRU = new Color32(0x3F, 0xA9, 0xF5, 255);
    public static readonly Color MERAH = new Color32(0xFF, 0x5B, 0x5B, 255);
    public static readonly Color TEKS = new Color32(0x3A, 0x2A, 0x4A, 255);
    const float SUDUT = 44f;

    static Sprite spBulat, spLingkaran;
    public static TMP_FontAsset fontJudul, fontIsi;

    public static Sprite Bulat { get { if (spBulat == null) spBulat = SpriteBulat(128, (int)SUDUT, true); return spBulat; } }
    public static Sprite Lingkaran { get { if (spLingkaran == null) spLingkaran = SpriteBulat(256, 128, false); return spLingkaran; } }

    public static Transform BuatKanvas(string nama, int urutan)
    {
        var go = new GameObject(nama, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = urutan;
        var s = go.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = 0.5f;
        return go.transform;
    }

    public static RectTransform Wadah(Transform induk, string nama, Vector2 jangkar, Vector2 pos, Vector2 ukuran)
    {
        var g = new GameObject(nama, typeof(RectTransform));
        var r = g.GetComponent<RectTransform>(); r.SetParent(induk, false);
        r.anchorMin = r.anchorMax = r.pivot = jangkar;
        r.anchoredPosition = pos; r.sizeDelta = ukuran;
        return r;
    }

    public static RectTransform Panel(Transform induk, string nama, Vector2 jangkar, Vector2 pos, Vector2 ukuran, Color isi,
                                      float radius = 30f, float tebal = 6f, bool lingkaran = false)
    {
        var w = Wadah(induk, nama, jangkar, pos, ukuran);
        var s = lingkaran ? Lingkaran : Bulat;
        float mLuar = lingkaran ? 1f : SUDUT / Mathf.Max(radius, 4f);
        float mDalam = lingkaran ? 1f : SUDUT / Mathf.Max(radius - tebal, 4f);
        Lapis(w, "Bayangan", s, new Color(0.1f, 0.04f, 0.15f, 0.3f), new Vector2(0, -9), 0f, mLuar);
        Lapis(w, "Garis", s, GARIS, Vector2.zero, 0f, mLuar);
        Lapis(w, "Isi", s, isi, Vector2.zero, tebal, mDalam);
        var kilap = Lapis(w, "Kilap", s, new Color(1f, 1f, 1f, 0.28f), Vector2.zero, tebal + 5f, mDalam);
        kilap.rectTransform.anchorMin = new Vector2(0f, 0.55f);
        return w;
    }

    public static Image Lapis(RectTransform induk, string nama, Sprite s, Color c, Vector2 geser, float sisip, float pengali)
    {
        var g = new GameObject(nama, typeof(RectTransform), typeof(Image));
        var r = g.GetComponent<RectTransform>(); r.SetParent(induk, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(sisip, sisip) + geser; r.offsetMax = new Vector2(-sisip, -sisip) + geser;
        var img = g.GetComponent<Image>(); img.sprite = s; img.color = c; img.raycastTarget = false;
        img.type = s == spBulat ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = pengali;
        return img;
    }

    public static TextMeshProUGUI Teks(RectTransform induk, string isi, float ukuran, Color warna, TextAlignmentOptions rata,
                                       float tepi = 0f, bool judul = false, float garis = 0f)
    {
        var g = new GameObject("Teks", typeof(RectTransform), typeof(TextMeshProUGUI));
        var r = g.GetComponent<RectTransform>(); r.SetParent(induk, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(tepi, 2); r.offsetMax = new Vector2(-tepi, -2);
        var t = g.GetComponent<TextMeshProUGUI>();
        var f = judul ? fontJudul : fontIsi;
        if (f != null) t.font = f;
        t.text = isi; t.fontSize = ukuran; t.color = warna; t.alignment = rata; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        if (garis > 0f)
        {
            var m = t.fontMaterial;
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, garis);
            m.SetColor(ShaderUtilities.ID_OutlineColor, GARIS);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, GARIS);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.8f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, garis * 2f);
        }
        return t;
    }

    /// <summary>Tombol kapsul kartun dengan efek tekan.</summary>
    public static Button Tombol(Transform induk, string label, Vector2 jangkar, Vector2 pos, Vector2 ukuran, Color warna,
                                UnityEngine.Events.UnityAction klik, float ukuranTeks = 44f)
    {
        var r = Panel(induk, "Tombol " + label, jangkar, pos, ukuran, warna, ukuran.y * 0.5f, 7f);
        r.Find("Garis").GetComponent<Image>().raycastTarget = true;
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = r.Find("Isi").GetComponent<Image>();
        var cb = b.colors; cb.highlightedColor = new Color(1f, 1f, 0.92f); cb.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        cb.selectedColor = Color.white; b.colors = cb;
        b.onClick.AddListener(klik);
        r.gameObject.AddComponent<AntarTombolKenyal>();
        Teks(r, label, ukuranTeks, Color.white, TextAlignmentOptions.Center, 10f, true, 0.28f);
        return b;
    }

    static Sprite SpriteBulat(int n, int r, bool iris)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float px0 = x + 0.5f, py0 = y + 0.5f;
                float cx = Mathf.Clamp(px0, r, n - r), cy = Mathf.Clamp(py0, r, n - r);
                float d = Mathf.Sqrt((px0 - cx) * (px0 - cx) + (py0 - cy) * (py0 - cy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        var tepi = iris ? new Vector4(r + 2, r + 2, r + 2, r + 2) : Vector4.zero;
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, tepi);
    }
}

/// <summary>Efek tombol kenyal: membesar saat disorot, mengecil saat ditekan.</summary>
public class AntarTombolKenyal : MonoBehaviour,
    UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
{
    float target = 1f;
    bool sorot;
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { sorot = true; target = 1.06f; }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { sorot = false; target = 1f; }
    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { target = 0.92f; }
    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { target = sorot ? 1.06f : 1f; }
    void Update()
    {
        float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(s, s, 1f);
    }
}
