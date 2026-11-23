using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// ANTAR CEPAT — alur game ojol makanan Anas.
/// Setiap pesanan: ambil makanan di toko (bicara dengan penjual) ➔ antar ke rumah pelanggan
/// (ketuk pintu, pintu terbuka, pelanggan keluar) ➔ dapat rating & ongkir. Setelah semua pesanan (6, keliling kota): selesai.
/// Tombol: WASD/klik = jalan, Spasi = lompat, E = aksi (atau tombol AKSI / LOMPAT di layar).
/// HUD dibuat dari skrip supaya tidak perlu disusun manual.
/// </summary>
public class AntarGame : MonoBehaviour
{
    [System.Serializable]
    public class Pesanan
    {
        public string makanan = "Donat";
        public string namaToko = "Toko Donat";
        public string namaRumah = "Rumah Hijau";
        public Transform titikToko;
        public AntarNpc penjual;
        public AntarPintu pintu;
        public Transform titikAntar;
        public Color warnaKemasan = new Color(1f, 0.6f, 0.75f);
        public int ongkir = 15000;
        [Tooltip("Batas waktu antar (detik). 0 = dihitung otomatis dari jarak toko ke rumah")]
        public float batasWaktu = 0f;
    }

    public Transform pemain;
    public List<Pesanan> pesanan = new List<Pesanan>();
    [Tooltip("Anak kecil yang ikut Anas setelah diajak bicara")]
    public AntarNpc dodi;
    public float jarakAksi = 2.6f;

    [Header("Dialog pembuka & penutup")]
    [TextArea(2, 4)] public string[] dialogPembuka;
    [TextArea(2, 4)] public string[] dialogPenutup;

    [Header("Suara")]
    public AudioClip suaraNotif;
    public AudioClip suaraAmbil;
    public AudioClip suaraKoin;
    [Tooltip("Dengung motor skuter listrik (berulang)")]
    public AudioClip suaraSkuter;
    public AudioClip suaraKlakson;
    [Tooltip("Detak saat waktu antar hampir habis")]
    public AudioClip suaraDetik;

    public Color warnaUtama = new Color(0.0f, 0.67f, 0.36f);

    [Header("Font kartun")]
    public TMP_FontAsset fontJudul;
    public TMP_FontAsset fontIsi;
    [Tooltip("Font untuk balon sapaan warga (Text biasa)")]
    public Font fontBalon;

    // status
    int indeks;
    bool membawa;
    bool sibuk;
    int pendapatan;
    readonly List<int> rating = new List<int>();
    bool tombolAksi;

    // batas waktu antar & rekap
    float sisaWaktu, batasKini, waktuTempuh;
    bool timerJalan;
    int detikTerakhir = -1;
    readonly List<string> rekap = new List<string>();
    RectTransform chipWaktu;
    TextMeshProUGUI teksWaktu;
    Image isiChipWaktu;
    Transform hudAkar;
    // tangan & suara
    Transform pegangKiri, pegangKanan, titikKetuk;
    bool menyerahkan;
    AudioSource dengungSkuter;
    GameObject tombolKlakson;
    bool klaksonDiminta;
    Transform penanda;
    GameObject kemasan;
    AntarNpc[] semuaNpc;
    NavMeshAgent agenPemain;
    AntarLompat lompat;

    // UI
    TextMeshProUGUI teksPesanan, teksNomor, teksJarak, teksUang, teksPrompt, teksNama, teksDialog, teksNotif, teksAkhir;
    GameObject panelDialog, panelPrompt, panelAkhir, tombolLompat;
    CanvasGroup grupNotif;
    RectTransform promptRt, panahLanjut, kartuAkhir, tombolAksiRt;
    RectTransform[] bintang, tombolAkhir;
    Image isiPitaNama;
    bool lanjutDitekan;

    void Start()
    {
        agenPemain = pemain.GetComponent<NavMeshAgent>();
        lompat = pemain.GetComponent<AntarLompat>();
        semuaNpc = FindObjectsByType<AntarNpc>(FindObjectsInactive.Include);
        BuatHud();
        GayaBalonWarga();
        BuatTasOjol();
        BuatPenanda();
        SiapkanSkuter();
        StartCoroutine(Pembuka());
    }

    // ================== ALUR ==================
    IEnumerator Pembuka()
    {
        sibuk = true;
        // tunggu cutscene pembuka (Timeline Modul 8) selesai
        yield return new WaitForSeconds(0.3f);
        var cutscene = FindObjectsByType<Modul8Cutscene>();
        while (true)
        {
            bool main = false;
            foreach (var c in cutscene) if (c != null && c.IsPlaying) main = true;
            if (!main) break;
            yield return null;
        }
        Notif($"PESANAN MASUK! ({pesanan.Count})");
        yield return Dialog(dialogPembuka, null);
        sibuk = false;
        PerbaruiHud();
    }

    void Update()
    {
        UpdateWaktu();
        UpdateTangan();
        UpdateSuaraSkuter();
        if (naik && (Input.GetKeyDown(KeyCode.H) || klaksonDiminta)) { if (suaraKlakson != null) AudioSource.PlayClipAtPoint(suaraKlakson, pemain.position, 0.9f); }
        klaksonDiminta = false;
        if (sibuk) { panelPrompt.SetActive(false); penanda.gameObject.SetActive(false); return; }
        bool tekanAksi = Input.GetKeyDown(KeyCode.E) || tombolAksi;
        tombolAksi = false;
        if (indeks >= pesanan.Count)
        {
            panelPrompt.SetActive(false);
            if (naik && tekanAksi) StartCoroutine(Turun());
            return;
        }

        var p = pesanan[indeks];
        Transform tujuan = membawa ? p.titikAntar : p.titikToko;
        var pos = pemain.position;

        // penanda tujuan
        penanda.gameObject.SetActive(true);
        penanda.position = tujuan.position + Vector3.up * (2.6f + Mathf.Sin(Time.time * 3f) * 0.25f);
        penanda.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        PerbaruiSkuterDanGps(tujuan);
        float jarakTujuan = Vector3.Distance(Datar(pos), Datar(tujuan.position));
        float jarakTampil = panjangJalur > 0f ? panjangJalur : jarakTujuan;
        teksJarak.text = $"{Mathf.RoundToInt(jarakTampil)} m";
        if (naik)
        {
            teksGps.text = $"Ke\n<b>{(membawa ? p.namaRumah : p.namaToko)}</b>";
            teksGpsJarak.text = $"{Mathf.RoundToInt(jarakTampil)} m";
        }

        string prompt = null;
        System.Action aksi = null;
        if (naik)
        {
            if (jarakTujuan < 12f) prompt = "Parkir & turun dari skuter";
            if (tekanAksi) { StartCoroutine(Turun()); tekanAksi = false; }
        }
        else if (jarakTujuan < jarakAksi)
        {
            if (!membawa) { prompt = $"Ambil pesanan {p.makanan}"; aksi = () => StartCoroutine(AmbilPesanan(p)); }
            else { prompt = "Ketuk pintu & antar pesanan"; aksi = () => StartCoroutine(AntarPesanan(p)); }
        }
        else if (skuter != null && Vector3.Distance(Datar(pos), Datar(skuter.position)) < 2.8f)
        {
            prompt = "Naik skuter listrik";
            aksi = () => StartCoroutine(Naik());
        }
        else
        {
            // NPC lain yang bisa diajak bicara
            var npc = NpcTerdekat(pos);
            if (npc != null)
            {
                prompt = $"Bicara dengan {npc.nama}";
                aksi = () => StartCoroutine(BicaraNpc(npc));
            }
        }

        bool tampil = prompt != null;
        if (tampil && !panelPrompt.activeSelf) StartCoroutine(Pop(promptRt, 0.25f));
        panelPrompt.SetActive(tampil);
        if (prompt != null)
        {
            teksPrompt.text = prompt;
            if (tekanAksi && aksi != null) aksi();
        }
    }

    AntarNpc NpcTerdekat(Vector3 pos)
    {
        AntarNpc terbaik = null; float jarak = jarakAksi;
        foreach (var n in semuaNpc)
        {
            if (n == null || !n.isActiveAndEnabled || n.dialog == null || n.dialog.Length == 0) continue;
            if (IsPenjualAktif(n)) continue;
            float d = Vector3.Distance(Datar(pos), Datar(n.transform.position));
            if (d < jarak) { jarak = d; terbaik = n; }
        }
        return terbaik;
    }

    bool IsPenjualAktif(AntarNpc n)
    {
        foreach (var p in pesanan) if (p.penjual == n || (p.pintu != null && p.pintu.pelanggan == n)) return true;
        return false;
    }

