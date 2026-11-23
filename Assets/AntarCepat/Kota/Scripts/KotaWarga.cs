using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pejalan kaki kota. Berjalan menyusuri graf trotoar (lajur kiri searah jalan, tidak menginjak badan jalan),
/// memilih belokan secara acak, dan hanya menyeberang di zebra cross:
/// berhenti di tepi, menoleh kiri-kanan, menunggu lampu penyeberang hijau dan tidak ada mobil yang terlanjur lewat,
/// lalu menyeberang (bergegas kalau lampu mulai berkedip). Menjaga jarak dengan warga lain dan memberi jalan pada pemain.
/// </summary>
public class KotaWarga : MonoBehaviour
{
    public static readonly List<KotaWarga> Semua = new List<KotaWarga>();

    public KotaJalan jalan;
    public int edge;                 // indeks pedEdges
    public bool maju = true;         // dari x ke y
    public float progres;            // 0..1
    public float kecepatan = 1.35f;
    public float lajurSamping = 0.6f;
    public float animJalan = 0.3f, animLari = 0.8f;
    [Tooltip("Pengali kecepatan animasi jalan supaya langkah kaki pas dengan laju")]
    public float skalaAnim = 1f;

    enum Langkah { Jalan, Menunggu, Menyeberang }
    Langkah langkah;
    Animator anim;
    int hashSpeed;
    Transform pemain;
    float tunggu, menolehMulai, jedaReaksi, diamPemain;
    float lajuSekarang, y;
    int zebra = -1;
    int edgeLalu = -1;

    public bool SedangMenunggu { get { return langkah == Langkah.Menunggu; } }

