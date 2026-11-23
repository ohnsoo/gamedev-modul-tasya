using UnityEngine;

/// <summary>
/// Lampu lalu lintas satu persimpangan: dua fase (Utara-Selatan dan Timur-Barat) bergantian,
/// dengan kuning dan jeda semua-merah. Juga mengatur lampu penyeberang di zebra cross persimpangan ini.
/// </summary>
public class KotaLampu : MonoBehaviour
{
    public enum Warna { Hijau, Kuning, Merah }

    public float hijauUS = 11f, hijauTB = 11f;
    public float kuning = 2.5f, semuaMerah = 1.5f;
    [Tooltip("Penyeberang tidak boleh mulai menyeberang bila sisa hijau penyeberang kurang dari ini (detik)")]
    public float minimalMulaiSeberang = 6f;
    public float offsetAwal;

    [Header("Kepala lampu mobil (per lengan): sumbu U-S atau T-B")]
    public Renderer[] usMerah, usKuning, usHijau;
    public Renderer[] tbMerah, tbKuning, tbHijau;
    [Header("Lampu penyeberang: yang menyeberangi jalan U-S / T-B")]
    public Renderer[] pUSMerah, pUSHijau;     // penyeberang yang memotong jalan arah U-S
    public Renderer[] pTBMerah, pTBHijau;

    public Material bahanMati, bahanMerah, bahanKuning, bahanHijau;

    // 0 = U-S hijau, 1 = U-S kuning, 2 = semua merah, 3 = T-B hijau, 4 = T-B kuning, 5 = semua merah
    int fase = -1;
    float timer;
    bool kedipNyala = true;

    void Start()
    {
        float total = hijauUS + hijauTB + 2f * (kuning + semuaMerah);
        float t = Mathf.Repeat(offsetAwal, total);
        fase = 0;
        while (t > Durasi(fase)) { t -= Durasi(fase); fase = (fase + 1) % 6; }
        timer = t;
        Terapkan();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= Durasi(fase))
        {
            timer -= Durasi(fase);
            fase = (fase + 1) % 6;
            Terapkan();
        }
        // lampu hijau penyeberang berkedip menjelang habis
        bool kedip = Mathf.Repeat(Time.time, 0.5f) < 0.3f;
        if (kedip != kedipNyala) { kedipNyala = kedip; TerapkanPenyeberang(); }
    }

    float Durasi(int f)
    {
        switch (f)
        {
            case 0: return hijauUS;
            case 3: return hijauTB;
            case 1: case 4: return kuning;
            default: return semuaMerah;
        }
    }

    public float SisaFase { get { return Durasi(fase) - timer; } }

    static bool SumbuUS(Vector3 arah) { return Mathf.Abs(arah.z) >= Mathf.Abs(arah.x); }

    /// <summary>Warna lampu untuk mobil yang melaju ke arah 'arah'.</summary>
    public Warna Mobil(Vector3 arah)
    {
        int hijau = SumbuUS(arah) ? 0 : 3;
        if (fase == hijau) return Warna.Hijau;
        if (fase == hijau + 1) return Warna.Kuning;
        return Warna.Merah;
    }

    /// <summary>
    /// Lampu penyeberang untuk zebra yang memotong jalan dengan sumbu 'sumbuJalan'.
    /// Hijau saat kendaraan di jalan itu berhenti (arah lain sedang hijau); Kuning = hijau berkedip (jangan mulai menyeberang).
    /// </summary>
    public Warna Penyeberang(Vector3 sumbuJalan)
    {
        int hijauLain = SumbuUS(sumbuJalan) ? 3 : 0;
        if (fase == hijauLain) return SisaFase > minimalMulaiSeberang ? Warna.Hijau : Warna.Kuning;
        if (fase == hijauLain + 1) return Warna.Kuning;
        return Warna.Merah;
    }

    void Terapkan()
    {
        Set(usMerah, usKuning, usHijau, Mobil(Vector3.forward));
        Set(tbMerah, tbKuning, tbHijau, Mobil(Vector3.right));
        TerapkanPenyeberang();
    }

    void TerapkanPenyeberang()
    {
        SetP(pUSMerah, pUSHijau, Penyeberang(Vector3.forward));
        SetP(pTBMerah, pTBHijau, Penyeberang(Vector3.right));
    }

    void Set(Renderer[] m, Renderer[] k, Renderer[] h, Warna w)
    {
        Pasang(m, w == Warna.Merah ? bahanMerah : bahanMati);
        Pasang(k, w == Warna.Kuning ? bahanKuning : bahanMati);
        Pasang(h, w == Warna.Hijau ? bahanHijau : bahanMati);
    }

    void SetP(Renderer[] m, Renderer[] h, Warna w)
    {
        Pasang(m, w == Warna.Merah ? bahanMerah : bahanMati);
        Pasang(h, w == Warna.Hijau || (w == Warna.Kuning && kedipNyala) ? bahanHijau : bahanMati);
    }

    static void Pasang(Renderer[] rs, Material mat)
    {
        if (rs == null || mat == null) return;
        foreach (var r in rs) if (r != null && r.sharedMaterial != mat) r.sharedMaterial = mat;
    }
}