    IEnumerator BicaraNpc(AntarNpc npc)
    {
        sibuk = true;
        yield return Dialog(npc.dialog, npc);
        if (npc == dodi && dodi.perilaku != AntarNpc.Perilaku.Ikut)
        {
            dodi.MulaiIkut();
            Notif("Dodi ikut menemanimu!");
        }
        sibuk = false;
    }

    IEnumerator AmbilPesanan(Pesanan p)
    {
        sibuk = true;
        if (p.penjual != null) yield return Dialog(p.penjual.dialog, p.penjual);
        membawa = true;
        Main(suaraAmbil);
        kemasan.GetComponent<Renderer>().material.color = p.warnaKemasan;
        kemasan.SetActive(true);
        batasKini = p.batasWaktu > 0f ? p.batasWaktu : HitungBatas(p);
        sisaWaktu = batasKini; waktuTempuh = 0f; detikTerakhir = -1; timerJalan = true;
        if (chipWaktu != null) { chipWaktu.gameObject.SetActive(true); StartCoroutine(Pop(chipWaktu, 0.3f)); }
        Notif($"{p.makanan} diambil! Antar dalam {FormatWaktu(batasKini)}");
        PerbaruiHud();
        if (skuter == null && prefabSkuter != null)
        {
            yield return new WaitForSeconds(0.4f);
            BeriSkuter();
            yield return Dialog(dialogSkuter, null);
        }
        sibuk = false;
    }

    IEnumerator AntarPesanan(Pesanan p)
    {
        sibuk = true;
        Hentikan();
        timerJalan = false;
        int bintangP = waktuTempuh <= batasKini ? 5 : (waktuTempuh <= batasKini * 1.35f ? 4 : 3);
        int tip = waktuTempuh <= batasKini * 0.6f ? 5000 : 0;
        AntarNpc pelanggan = p.pintu != null ? p.pintu.pelanggan : null;
        if (p.pintu != null)
        {
            // melangkah ke depan pintu, lalu mengetuk
            var pintuT = p.pintu.transform;
            // titik berdiri di depan pintu (sisi luar rumah); kalau tidak ada NavMesh di situ, cari yang terdekat ke arah jalan
            Vector3 depan = pemain.position;
            NavMeshHit hit;
            for (float jauh = 0.75f; jauh <= 2.5f; jauh += 0.35f)
                if (NavMesh.SamplePosition(pintuT.position + pintuT.forward * jauh, out hit, 0.6f, NavMesh.AllAreas)) { depan = hit.position; break; }
            Vector3 dari = pemain.position;
            var hadap = pintuT.position - depan; hadap.y = 0f;
            Quaternion rDari = pemain.rotation, rKe = hadap.sqrMagnitude > 0.01f ? Quaternion.LookRotation(hadap) : pemain.rotation;
            for (float u = 0f; u < 1f; u += Time.deltaTime / 0.35f)
            {
                agenPemain.Warp(Vector3.Lerp(dari, depan, Mathf.SmoothStep(0f, 1f, u)));
                pemain.rotation = Quaternion.Slerp(rDari, rKe, u);
                yield return null;
            }
            agenPemain.Warp(depan); pemain.rotation = rKe;
            StartCoroutine(Ketuk(pintuT));
            yield return p.pintu.Buka();
            // serahkan pesanan ke pelanggan
            if (pelanggan != null) yield return Serahkan(pelanggan);
            if (pelanggan != null)
            {
                string[] baris = bintangP == 5 ? pelanggan.dialog : DialogTelat(pelanggan.nama, bintangP);
                yield return Dialog(baris, pelanggan);
                Senang(pelanggan, bintangP);
            }
        }
        membawa = false;
        kemasan.SetActive(false);
        if (chipWaktu != null) chipWaktu.gameObject.SetActive(false);
        int dapat = p.ongkir + tip;
        pendapatan += dapat;
        rating.Add(bintangP);
        rekap.Add($"{p.makanan}   <color=#8A7A99>{FormatWaktu(waktuTempuh)}</color>   <color=#FF8A2B><b>{bintangP}/5</b></color>   <color=#2EC46A>Rp {dapat:N0}</color>".Replace(',', '.'));
        Main(suaraKoin);
        EfekHadiah(pelanggan != null ? pelanggan.transform.position + Vector3.up * 2.2f : pemain.position + Vector3.up * 2.4f, bintangP, dapat, tip > 0);
        Notif((tip > 0 ? $"Super cepat! {bintangP} bintang  +Rp {dapat:N0} (tip)" : $"Pesanan selesai! {bintangP} bintang  +Rp {dapat:N0}").Replace(',', '.'));
        indeks++;
        PerbaruiHud();
        if (indeks >= pesanan.Count)
        {
            yield return new WaitForSeconds(1.6f);
            yield return Penutup();
        }
        sibuk = false;
    }

    // ---------- batas waktu ----------
    float HitungBatas(Pesanan p)
    {
        float panjang = 60f;
        if (p.titikToko != null && p.titikAntar != null)
        {
            var path = new NavMeshPath();
            NavMeshHit a, b;
            Vector3 dari = p.titikToko.position, ke = p.titikAntar.position;
            if (NavMesh.SamplePosition(dari, out a, 4f, NavMesh.AllAreas)) dari = a.position;
            if (NavMesh.SamplePosition(ke, out b, 4f, NavMesh.AllAreas)) ke = b.position;
            if (NavMesh.CalculatePath(dari, ke, NavMesh.AllAreas, path) && path.corners.Length > 1)
            {
                panjang = 0f;
                for (int i = 1; i < path.corners.Length; i++) panjang += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }
            else panjang = Vector3.Distance(dari, ke) * 1.3f;
        }
        // naik skuter + parkir + jalan ke pintu, dengan kelonggaran untuk pemain baru
        return Mathf.Ceil((30f + panjang / 4f) / 5f) * 5f;
    }

    static string FormatWaktu(float detik)
    {
        int d = Mathf.Max(0, Mathf.CeilToInt(detik));
        return $"{d / 60}:{d % 60:00}";
    }

    void UpdateWaktu()
    {
        if (chipWaktu == null || !timerJalan) return;
        if (!sibuk || naik) { sisaWaktu -= Time.deltaTime; waktuTempuh += Time.deltaTime; }
        float r = sisaWaktu / Mathf.Max(1f, batasKini);
        if (sisaWaktu >= 0f)
        {
            teksWaktu.text = $"WAKTU  {FormatWaktu(sisaWaktu)}";
            isiChipWaktu.color = r > 0.5f ? HIJAU : (r > 0.2f ? ORANYE : MERAH);
        }
        else
        {
            teksWaktu.text = $"TELAT  +{FormatWaktu(-sisaWaktu)}";
            isiChipWaktu.color = MERAH;
        }
        // detak & denyut 10 detik terakhir
        int detik = Mathf.CeilToInt(sisaWaktu);
        if (sisaWaktu > 0f && sisaWaktu <= 10f)
        {
            if (detik != detikTerakhir) { detikTerakhir = detik; if (suaraDetik != null) AudioSource.PlayClipAtPoint(suaraDetik, pemain.position, 0.6f); }
            chipWaktu.localScale = Vector3.one * (1f + Mathf.Max(0f, Mathf.Sin((sisaWaktu % 1f) * Mathf.PI)) * 0.12f);
        }
        else chipWaktu.localScale = Vector3.one;
    }

    static string[] DialogTelat(string nama, int bintangP)
    {
        if (bintangP == 4)
            return new[]
            {
                $"{nama}|Akhirnya datang juga. Agak lama, tapi makanannya masih enak.",
                "Anas|Maaf sudah menunggu. Selamat menikmati!",
                $"{nama}|Bintang empat, ya. Semangat terus!",
            };
        return new[]
        {
            $"{nama}|Lama sekali... makanannya sudah mulai dingin.",
            "Anas|Maaf, tadi saya kelamaan di jalan. Selamat menikmati!",
            $"{nama}|Bintang tiga dulu, ya. Lain kali lebih cepat.",
        };
    }

    // ---------- tangan Anas: memegang kemasan, mengetuk, menyerahkan ----------
    void UpdateTangan()
    {
        if (kontrol == null || menyerahkan) return;
        bool pegang = kemasan != null && kemasan.activeInHierarchy && !naik;
        kontrol.ikKiri = pegang ? pegangKiri : null;
        if (kontrol.ikKanan != titikKetuk || titikKetuk == null || !titikKetuk.gameObject.activeSelf)
            kontrol.ikKanan = pegang ? pegangKanan : null;
    }

