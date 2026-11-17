using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main Menu "Antar Cepat": tombol MAIN, CARA MAIN, KELUAR.
/// UI dibuat dari skrip dengan gaya kartun yang sama seperti di dalam game.
/// </summary>
public class AntarMenu : MonoBehaviour
{
    public TMP_FontAsset fontJudul;
    public TMP_FontAsset fontIsi;
    [Tooltip("Panggung kecil (Anas + skuter) yang berputar pelan di belakang menu")]
    public Transform diorama;
    public Animator anas;
    public AudioClip suaraKlik;

    RectTransform judul, grupTombol, panelCara;
    AudioSource audioSrc;

    void Start()
    {
        AntarUI.fontJudul = fontJudul; AntarUI.fontIsi = fontIsi;
        audioSrc = gameObject.AddComponent<AudioSource>();
        if (anas != null) { anas.applyRootMotion = false; anas.SetFloat("Speed_f", 0f); anas.SetBool("Grounded_b", true); }
        BuatUI();
        StartCoroutine(Masuk());
        if (anas != null) StartCoroutine(Melambai());
    }

    void Update()
    {
        if (diorama != null) diorama.Rotate(0f, 12f * Time.deltaTime, 0f, Space.World);
        if (judul != null)
        {
            float s = 1f + Mathf.Sin(Time.time * 2.2f) * 0.025f;
            judul.localScale = new Vector3(s, s, 1f);
        }
        if (panelCara != null && panelCara.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape)) TutupCara();
    }

    void BuatUI()
    {
        var akar = AntarUI.BuatKanvas("Menu Antar Cepat", 10);

        // judul
        judul = AntarUI.Wadah(akar, "Judul", new Vector2(0, 1), new Vector2(110, -90), new Vector2(900, 260));
        judul.pivot = new Vector2(0.5f, 0.5f); judul.anchoredPosition = new Vector2(560, -210);
        judul.localRotation = Quaternion.Euler(0, 0, 3f);
        var tJudul = AntarUI.Teks(judul, "ANTAR\nCEPAT", 150, AntarUI.KUNING, TextAlignmentOptions.Center, 0f, true, 0.3f);
        tJudul.lineSpacing = -28f;
        var sub = AntarUI.Panel(akar, "Subjudul", new Vector2(0, 1), new Vector2(330, -360), new Vector2(460, 70), AntarUI.HIJAU, 35f, 6f);
        sub.localRotation = Quaternion.Euler(0, 0, -2f);
        AntarUI.Teks(sub, "Ojol Makanan Kota", 36, Color.white, TextAlignmentOptions.Center, 10f, true, 0.25f);

        // tombol
        grupTombol = AntarUI.Wadah(akar, "Tombol", new Vector2(0, 0), new Vector2(330, 120), new Vector2(460, 420));
        AntarUI.Tombol(grupTombol, "MAIN", new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(460, 120), AntarUI.HIJAU, Main, 60);
        AntarUI.Tombol(grupTombol, "CARA MAIN", new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(460, 104), AntarUI.BIRU, BukaCara, 46);
        AntarUI.Tombol(grupTombol, "KELUAR", new Vector2(0.5f, 1), new Vector2(0, -284), new Vector2(460, 104), AntarUI.MERAH, Keluar, 46);
        foreach (RectTransform t in grupTombol) t.localScale = Vector3.zero;

        // panel cara main
        panelCara = AntarUI.Panel(akar, "Panel Cara Main", new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1180, 700), AntarUI.KRIM, 40f, 8f);
        var pita = AntarUI.Panel(panelCara, "Pita", new Vector2(0.5f, 1), new Vector2(0, 50), new Vector2(520, 100), AntarUI.BIRU, 30f, 8f);
        pita.localRotation = Quaternion.Euler(0, 0, -2f);
        AntarUI.Teks(pita, "CARA MAIN", 60, Color.white, TextAlignmentOptions.Center, 0f, true, 0.28f);
        var isi = AntarUI.Wadah(panelCara, "Isi", new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1040, 440));
        AntarUI.Teks(isi,
            "Kamu adalah <b>Anas</b>, driver baru Antar Cepat. Antar <b>3 pesanan</b> makanan dari toko ke rumah pelanggan!\n\n" +
            "<color=#2EC46A><b>WASD / Panah</b></color>   jalan          <color=#2EC46A><b>Shift</b></color>   jalan pelan\n" +
            "<color=#2EC46A><b>Geser layar</b></color>   putar kamera     <color=#2EC46A><b>Spasi</b></color>   lompat\n" +
            "<color=#2EC46A><b>E / tombol AKSI</b></color>   ambil pesanan, ketuk pintu, bicara, naik & turun skuter\n\n" +
            "Setelah pesanan pertama kamu dapat <b>skuter listrik</b>. Naik skuter lalu ikuti <b>garis GPS biru</b> ke tujuan.",
            34, AntarUI.TEKS, TextAlignmentOptions.TopLeft);
        AntarUI.Tombol(panelCara, "KEMBALI", new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(380, 100), AntarUI.ORANYE, TutupCara, 44);
        panelCara.gameObject.SetActive(false);
    }

    IEnumerator Masuk()
    {
        yield return new WaitForSeconds(0.25f);
        foreach (RectTransform t in grupTombol)
        {
            StartCoroutine(Pop(t, 0.35f));
            yield return new WaitForSeconds(0.12f);
        }
    }

    IEnumerator Melambai()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
            anas.SetTrigger("Wave");
        }
    }

    static IEnumerator Pop(Transform t, float durasi)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        for (float u = 0f; u < 1f; u += Time.unscaledDeltaTime / durasi)
        {
            float x = u - 1f;
            float s = 1f + c3 * x * x * x + c1 * x * x;
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    void Klik() { if (suaraKlik != null) audioSrc.PlayOneShot(suaraKlik, 0.8f); }

    void Main()
    {
        Klik();
        AntarPindah.Ke(AntarPindah.SCENE_GAME);
    }

    void BukaCara()
    {
        Klik();
        grupTombol.gameObject.SetActive(false);
        panelCara.gameObject.SetActive(true);
        StartCoroutine(Pop(panelCara, 0.3f));
    }

    void TutupCara()
    {
        Klik();
        panelCara.gameObject.SetActive(false);
        grupTombol.gameObject.SetActive(true);
    }

    void Keluar()
    {
        Klik();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