    void OnEnable() { Semua.Add(this); }
    void OnDisable()
    {
        Semua.Remove(this);
        if (langkah == Langkah.Menyeberang && jalan != null) jalan.KeluarZebra(zebra);
    }

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
            hashSpeed = Animator.StringToHash("Speed_f");
            foreach (var p in anim.parameters) if (p.name == "Grounded_b") anim.SetBool("Grounded_b", true);
        }
        var pl = GameObject.FindGameObjectWithTag("Player");
        if (pl != null) pemain = pl.transform;
        lajurSamping = Random.Range(0.25f, 1.0f);
        kecepatan *= Random.Range(0.85f, 1.15f);
        y = jalan.tinggiTrotoar;
        if (jalan.pedEdges[edge].z >= 0) { edge = CariTrotoar(); }
        transform.position = Posisi();
        var d = Arah(); if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
    }

    int CariTrotoar()
    {
        for (int i = 0; i < jalan.pedEdges.Length; i++) if (jalan.pedEdges[(edge + i) % jalan.pedEdges.Length].z < 0) return (edge + i) % jalan.pedEdges.Length;
        return edge;
    }

    Vector3 Awal { get { var e = jalan.pedEdges[edge]; return jalan.pedNodes[maju ? e.x : e.y]; } }
    Vector3 Akhir { get { var e = jalan.pedEdges[edge]; return jalan.pedNodes[maju ? e.y : e.x]; } }
    Vector3 Arah() { var d = Akhir - Awal; d.y = 0f; return d.normalized; }
    float Panjang { get { var d = Akhir - Awal; d.y = 0f; return Mathf.Max(0.1f, d.magnitude); } }

    // lajur kiri searah jalan supaya yang berpapasan tidak bertabrakan
    Vector3 Posisi()
    {
        var p = Vector3.Lerp(Awal, Akhir, progres) + KotaJalan.Left(Arah()) * (langkah == Langkah.Menyeberang ? lajurSamping * 1.5f : lajurSamping);
        return p;
    }

    void Update()
    {
        if (jalan == null) return;
        float dt = Time.deltaTime;
        float target = kecepatan;
        var dir = Arah();

        if (langkah == Langkah.Menunggu)
        {
            target = 0f;
            jalan.MintaZebra(zebra);
            var lampu = jalan.lampu[jalan.zebraNode[zebra]];
            bool hijau = lampu == null || lampu.Penyeberang(jalan.zebraAxis[zebra]) == KotaLampu.Warna.Hijau;
            bool aman = hijau && !AdaMobilLewat();
            // menoleh kiri-kanan
            float tt = Time.time - menolehMulai;
            float noleh = tt < 0.7f ? -60f : tt < 1.4f ? 60f : 0f;
            var hadap = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, noleh, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, hadap, dt * 5f);
            if (!aman) { tunggu = 0f; if (tt > 4f && Random.value < dt * 0.3f) menolehMulai = Time.time; }
            else
            {
                tunggu += dt;
                if (tunggu > jedaReaksi && tt > 1.4f)
                {
                    langkah = Langkah.Menyeberang;
                    jalan.MasukZebra(zebra);
                }
            }
        }
        else
        {
            if (langkah == Langkah.Menyeberang)
            {
                target = kecepatan * 1.25f;
                var lampu = jalan.lampu[jalan.zebraNode[zebra]];
                if (lampu != null && lampu.Penyeberang(jalan.zebraAxis[zebra]) != KotaLampu.Warna.Hijau) target = kecepatan * 1.9f;   // bergegas
            }
            // jaga jarak dengan warga lain di depan
            foreach (var o in Semua)
            {
                if (o == this) continue;
                var v = o.transform.position - transform.position; v.y = 0f;
                float f = Vector3.Dot(v, dir);
                if (f > 0f && f < 1.3f && Mathf.Abs(Vector3.Dot(v, KotaJalan.Left(dir))) < 0.55f)
                {
                    if (Vector3.Dot(o.transform.forward, dir) > 0.3f) target = Mathf.Min(target, o.lajuSekarang * 0.95f);
                    else lajurSamping = Mathf.Min(1.1f, lajurSamping + dt);    // berpapasan: geser ke tepi
                }
            }
            // beri jalan pada pemain
            if (pemain != null)
            {
                var v = pemain.position - transform.position; v.y = 0f;
                float f = Vector3.Dot(v, dir);
                if (f > 0f && f < 1.4f && Mathf.Abs(Vector3.Dot(v, KotaJalan.Left(dir))) < 0.7f)
                {
                    diamPemain += dt;
                    if (diamPemain < 1.2f) target = 0f;
                    else lajurSamping = Mathf.Clamp(lajurSamping + (Vector3.Dot(v, KotaJalan.Left(dir)) > 0f ? -dt : dt) * 1.5f, -1.2f, 1.2f);
                }
                else diamPemain = 0f;
            }
        }

        lajuSekarang = Mathf.MoveTowards(lajuSekarang, target, dt * 4f);
        progres += lajuSekarang * dt / Panjang;
        if (progres >= 1f && langkah != Langkah.Menunggu) TibaDiTitik();

        var p = Posisi();
        float yt = jalan.TinggiPermukaan(p);
        y = Mathf.MoveTowards(y, yt, dt * 1.2f);
        p.y = y;
        transform.position = p;
        if (langkah != Langkah.Menunggu && lajuSekarang > 0.05f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 8f);

        if (anim != null)
        {
            float a = lajuSekarang < 0.05f ? 0f : (lajuSekarang > kecepatan * 1.6f ? animLari : animJalan);
            anim.SetFloat(hashSpeed, a);
            anim.speed = a > 0f && a < animLari ? Mathf.Clamp(lajuSekarang / kecepatan, 0.7f, 1.4f) * skalaAnim : 1f;
        }
    }

    bool AdaMobilLewat()
    {
        foreach (var c in KotaMobil.Semua) if (c != null && c.AkanLewat(zebra)) return true;
        // mobil yang sedang tepat di atas / sangat dekat dengan zebra
        var zp = jalan.zebraPos[zebra];
        foreach (var c in KotaMobil.Semua)
        {
            if (c == null) continue;
            var v = c.transform.position - zp; v.y = 0f;
            if (Mathf.Abs(Vector3.Dot(v, jalan.zebraAxis[zebra])) < 1.5f + c.panjang * 0.5f && v.magnitude < 7f) return true;
        }
        return false;
    }

    void TibaDiTitik()
    {
        int titik = maju ? jalan.pedEdges[edge].y : jalan.pedEdges[edge].x;
        if (langkah == Langkah.Menyeberang) { jalan.KeluarZebra(zebra); zebra = -1; langkah = Langkah.Jalan; }
        var pilihan = jalan.pedAdj[titik];
        int pilih = -1;
        // utamakan tidak berbalik arah; zebra dipilih sesekali saja
        for (int coba = 0; coba < 8 && pilih < 0; coba++)
        {
            int k = pilihan[Random.Range(0, pilihan.Count)];
            if (k == edge && pilihan.Count > 1) continue;
            if (jalan.pedEdges[k].z >= 0 && k == edgeLalu) continue;
            if (jalan.pedEdges[k].z >= 0 && Random.value < 0.55f) continue;
            pilih = k;
        }
        if (pilih < 0) pilih = pilihan[0] == edge && pilihan.Count > 1 ? pilihan[1] : pilihan[0];
        edgeLalu = edge;
        edge = pilih;
        maju = jalan.pedEdges[edge].x == titik;
        progres = 0f;
        if (jalan.pedEdges[edge].z >= 0)
        {
            zebra = jalan.pedEdges[edge].z;
            langkah = Langkah.Menunggu;
            tunggu = 0f;
            menolehMulai = Time.time;
            jedaReaksi = Random.Range(0.2f, 0.9f);
        }
    }
}