    IEnumerator Ketuk(Transform pintuT)
    {
        if (titikKetuk == null) { titikKetuk = new GameObject("Titik Ketuk").transform; }
        titikKetuk.gameObject.SetActive(true);
        var dasar = pintuT.position + Vector3.up * 1.35f + pintuT.forward * 0.12f;
        var dari = kemasan.transform.localPosition;
        kemasan.transform.localPosition = dari + new Vector3(-0.14f, 0f, 0f);   // kemasan dipegang tangan kiri dulu
        kontrol.ikKanan = titikKetuk;
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            // tiga ketukan
            float k = Mathf.Abs(Mathf.Sin(t / 0.9f * Mathf.PI * 3f));
            titikKetuk.position = dasar + pintuT.forward * (0.03f + 0.13f * k);
            yield return null;
        }
        kontrol.ikKanan = null;
        kemasan.transform.localPosition = dari;
        titikKetuk.gameObject.SetActive(false);
        // mundur sedikit supaya pelanggan punya tempat keluar dari pintu
        Vector3 awal = pemain.position, mundur = awal;
        NavMeshHit hit;
        for (float jauh = 2.2f; jauh >= 1.4f; jauh -= 0.2f)
            if (NavMesh.SamplePosition(pintuT.position + pintuT.forward * jauh, out hit, 0.5f, NavMesh.AllAreas)) { mundur = hit.position; break; }
        // lompatan kecil ke belakang (tanpa animasi jalan supaya kaki tidak terlihat "moonwalk")
        if (halusPemain != null) halusPemain.paksaDiam = true;
        float off0 = agenPemain.baseOffset;
        for (float u = 0f; u < 1f; u += Time.deltaTime / 0.45f)
        {
            agenPemain.Warp(Vector3.Lerp(awal, mundur, Mathf.SmoothStep(0f, 1f, u)));
            agenPemain.baseOffset = off0 + Mathf.Sin(u * Mathf.PI) * 0.12f;
            yield return null;
        }
        agenPemain.baseOffset = off0;
        agenPemain.Warp(mundur);
        if (halusPemain != null) halusPemain.paksaDiam = false;
    }

    IEnumerator Serahkan(AntarNpc pelanggan)
    {
        menyerahkan = true;
        pelanggan.sedangBicara = true;
        yield return new WaitForSeconds(0.25f);
        // salinan kemasan berpindah dari tangan Anas ke tangan pelanggan
        var kopi = Instantiate(kemasan);
        kopi.name = "Pesanan " + pelanggan.nama;
        kopi.transform.SetPositionAndRotation(kemasan.transform.position, kemasan.transform.rotation);
        kopi.transform.localScale = kemasan.transform.lossyScale;
        kemasan.SetActive(false);
        Transform kKiri = kopi.transform.Find("Pegang Kiri"), kKanan = kopi.transform.Find("Pegang Kanan");
        kontrol.ikKiri = kKiri; kontrol.ikKanan = kKanan;
        var arah = pemain.position - pelanggan.transform.position; arah.y = 0f; arah.Normalize();
        Vector3 p0 = kopi.transform.position;
        Vector3 p1 = pelanggan.transform.position + Vector3.up * 1.0f + arah * 0.45f;
        Main(suaraAmbil);
        for (float u = 0f; u < 1f; u += Time.deltaTime / 0.6f)
        {
            float e = Mathf.SmoothStep(0f, 1f, u);
            kopi.transform.position = Vector3.Lerp(p0, p1, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.12f;
            // tangan Anas ikut menyodorkan sampai setengah jalan, lalu melepas
            if (u > 0.6f) { kontrol.ikKiri = null; kontrol.ikKanan = null; }
            yield return null;
        }
        kopi.transform.position = p1;
        kopi.transform.SetParent(pelanggan.transform, true);
        kontrol.ikKiri = null; kontrol.ikKanan = null;
        menyerahkan = false;
        yield return new WaitForSeconds(0.2f);
    }

    void Senang(AntarNpc pelanggan, int bintangP)
    {
        var anim = pelanggan.GetComponentInChildren<Animator>();
        if (bintangP >= 4 && anim != null && anim.HasState(0, Animator.StringToHash("Standing_Jump")))
            anim.CrossFadeInFixedTime("Standing_Jump", 0.15f);
        else pelanggan.Lambai();
    }

    // bintang & uang melayang di atas kepala pelanggan
    void EfekHadiah(Vector3 posDunia, int bintangP, int uang, bool adaTip)
    {
        if (hudAkar == null) return;
        var w = Wadah(hudAkar, "Efek Hadiah", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 170));
        var cg = w.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false;
        for (int i = 0; i < 5; i++)
        {
            var b = Wadah(w, "Bintang", new Vector2(0.5f, 1f), new Vector2(-120 + i * 60, -30), new Vector2(56, 56));
            b.pivot = new Vector2(0.5f, 0.5f);
            Lapis(b, "Garis", spBintang, GARIS, Vector2.zero, 0f, 1f);
            Lapis(b, "Isi", spBintang, i < bintangP ? KUNING : new Color(0.75f, 0.72f, 0.8f), Vector2.zero, 6f, 1f);
        }
        var label = Teks(Wadah(w, "Uang", new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(420, 70)),
            ($"+Rp {uang:N0}" + (adaTip ? "  TIP!" : "")).Replace(',', '.'), 48, HIJAU, TextAlignmentOptions.Center, Vector2.zero, true, 0.3f);
        StartCoroutine(AnimEfekHadiah(w, cg, posDunia));
    }

    IEnumerator AnimEfekHadiah(RectTransform w, CanvasGroup cg, Vector3 posDunia)
    {
        var cam = Camera.main;
        var kanvas = hudAkar as RectTransform;
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            var sp = cam != null ? cam.WorldToScreenPoint(posDunia + Vector3.up * t * 0.5f) : new Vector3(Screen.width * 0.5f, Screen.height * 0.6f, 1f);
            Vector2 lokal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(kanvas, sp, null, out lokal);
            w.anchoredPosition = lokal;
            float muncul = Mathf.Clamp01(t / 0.25f);
            w.localScale = Vector3.one * (sp.z > 0f ? 1f : 0f) * (0.6f + 0.4f * muncul + Mathf.Sin(muncul * Mathf.PI) * 0.25f);
            cg.alpha = t < 1.6f ? 1f : 1f - (t - 1.6f) / 0.6f;
            yield return null;
        }
        Destroy(w.gameObject);
    }

    // ---------- suara skuter ----------
    void UpdateSuaraSkuter()
    {
        if (dengungSkuter == null) return;
        float laju = kontrol != null ? kontrol.Kecepatan.magnitude : 0f;
        float maks = kontrol != null ? Mathf.Max(1f, kontrol.kecepatanSkuter) : 9f;
        float r = Mathf.Clamp01(laju / maks);
        float volTarget = naik ? 0.12f + 0.45f * r : 0f;
        dengungSkuter.volume = Mathf.MoveTowards(dengungSkuter.volume, volTarget, Time.deltaTime * 1.5f);
        dengungSkuter.pitch = Mathf.Lerp(dengungSkuter.pitch, 0.75f + 0.75f * r, 1f - Mathf.Exp(-6f * Time.deltaTime));
        if (dengungSkuter.volume > 0.001f && !dengungSkuter.isPlaying) dengungSkuter.Play();
        else if (dengungSkuter.volume <= 0.001f && dengungSkuter.isPlaying) dengungSkuter.Stop();
    }

    IEnumerator Penutup()
    {
        penanda.gameObject.SetActive(false);
        // kalimat "rating sempurna" hanya dipakai kalau semua pesanan memang bintang 5
        var penutup = new List<string>(dialogPenutup);
        if (RataRating() < 4.95)
            for (int i = 0; i < penutup.Count; i++)
                if (penutup[i].Contains("sempurna"))
                {
                    int bar = penutup[i].IndexOf('|');
                    string nama = bar >= 0 ? penutup[i].Substring(0, bar + 1) : "";
                    penutup[i] = nama + $"Kerja bagus! Semua pesanan terkirim dengan rating {RataRating():0.0}. Besok coba lebih cepat, ya!";
                }
        yield return Dialog(penutup.ToArray(), null);
        panelPrompt.SetActive(false);
        panelAkhir.SetActive(true);
        teksAkhir.text = "";
        yield return Pop(kartuAkhir, 0.4f);
        int rata = rating.Count > 0 ? Mathf.RoundToInt((float)RataRating()) : 5;
        for (int i = 0; i < bintang.Length; i++)
        {
            var isi = bintang[i].Find("Isi");
            if (isi != null) isi.GetComponent<Image>().color = i < rata ? KUNING : new Color(0.8f, 0.77f, 0.84f);
        }
        foreach (var b in bintang)
        {
            Main(suaraAmbil);
            StartCoroutine(Pop(b, 0.3f));
            yield return new WaitForSeconds(0.18f);
        }
        for (float u = 0f; u <= 1f; u += Time.deltaTime / 1.2f)
        {
            TulisAkhir(Mathf.RoundToInt(Mathf.Lerp(0f, pendapatan, u) / 500f) * 500);
            yield return null;
        }
        TulisAkhir(pendapatan);
        Main(suaraKoin);
        foreach (var t in tombolAkhir)
        {
            StartCoroutine(Pop(t, 0.3f));
            yield return new WaitForSeconds(0.12f);
        }
        InputManager.bCanPlayerMove = true;
    }

    double RataRating()
    {
        if (rating.Count == 0) return 5.0;
        double t = 0; foreach (var r in rating) t += r;
        return t / rating.Count;
    }

    void TulisAkhir(int uang)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in rekap) sb.Append(r).Append('\n');
        sb.Append($"\nRating   <color=#FF8A2B><b>{RataRating():0.0} / 5</b></color>     Pendapatan   <color=#2EC46A><b>Rp {uang:N0}</b></color>".Replace(',', '.'));
        teksAkhir.text = sb.ToString();
    }

    void LateUpdate()
    {
        if (panelPrompt != null && panelPrompt.activeSelf)
            promptRt.anchoredPosition = new Vector2(0f, 320f + Mathf.Sin(Time.time * 4f) * 5f);
        if (panelDialog != null && panelDialog.activeSelf)
            panahLanjut.anchoredPosition = new Vector2(-40f, 22f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 8f);
        if (tombolAksiRt != null)
        {
            bool ajak = panelPrompt.activeSelf || panelDialog.activeSelf;
            float s = ajak ? 1f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.07f : 1f;
            tombolAksiRt.localScale = Vector3.one * s;
        }
    }

    // ================== DIALOG ==================
    IEnumerator Dialog(string[] baris, AntarNpc npc)
    {
        if (baris == null || baris.Length == 0) yield break;
        Hentikan();
        InputManager.bCanPlayerMove = false;
        if (npc != null) npc.sedangBicara = true;
        if (npc != null && kameraTpp != null) kameraTpp.lawanBicara = npc.transform;
        panelDialog.SetActive(true);
        StartCoroutine(Pop(panelDialog.transform, 0.25f));
        if (tombolLompat) tombolLompat.SetActive(false);
        foreach (var b in baris)
        {
            string nama = "Anas", teks = b;
            int i = b.IndexOf('|');
            if (i >= 0) { nama = b.Substring(0, i); teks = b.Substring(i + 1); }
            teksNama.text = nama;
            isiPitaNama.color = WarnaNama(nama);
            var rtNama = (RectTransform)isiPitaNama.transform.parent;
            rtNama.sizeDelta = new Vector2(Mathf.Max(220f, teksNama.GetPreferredValues(nama).x + 70f), rtNama.sizeDelta.y);
            teksDialog.text = teks;
            // hadapkan Anas ke lawan bicara
            if (npc != null)
            {
                var d = npc.transform.position - pemain.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) pemain.rotation = Quaternion.LookRotation(d);
            }
            // efek ketik
            teksDialog.maxVisibleCharacters = 0;
            lanjutDitekan = false;
            yield return null;
            for (int c = 0; c <= teks.Length; c++)
            {
                teksDialog.maxVisibleCharacters = c;
                if (TekanLanjut()) { teksDialog.maxVisibleCharacters = teks.Length; break; }
                yield return new WaitForSeconds(0.02f);
            }
            yield return null;
            while (!TekanLanjut()) yield return null;
        }
        panelDialog.SetActive(false);
        if (tombolLompat) tombolLompat.SetActive(!naik);
        if (npc != null) npc.sedangBicara = false;
        if (kameraTpp != null) kameraTpp.lawanBicara = null;
        InputManager.bCanPlayerMove = true;
        yield return null;
    }

    bool TekanLanjut()
    {
        bool t = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || lanjutDitekan || tombolAksi;
        lanjutDitekan = false; tombolAksi = false;
        return t;
    }

    void Hentikan()
    {
        if (kontrol != null) kontrol.Berhenti();
        if (agenPemain != null && agenPemain.isOnNavMesh) { agenPemain.ResetPath(); agenPemain.velocity = Vector3.zero; }
    }

    static Vector3 Datar(Vector3 v) { v.y = 0f; return v; }

    void Main(AudioClip c) { if (c != null) AudioSource.PlayClipAtPoint(c, pemain.position, 0.8f); }

    // ================== SKUTER LISTRIK & GPS ==================
    [Header("Skuter listrik")]
    public GameObject prefabSkuter;
    public float tinggiDek = 0.33f;
    public float skalaSkuter = 1.3f;
    float Dek => tinggiDek * skalaSkuter;

    AntarPemain kontrol;
    AntarHalus halusPemain;
    AntarKamera kameraTpp;
    Transform skuter, rodaDepan, rodaBelakang;
    bool naik;
    LineRenderer garisGps;
    Material bahanGps;
    NavMeshPath jalur;
    float waktuJalur;
    float panjangJalur = -1f;
    Vector3 arahGps;
    GameObject panelGps;
    RectTransform panahGps;
    TextMeshProUGUI teksGps, teksGpsJarak;
    float offsetPemainAwal;

    static readonly string[] dialogSkuter =
    {
        "Aplikasi Antar Cepat|Selamat, Anas! Kamu dapat skuter listrik dari Antar Cepat.",
        "Aplikasi Antar Cepat|Naik skuternya (tekan E di dekat skuter), lalu ikuti garis GPS ke rumah pelanggan.",
    };

    void SiapkanSkuter()
    {
        kontrol = pemain.GetComponent<AntarPemain>();
        halusPemain = pemain.GetComponent<AntarHalus>();
        if (Camera.main != null) kameraTpp = Camera.main.GetComponent<AntarKamera>();
        offsetPemainAwal = agenPemain != null ? agenPemain.baseOffset : 0f;
        jalur = new NavMeshPath();

        // garis GPS di jalan
        var g = new GameObject("Garis GPS");
        garisGps = g.AddComponent<LineRenderer>();
        garisGps.widthMultiplier = 0.9f;
        garisGps.numCornerVertices = 4; garisGps.numCapVertices = 4;
        garisGps.alignment = LineAlignment.TransformZ;
        g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // garis rebah di tanah
        garisGps.textureMode = LineTextureMode.Tile;
        garisGps.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        garisGps.receiveShadows = false;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        bahanGps = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
        bahanGps.mainTexture = TeksturPanah();
        if (sh != null)
        {
            bahanGps.SetFloat("_Surface", 1f); bahanGps.SetFloat("_Blend", 0f);
            bahanGps.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            bahanGps.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            bahanGps.SetFloat("_ZWrite", 0f);
            bahanGps.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            bahanGps.renderQueue = 3000;
        }
        bahanGps.mainTextureScale = new Vector2(0.55f, 1f);
        garisGps.material = bahanGps;
        garisGps.positionCount = 0;
        g.SetActive(false);
    }

    static Texture2D TeksturPanah()
    {
        const int L = 64, T = 32;
        var t = new Texture2D(L, T, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Bilinear;
        var biru = new Color32(0x2F, 0x9B, 0xF0, 235);
        var tepi = new Color32(0x1B, 0x4F, 0x9C, 255);
        for (int y = 0; y < T; y++)
            for (int x = 0; x < L; x++)
            {
                float v = (y + 0.5f) / T;          // 0..1 lebar garis
                float u = (x + 0.5f) / L;          // 0..1 sepanjang garis
                Color32 c = (v < 0.12f || v > 0.88f) ? tepi : biru;
                // chevron ">" putih
                float jarakTengah = Mathf.Abs(v - 0.5f);
                float garis = u - 0.30f + jarakTengah * 0.7f;
                if (garis > 0f && garis < 0.16f && v > 0.18f && v < 0.82f) c = new Color32(255, 255, 255, 255);
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    void BeriSkuter()
    {
        if (skuter != null || prefabSkuter == null) return;
        var pos = pemain.position + pemain.right * 1.8f + pemain.forward * 0.4f;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(pos, out hit, 3f, NavMesh.AllAreas)) pos = hit.position;
        var go = Instantiate(prefabSkuter, pos, Quaternion.Euler(0f, pemain.eulerAngles.y, 0f));
        go.name = "Skuter Listrik Anas";
        skuter = go.transform;
        skuter.localScale = Vector3.one * skalaSkuter;
        // titik pegangan tangan (untuk IK)
        var gk = new GameObject("Pegangan_Kiri").transform; gk.SetParent(skuter, false); gk.localPosition = new Vector3(-0.33f, 1.82f, 0.60f);
        var gn = new GameObject("Pegangan_Kanan").transform; gn.SetParent(skuter, false); gn.localPosition = new Vector3(0.33f, 1.82f, 0.60f);
        if (kontrol != null) { kontrol.gripKiri = gk; kontrol.gripKanan = gn; }
        if (suaraSkuter != null)
        {
            dengungSkuter = go.AddComponent<AudioSource>();
            dengungSkuter.clip = suaraSkuter; dengungSkuter.loop = true; dengungSkuter.playOnAwake = false;
            dengungSkuter.volume = 0f; dengungSkuter.spatialBlend = 0.4f;
        }
        rodaDepan = skuter.Find("Roda_Depan");
        rodaBelakang = skuter.Find("Roda_Belakang");
        Parkir();
        StartCoroutine(Pop(skuter, 0.45f));
    }

    void Parkir()
    {
        var e = skuter.eulerAngles;
        skuter.rotation = Quaternion.Euler(0f, e.y, 5f);   // sedikit miring bertumpu di standar
    }

    IEnumerator Naik()
    {
        sibuk = true;
        Hentikan();
        InputManager.bCanPlayerMove = false;
        naik = true;
        if (kontrol != null) kontrol.naikSkuter = true;
        if (halusPemain != null) halusPemain.paksaDiam = true;
        if (lompat != null) lompat.dikunci = true;
        if (tombolLompat) tombolLompat.SetActive(false);
        if (tombolKlakson) tombolKlakson.SetActive(true);
        kemasan.SetActive(false);   // pesanan disimpan di tas ojol

        // lompat kecil ke atas dek
        Vector3 dari = pemain.position, ke = skuter.position;
        Quaternion rDari = pemain.rotation, rKe = Quaternion.Euler(0f, skuter.eulerAngles.y, 0f);
        skuter.rotation = rKe;
        Main(suaraAmbil);
        for (float u = 0f; u < 1f; u += Time.deltaTime / 0.35f)
        {
            var p = Vector3.Lerp(dari, ke, u); p.y = Mathf.Lerp(dari.y, ke.y, u);
            agenPemain.Warp(p);
            agenPemain.baseOffset = offsetPemainAwal + Mathf.Lerp(0f, Dek, u) + Mathf.Sin(u * Mathf.PI) * 0.5f;
            pemain.rotation = Quaternion.Slerp(rDari, rKe, u);
            yield return null;
        }
        agenPemain.Warp(ke);
        agenPemain.baseOffset = offsetPemainAwal + Dek;
        pemain.rotation = rKe;
        skuter.SetParent(pemain, true);
        skuter.localPosition = new Vector3(0f, -Dek, 0f);
        skuter.localRotation = Quaternion.identity;
        if (kameraTpp != null) kameraTpp.KeBelakang();

        // Dodi ikut membonceng kalau sedang menemani
        if (dodi != null && dodi.perilaku == AntarNpc.Perilaku.Ikut && dodi.gameObject.activeInHierarchy)
        {
            var ag = dodi.GetComponent<NavMeshAgent>(); if (ag != null) ag.enabled = false;
            var h = dodi.GetComponent<AntarHalus>(); if (h != null) h.paksaDiam = true;
            dodi.transform.SetParent(skuter, false);
            dodi.transform.localPosition = new Vector3(0f, 0.42f, -0.82f) ;
            dodi.transform.localScale = Vector3.one * (0.72f / skalaSkuter);
            var rbd = dodi.GetComponent<Rigidbody>(); if (rbd != null) rbd.isKinematic = true;
            dodi.transform.localRotation = Quaternion.identity;
        }

        Notif("GPS aktif! Ikuti garis biru");
        waktuJalur = 0f;
        InputManager.bCanPlayerMove = true;
        sibuk = false;
    }

    IEnumerator Turun()
    {
        sibuk = true;
        Hentikan();
        InputManager.bCanPlayerMove = false;
        garisGps.gameObject.SetActive(false);
        panelGps.SetActive(false);

        // turunkan Dodi dulu
        if (dodi != null && dodi.transform.parent == skuter)
        {
            dodi.transform.SetParent(null, true);
            dodi.transform.localScale = Vector3.one * 0.72f;
            var sisi = pemain.position - pemain.right * 1.6f - pemain.forward * 0.8f;
            NavMeshHit hd; if (NavMesh.SamplePosition(sisi, out hd, 3f, NavMesh.AllAreas)) sisi = hd.position;
            dodi.transform.position = sisi;
            dodi.transform.rotation = Quaternion.Euler(0f, pemain.eulerAngles.y, 0f);
            var h = dodi.GetComponent<AntarHalus>(); if (h != null) h.paksaDiam = false;
            dodi.MulaiIkut();
        }

        skuter.SetParent(null, true);
        var rot = Quaternion.Euler(0f, pemain.eulerAngles.y, 0f);
        Vector3 tanah = pemain.position - Vector3.up * (agenPemain.baseOffset - offsetPemainAwal);
        Vector3 skuterDari = skuter.position;
        Vector3 skuterKe = tanah + rot * Vector3.right * 1.5f;
        skuterKe.y = tanah.y + (skuterDari.y - (tanah.y + Dek - Dek));
        skuterKe.y = skuterDari.y;
        Vector3 dari = pemain.position;
        Vector3 ke = tanah;
        float offDari = agenPemain.baseOffset;
        Main(suaraAmbil);
        for (float u = 0f; u < 1f; u += Time.deltaTime / 0.3f)
        {
            agenPemain.baseOffset = Mathf.Lerp(offDari, offsetPemainAwal, u) + Mathf.Sin(u * Mathf.PI) * 0.35f;
            pemain.rotation = rot;
            skuter.position = Vector3.Lerp(skuterDari, skuterKe, u);
            skuter.rotation = rot;
            yield return null;
        }
        skuter.position = skuterKe;
        Parkir();
        agenPemain.baseOffset = offsetPemainAwal;

        naik = false;
        if (kontrol != null) kontrol.naikSkuter = false;
        if (halusPemain != null) halusPemain.paksaDiam = false;
        if (lompat != null) lompat.dikunci = false;
        if (tombolLompat) tombolLompat.SetActive(true);
        if (tombolKlakson) tombolKlakson.SetActive(false);
        if (membawa) kemasan.SetActive(true);
        InputManager.bCanPlayerMove = true;
        sibuk = false;
    }

    void PerbaruiSkuterDanGps(Transform tujuan)
    {
        if (!naik || skuter == null) { panjangJalur = -1f; return; }

        // roda berputar sesuai kecepatan
        float laju = kontrol != null ? kontrol.Kecepatan.magnitude : 0f;
        float derajat = laju / (0.2f * skalaSkuter) * Mathf.Rad2Deg * Time.deltaTime;
        if (rodaDepan) rodaDepan.Rotate(derajat, 0f, 0f, Space.Self);
        if (rodaBelakang) rodaBelakang.Rotate(derajat, 0f, 0f, Space.Self);

        if (tujuan == null) return;
        if (!garisGps.gameObject.activeSelf) garisGps.gameObject.SetActive(true);
        if (!panelGps.activeSelf) { panelGps.SetActive(true); StartCoroutine(Pop(panelGps.transform, 0.3f)); }

        waktuJalur -= Time.deltaTime;
        if (waktuJalur <= 0f)
        {
            waktuJalur = 0.25f;
            NavMeshHit a, b;
            Vector3 asal = pemain.position, akhir = tujuan.position;
            if (NavMesh.SamplePosition(asal, out a, 4f, NavMesh.AllAreas)) asal = a.position;
            if (NavMesh.SamplePosition(akhir, out b, 4f, NavMesh.AllAreas)) akhir = b.position;
            if (NavMesh.CalculatePath(asal, akhir, NavMesh.AllAreas, jalur) && jalur.corners.Length > 1)
            {
                var c = jalur.corners;
                garisGps.positionCount = c.Length;
                float panjang = 0f;
                for (int i = 0; i < c.Length; i++)
                {
                    garisGps.SetPosition(i, c[i] + Vector3.up * 0.18f);
                    if (i > 0) panjang += Vector3.Distance(c[i - 1], c[i]);
                }
                panjangJalur = panjang;
                // arah ke belokan berikutnya (lewati titik yang sangat dekat)
                int k = 1;
                while (k < c.Length - 1 && Vector3.Distance(Datar(c[k]), Datar(pemain.position)) < 3f) k++;
                arahGps = Datar(c[k] - pemain.position);
            }
            else
            {
                garisGps.positionCount = 0;
                panjangJalur = -1f;
                arahGps = Datar(tujuan.position - pemain.position);
            }
        }
        // garis berjalan maju seperti navigasi
        bahanGps.mainTextureOffset = new Vector2(-Time.time * 1.2f, 0f);

        // panah GPS di layar: relatif terhadap arah kamera
        var cam = Camera.main != null ? Camera.main.transform : pemain;
        var depan = Datar(cam.forward);
        if (arahGps.sqrMagnitude > 0.01f && depan.sqrMagnitude > 0.01f)
        {
            float sudut = Vector3.SignedAngle(depan, arahGps, Vector3.up);
            panahGps.localRotation = Quaternion.Slerp(panahGps.localRotation, Quaternion.Euler(0f, 0f, -sudut), 1f - Mathf.Exp(-10f * Time.deltaTime));
        }
    }

    void BuatPanelGps(Transform akar)
    {
        var gps = Panel(akar, "Panel GPS", new Vector2(1, 1), new Vector2(-40, -62), new Vector2(330, 200), KRIM);
        var pita = Panel(gps, "Pita GPS", new Vector2(1, 1), new Vector2(12, 36), new Vector2(150, 62), BIRU, 22f);
        pita.localRotation = Quaternion.Euler(0, 0, -4f);
        Teks(pita, "GPS", 38, Color.white, TextAlignmentOptions.Center, Vector2.zero, true, 0.25f);
        var bulat = Panel(gps, "Kompas", new Vector2(0, 0.5f), new Vector2(20, -4), new Vector2(130, 130), Color.white, 0f, 6f, spLingkaran);
        panahGps = Wadah(bulat, "Panah", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78, 78));
        panahGps.pivot = new Vector2(0.5f, 0.5f); panahGps.anchoredPosition = Vector2.zero;
        var bentuk = new Vector2[] { new Vector2(0.5f, 0.97f), new Vector2(0.92f, 0.08f), new Vector2(0.5f, 0.3f), new Vector2(0.08f, 0.08f) };
        var spPanah = SpritePoligon(128, bentuk);
        Lapis(panahGps, "Garis", spPanah, GARIS, Vector2.zero, -5f, 1f);
        Lapis(panahGps, "Isi", spPanah, BIRU, Vector2.zero, 3f, 1f);
        teksGps = Teks(Wadah(gps, "Tujuan", new Vector2(0, 1), new Vector2(162, -36), new Vector2(156, 70)), "", 26, TEKS, TextAlignmentOptions.TopLeft, Vector2.zero);
        var chip = Panel(gps, "Chip GPS", new Vector2(0, 0), new Vector2(162, 22), new Vector2(150, 48), BIRU, 24f, 5f);
        teksGpsJarak = Teks(chip, "", 28, Color.white, TextAlignmentOptions.Center, Vector2.zero, true, 0.22f);
        panelGps = gps.gameObject; panelGps.SetActive(false);
    }

    // ================== HUD (gaya kartun) ==================
    static readonly Color GARIS = new Color32(0x2B, 0x1D, 0x3A, 255);
    static readonly Color KRIM = new Color32(0xFF, 0xF7, 0xE6, 255);
    static readonly Color KUNING = new Color32(0xFF, 0xC9, 0x2E, 255);
    static readonly Color HIJAU = new Color32(0x2E, 0xC4, 0x6A, 255);
    static readonly Color ORANYE = new Color32(0xFF, 0x8A, 0x2B, 255);
    static readonly Color BIRU = new Color32(0x3F, 0xA9, 0xF5, 255);
    static readonly Color PINK = new Color32(0xFF, 0x5F, 0xA2, 255);
    static readonly Color MERAH = new Color32(0xFF, 0x5B, 0x5B, 255);
    static readonly Color UNGU = new Color32(0x8B, 0x5C, 0xF6, 255);
    static readonly Color TEKS = new Color32(0x3A, 0x2A, 0x4A, 255);
    const float SUDUT_SPRITE = 44f;

    Sprite spBulat, spLingkaran, spBintang, spSegitiga;

    void PerbaruiHud()
    {
        if (indeks >= pesanan.Count)
        {
            teksNomor.text = "OK";
            teksPesanan.text = "<size=22><color=#8A7A99>Driver: Anas</color></size>\n<b>Semua pesanan\nselesai!</b>";
            teksUang.text = $"Rp {pendapatan:N0}".Replace(',', '.');
            return;
        }
        var p = pesanan[indeks];
        teksNomor.text = $"{indeks + 1}/{pesanan.Count}";
        teksPesanan.text = membawa
            ? $"<size=22><color=#8A7A99>Driver: Anas</color></size>\nAntar <color=#FF7A1A><b>{p.makanan}</b></color>\nke <b>{p.namaRumah}</b>"
            : $"<size=22><color=#8A7A99>Driver: Anas</color></size>\nAmbil <color=#FF7A1A><b>{p.makanan}</b></color>\ndi <b>{p.namaToko}</b>";
        teksUang.text = $"Rp {pendapatan:N0}".Replace(',', '.');
        var tujuan = membawa ? p.titikAntar : p.titikToko;
        if (tujuan != null) teksJarak.text = $"{Mathf.RoundToInt(Vector3.Distance(Datar(pemain.position), Datar(tujuan.position)))} m";
    }

    void Notif(string teks)
    {
        Main(suaraNotif);
        teksNotif.text = teks;
        StopCoroutine("NotifAnim");
        StartCoroutine("NotifAnim");
    }

    IEnumerator NotifAnim()
    {
        var t = grupNotif.transform;
        for (float u = 0f; u < 1f; u += Time.deltaTime / 0.35f)
        {
            grupNotif.alpha = Mathf.Clamp01(u * 3f);
            t.localScale = Vector3.one * EaseOutBack(u);
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(u * 20f) * (1f - u) * 6f);
            yield return null;
        }
        grupNotif.alpha = 1f; t.localScale = Vector3.one; t.localRotation = Quaternion.identity;
        yield return new WaitForSeconds(2.4f);
        for (float u = 1f; u > 0f; u -= Time.deltaTime / 0.25f)
        {
            grupNotif.alpha = u; t.localScale = Vector3.one * (0.6f + 0.4f * u);
            yield return null;
        }
        grupNotif.alpha = 0f;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        x = Mathf.Clamp01(x);
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    IEnumerator Pop(Transform t, float durasi = 0.3f)
    {
        for (float u = 0f; u < 1f; u += Time.deltaTime / durasi)
        {
            t.localScale = Vector3.one * EaseOutBack(u);
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    static Color WarnaNama(string n)
    {
        if (n == "Anas") return UNGU;
        if (n.Contains("Aplikasi")) return HIJAU;
        if (n.Contains("Dodi")) return BIRU;
        return ORANYE;
    }

    void BuatHud()
    {
        BuatSprite();
        var go = new GameObject("HUD Antar Cepat", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
        var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        var akar = go.transform;
        hudAkar = akar;

        // ---- kartu pesanan (kiri atas)
        var kartu = Panel(akar, "Kartu Pesanan", new Vector2(0, 1), new Vector2(40, -86), new Vector2(580, 204), KRIM);
        var pita = Panel(kartu, "Pita Judul", new Vector2(0, 1), new Vector2(-12, 26), new Vector2(290, 64), HIJAU, 22f);
        pita.localRotation = Quaternion.Euler(0, 0, 4f);
        Teks(pita, "ANTAR CEPAT", 38, Color.white, TextAlignmentOptions.Center, Vector2.zero, true, 0.25f);
        var lencana = Panel(kartu, "Lencana", new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(124, 124), KUNING, 0f, 7f, spLingkaran);
        teksNomor = Teks(lencana, "", 44, Color.white, TextAlignmentOptions.Center, Vector2.zero, true, 0.3f);
        teksPesanan = Teks(Wadah(kartu, "Isi", new Vector2(0, 1), new Vector2(166, -30), new Vector2(400, 112)), "", 30, TEKS, TextAlignmentOptions.TopLeft, Vector2.zero);
        teksPesanan.lineSpacing = -8f;
        var chipJarak = Panel(kartu, "Chip Jarak", new Vector2(0, 0), new Vector2(166, 18), new Vector2(150, 48), BIRU, 24f, 5f);
        teksJarak = Teks(chipJarak, "", 28, Color.white, TextAlignmentOptions.Center, Vector2.zero, true, 0.22f);
        var chipUang = Panel(kartu, "Chip Uang", new Vector2(0, 0), new Vector2(330, 18), new Vector2(224, 48), ORANYE, 24f, 5f);
        teksUang = Teks(chipUang, "", 28, Color.white, TextAlignmentOptions.Center, Vector2.zero, true, 0.22f);
        // ---- batas waktu antar (di bawah kartu pesanan)
        chipWaktu = Panel(akar, "Chip Waktu", new Vector2(0, 1), new Vector2(60, -300), new Vector2(300, 64), HIJAU, 30f, 6f);
        chipWaktu.pivot = new Vector2(0.5f, 0.5f); chipWaktu.anchoredPosition = new Vector2(210, -332);
        isiChipWaktu = chipWaktu.Find("Isi").GetComponent<Image>();
        teksWaktu = Teks(chipWaktu, "", 32, Color.white, TextAlignmentOptions.Center, Vector2.zero, true, 0.25f);
        chipWaktu.gameObject.SetActive(false);

        // ---- notifikasi (atas tengah)
        var notif = Panel(akar, "Notif", new Vector2(0.5f, 1), new Vector2(40, -36), new Vector2(640, 88), KUNING, 44f, 7f);
        grupNotif = notif.gameObject.AddComponent<CanvasGroup>(); grupNotif.alpha = 0f; grupNotif.blocksRaycasts = false;
        teksNotif = Teks(notif, "", 38, Color.white, TextAlignmentOptions.Center, new Vector2(24, 0), true, 0.3f);
        teksNotif.textWrappingMode = TextWrappingModes.NoWrap; teksNotif.enableAutoSizing = true; teksNotif.fontSizeMin = 22; teksNotif.fontSizeMax = 38;

        // ---- prompt aksi (bawah tengah)
        promptRt = Panel(akar, "Prompt", new Vector2(0.5f, 0), new Vector2(0, 320), new Vector2(660, 88), Color.white, 44f, 6f);
        var kunci = Panel(promptRt, "Kunci E", new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(64, 64), HIJAU, 0f, 5f, spLingkaran);
        Teks(kunci, "E", 38, Color.white, TextAlignmentOptions.Center, Vector2.zero, true, 0.25f);
        teksPrompt = Teks(Wadah(promptRt, "Label", new Vector2(0, 0.5f), new Vector2(96, 0), new Vector2(540, 80)), "", 32, TEKS, TextAlignmentOptions.Left, Vector2.zero);
        panelPrompt = promptRt.gameObject; panelPrompt.SetActive(false);

        // ---- dialog (kiri bawah)
        var dlg = Panel(akar, "Dialog", new Vector2(0, 0), new Vector2(40, 40), new Vector2(1300, 250), KRIM);
        var tag = Panel(dlg, "Pita Nama", new Vector2(0, 1), new Vector2(36, 38), new Vector2(330, 68), PINK, 22f);
        tag.localRotation = Quaternion.Euler(0, 0, 3f);
        isiPitaNama = tag.Find("Isi").GetComponent<Image>();
        teksNama = Teks(tag, "", 36, Color.white, TextAlignmentOptions.Center, new Vector2(10, 0), true, 0.25f);
        teksNama.textWrappingMode = TextWrappingModes.NoWrap;
        teksDialog = Teks(Wadah(dlg, "Teks", new Vector2(0, 1), new Vector2(46, -52), new Vector2(1200, 150)), "", 36, TEKS, TextAlignmentOptions.TopLeft, Vector2.zero);
        Teks(Wadah(dlg, "Lanjut", new Vector2(1, 0), new Vector2(-92, 14), new Vector2(240, 44)), "lanjut  [E]", 24, new Color32(0x8A, 0x7A, 0x99, 255), TextAlignmentOptions.Right, Vector2.zero);
        panahLanjut = Wadah(dlg, "Panah", new Vector2(1, 0), new Vector2(-40, 22), new Vector2(40, 32));
        Lapis(panahLanjut, "Garis", spSegitiga, GARIS, Vector2.zero, -4f, 1f);
        Lapis(panahLanjut, "Isi", spSegitiga, ORANYE, Vector2.zero, 2f, 1f);
        var btnDlg = dlg.gameObject.AddComponent<Button>(); btnDlg.transition = Selectable.Transition.None; btnDlg.onClick.AddListener(() => lanjutDitekan = true);
        dlg.Find("Garis").GetComponent<Image>().raycastTarget = true;
        panelDialog = dlg.gameObject; panelDialog.SetActive(false);

        // ---- tombol layar sentuh (kanan bawah)
        tombolAksiRt = Tombol(akar, "AKSI", new Vector2(-60, 60), HIJAU, () => tombolAksi = true);
        tombolLompat = Tombol(akar, "LOMPAT", new Vector2(-270, 60), ORANYE, () => { if (lompat != null) lompat.MintaLompat(); }).gameObject;
        tombolKlakson = Tombol(akar, "KLAKSON", new Vector2(-270, 60), BIRU, () => klaksonDiminta = true).gameObject;
        tombolKlakson.SetActive(false);

        // ---- panel akhir
        var redup = Kotak(akar, "Panel Akhir", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.12f, 0.06f, 0.18f, 0.55f));
        redup.anchorMin = Vector2.zero; redup.anchorMax = Vector2.one; redup.sizeDelta = Vector2.zero;
        redup.GetComponent<Image>().raycastTarget = true;
        kartuAkhir = Panel(redup, "Kartu Akhir", new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(940, 720), KRIM, 40f, 8f);
        var judul = Panel(kartuAkhir, "Pita Judul", new Vector2(0.5f, 1), new Vector2(0, 50), new Vector2(780, 104), HIJAU, 30f, 8f);
        judul.localRotation = Quaternion.Euler(0, 0, -2f);
        Teks(judul, "HARI PERTAMA SELESAI!", 58, Color.white, TextAlignmentOptions.Center, Vector2.zero, true, 0.28f);
        bintang = new RectTransform[5];
        for (int i = 0; i < 5; i++)
        {
            var b = Wadah(kartuAkhir, "Bintang " + (i + 1), new Vector2(0.5f, 1), new Vector2(-220 + i * 110, -100), new Vector2(100, 100));
            b.pivot = new Vector2(0.5f, 0.5f); b.anchoredPosition = new Vector2(-220 + i * 110, -140);
            Lapis(b, "Bayangan", spBintang, new Color(0, 0, 0, 0.25f), new Vector2(0, -6), 0f, 1f);
            Lapis(b, "Garis", spBintang, GARIS, Vector2.zero, 0f, 1f);
            Lapis(b, "Isi", spBintang, KUNING, Vector2.zero, 9f, 1f);
            b.localScale = Vector3.zero;
            bintang[i] = b;
        }
        teksAkhir = Teks(Wadah(kartuAkhir, "Isi", new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(840, 260)), "", 34, TEKS, TextAlignmentOptions.Center, Vector2.zero);
        teksAkhir.enableAutoSizing = true; teksAkhir.fontSizeMin = 22; teksAkhir.fontSizeMax = 34;
        Teks(Wadah(kartuAkhir, "Terima Kasih", new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(800, 50)), "Terima kasih sudah bermain ANTAR CEPAT!", 28, new Color32(0x8A, 0x7A, 0x99, 255), TextAlignmentOptions.Center, Vector2.zero);
        AntarUI.fontJudul = fontJudul; AntarUI.fontIsi = fontIsi;
        tombolAkhir = new[]
        {
            (RectTransform)AntarUI.Tombol(kartuAkhir, "MAIN LAGI", new Vector2(0.5f, 0), new Vector2(-195, 40), new Vector2(360, 100), HIJAU,
                () => AntarPindah.Ke(AntarPindah.SCENE_GAME), 42).transform,
            (RectTransform)AntarUI.Tombol(kartuAkhir, "MENU UTAMA", new Vector2(0.5f, 0), new Vector2(195, 40), new Vector2(360, 100), ORANYE,
                () => AntarPindah.Ke(AntarPindah.SCENE_MENU), 42).transform,
        };
        foreach (var t in tombolAkhir) t.localScale = Vector3.zero;
        panelAkhir = redup.gameObject; panelAkhir.SetActive(false);
        BuatPanelGps(akar);
        PerbaruiHud();
    }

    /// <summary>Balon sapaan warga dibuat bergaya kartun juga (krem, garis tepi gelap, font bulat).</summary>
    void GayaBalonWarga()
    {
        foreach (var w in FindObjectsByType<NPCAvatarController>(FindObjectsInactive.Include))
        {
            var cv = w.GetComponentInChildren<Canvas>(true);
            if (cv == null) continue;
            if (cv.GetComponent<AntarBalon>() == null) cv.gameObject.AddComponent<AntarBalon>().skalaDasar = cv.transform.localScale.x;
            foreach (var img in cv.GetComponentsInChildren<Image>(true))
            {
                var r = img.rectTransform;
                var ukuran = new Vector2(r.rect.width * r.localScale.x, r.rect.height * r.localScale.y);
                r.localScale = Vector3.one; r.sizeDelta = ukuran;
                img.sprite = spBulat; img.type = Image.Type.Sliced; img.color = KRIM;
                img.pixelsPerUnitMultiplier = SUDUT_SPRITE / 180f;
                var o = img.gameObject.AddComponent<Outline>(); o.effectColor = GARIS; o.effectDistance = new Vector2(28f, -28f);
                var b = img.gameObject.AddComponent<Shadow>(); b.effectColor = new Color(0.1f, 0.04f, 0.15f, 0.35f); b.effectDistance = new Vector2(0f, -70f);
            }
            foreach (var tx in cv.GetComponentsInChildren<Text>(true))
            {
                if (fontBalon != null) tx.font = fontBalon;
                tx.color = TEKS;
            }
        }
    }

    // ---------- pembuat elemen ----------
    RectTransform Wadah(Transform induk, string nama, Vector2 jangkar, Vector2 pos, Vector2 ukuran)
    {
        var g = new GameObject(nama, typeof(RectTransform));
        var r = g.GetComponent<RectTransform>(); r.SetParent(induk, false);
        r.anchorMin = r.anchorMax = r.pivot = jangkar;
        r.anchoredPosition = pos; r.sizeDelta = ukuran;
        return r;
    }

    RectTransform Kotak(Transform induk, string nama, Vector2 jangkar, Vector2 pos, Vector2 ukuran, Color warna)
    {
        var r = Wadah(induk, nama, jangkar, pos, ukuran);
        var img = r.gameObject.AddComponent<Image>(); img.color = warna; img.raycastTarget = false;
        return r;
    }

    /// <summary>Panel kartun: bayangan + garis tepi gelap + isi berwarna + kilap.</summary>
    RectTransform Panel(Transform induk, string nama, Vector2 jangkar, Vector2 pos, Vector2 ukuran, Color isi,
                        float radius = 30f, float tebal = 6f, Sprite bentuk = null)
    {
        var w = Wadah(induk, nama, jangkar, pos, ukuran);
        bool bulat = bentuk == null;
        var s = bulat ? spBulat : bentuk;
        float mLuar = bulat ? SUDUT_SPRITE / Mathf.Max(radius, 4f) : 1f;
        float mDalam = bulat ? SUDUT_SPRITE / Mathf.Max(radius - tebal, 4f) : 1f;
        Lapis(w, "Bayangan", s, new Color(0.1f, 0.04f, 0.15f, 0.3f), new Vector2(0, -9), 0f, mLuar);
        Lapis(w, "Garis", s, GARIS, Vector2.zero, 0f, mLuar);
        Lapis(w, "Isi", s, isi, Vector2.zero, tebal, mDalam);
        var kilap = Lapis(w, "Kilap", s, new Color(1f, 1f, 1f, 0.28f), Vector2.zero, tebal + 5f, mDalam);
        var rk = kilap.rectTransform; rk.anchorMin = new Vector2(0f, 0.55f);
        return w;
    }

    Image Lapis(RectTransform induk, string nama, Sprite s, Color c, Vector2 geser, float sisip, float pengali)
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

    TextMeshProUGUI Teks(RectTransform induk, string isi, float ukuran, Color warna, TextAlignmentOptions rata, Vector2 tepi,
                         bool judul = false, float garis = 0f)
    {
        var g = new GameObject("Teks", typeof(RectTransform), typeof(TextMeshProUGUI));
        var r = g.GetComponent<RectTransform>(); r.SetParent(induk, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(tepi.x, 2); r.offsetMax = new Vector2(-tepi.x, -2);
        var t = g.GetComponent<TextMeshProUGUI>();
        var f = judul ? fontJudul : fontIsi;
        if (f != null) t.font = f;
        t.text = isi; t.fontSize = ukuran; t.color = warna; t.alignment = rata; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        if (garis > 0f)
        {
            var m = t.fontMaterial; // instance khusus teks ini
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

    RectTransform Tombol(Transform induk, string label, Vector2 pos, Color warna, UnityEngine.Events.UnityAction klik)
    {
        var r = Panel(induk, "Tombol " + label, new Vector2(1, 0), pos, new Vector2(180, 180), warna, 0f, 8f, spLingkaran);
        r.Find("Garis").GetComponent<Image>().raycastTarget = true;
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = r.Find("Isi").GetComponent<Image>();
        var cb = b.colors; cb.pressedColor = new Color(0.75f, 0.75f, 0.75f); cb.highlightedColor = new Color(1f, 1f, 1f); b.colors = cb;
        b.onClick.AddListener(klik);
        Teks(r, label, label.Length > 4 ? 34 : 42, Color.white, TextAlignmentOptions.Center, Vector2.zero, true, 0.28f);
        return r;
    }

    // ---------- sprite dibuat dari kode (tidak perlu file gambar) ----------
    void BuatSprite()
    {
        spBulat = SpriteBulat(128, (int)SUDUT_SPRITE, true);
        spLingkaran = SpriteBulat(256, 128, false);
        var bt = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
            float rr = (i % 2 == 0) ? 0.49f : 0.235f;
            bt[i] = new Vector2(0.5f + Mathf.Cos(a) * rr, 0.47f + Mathf.Sin(a) * rr);
        }
        spBintang = SpritePoligon(256, bt);
        spSegitiga = SpritePoligon(64, new[] { new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.92f), new Vector2(0.5f, 0.06f) });
    }

    static Texture2D TeksturBaru(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
        return t;
    }

    static Sprite SpriteBulat(int n, int r, bool iris)
    {
        var tex = TeksturBaru(n); var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float px0 = x + 0.5f, py0 = y + 0.5f;
                float cx = Mathf.Clamp(px0, r, n - r), cy = Mathf.Clamp(py0, r, n - r);
                float d = Mathf.Sqrt((px0 - cx) * (px0 - cx) + (py0 - cy) * (py0 - cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        var tepi = iris ? new Vector4(r + 2, r + 2, r + 2, r + 2) : Vector4.zero;
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, tepi);
    }

    static Sprite SpritePoligon(int n, Vector2[] titik)
    {
        var tex = TeksturBaru(n); var px = new Color32[n * n];
        const int S = 4;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int masuk = 0;
                for (int sy = 0; sy < S; sy++)
                    for (int sx = 0; sx < S; sx++)
                        if (DiDalam(new Vector2((x + (sx + 0.5f) / S) / n, (y + (sy + 0.5f) / S) / n), titik)) masuk++;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * masuk / (S * S)));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool DiDalam(Vector2 p, Vector2[] v)
    {
        bool dalam = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            if (((v[i].y > p.y) != (v[j].y > p.y)) && (p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x))
                dalam = !dalam;
        return dalam;
    }

    void BuatTasOjol()
    {
        // tas ojol hijau di punggung
        var tas = GameObject.CreatePrimitive(PrimitiveType.Cube); tas.name = "Tas Ojol";
        Destroy(tas.GetComponent<Collider>());
        tas.transform.SetParent(pemain, false);
        tas.transform.localPosition = new Vector3(0f, 1.25f, -0.38f);
        tas.transform.localScale = new Vector3(0.55f, 0.55f, 0.4f);
        tas.GetComponent<Renderer>().material.color = warnaUtama;
        var pita = GameObject.CreatePrimitive(PrimitiveType.Cube); pita.name = "Pita";
        Destroy(pita.GetComponent<Collider>());
        pita.transform.SetParent(tas.transform, false);
        pita.transform.localPosition = new Vector3(0f, 0.15f, -0.51f); pita.transform.localScale = new Vector3(1.02f, 0.18f, 0.02f);
        pita.GetComponent<Renderer>().material.color = Color.white;

        // kemasan makanan di tangan (muncul saat membawa pesanan)
        kemasan = GameObject.CreatePrimitive(PrimitiveType.Cube); kemasan.name = "Kemasan Makanan";
        Destroy(kemasan.GetComponent<Collider>());
        kemasan.transform.SetParent(pemain, false);
        kemasan.transform.localPosition = new Vector3(0f, 1.0f, 0.42f);
        kemasan.transform.localScale = new Vector3(0.42f, 0.28f, 0.3f);
        pegangKiri = new GameObject("Pegang Kiri").transform; pegangKiri.SetParent(kemasan.transform, false); pegangKiri.localPosition = new Vector3(-0.62f, -0.1f, 0f);
        pegangKanan = new GameObject("Pegang Kanan").transform; pegangKanan.SetParent(kemasan.transform, false); pegangKanan.localPosition = new Vector3(0.62f, -0.1f, 0f);
        kemasan.SetActive(false);
    }

    void BuatPenanda()
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = "Penanda Tujuan";
        Destroy(g.GetComponent<Collider>());
        g.transform.localScale = new Vector3(0.7f, 0.08f, 0.7f);
        var panah = GameObject.CreatePrimitive(PrimitiveType.Cube); panah.name = "Panah";
        Destroy(panah.GetComponent<Collider>());
        panah.transform.SetParent(g.transform, false);
        panah.transform.localPosition = new Vector3(0f, 6f, 0f);
        panah.transform.localScale = new Vector3(0.5f, 8f, 0.5f);
        panah.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(0.2f, 1f, 0.5f);
        g.GetComponent<Renderer>().material = mat; panah.GetComponent<Renderer>().material = mat;
        penanda = g.transform;
        penanda.gameObject.SetActive(false);
    }
}
