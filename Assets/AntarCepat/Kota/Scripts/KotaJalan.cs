using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Jaringan jalan kota Antar Cepat (lajur kiri seperti di Indonesia).
/// Node = titik tengah persimpangan / tikungan, edge = arah yang boleh dilewati (a -> b).
/// Persimpangan dipesan per gerakan (dari -> lewat -> ke): gerakan yang lintasannya tidak bersilangan boleh
/// jalan bersamaan, yang bersilangan bergantian. Mobil yang sudah lama menunggu didahulukan.
/// Juga menyimpan zebra cross (dengan status dipakai / diminta pejalan kaki) dan graf trotoar untuk warga.
/// </summary>
public class KotaJalan : MonoBehaviour
{
    public static KotaJalan Instance;

    [Header("Graf jalan")]
    public Vector3[] nodes;
    public Vector2Int[] edges;
    public float[] nodeRadius;
    public KotaLampu[] lampu;               // per node (null = tanpa lampu)
    public float laneOffset = 3f;
    public float lebarSetengah = 6f;        // setengah lebar badan jalan
    public float tinggiJalan = 0.12f, tinggiTrotoar = 0.27f;

    [Header("Zebra cross")]
    public int[] zebraNode;
    public int[] zebraArm;
    public Vector3[] zebraPos;
    public Vector3[] zebraAxis;             // arah lengan jalan (dari node ke luar)

    [Header("Graf trotoar")]
    public Vector3[] pedNodes;
    public Vector3Int[] pedEdges;           // a, b, indeks zebra (-1 = trotoar biasa)

    [HideInInspector] public List<KotaMobil> cars = new List<KotaMobil>();

    class Pesanan { public KotaMobil car; public int from, next; }
    class Antrean { public int from, next; public float sejak; public bool siap; }

    readonly Dictionary<int, List<Pesanan>> pesanan = new Dictionary<int, List<Pesanan>>();
    readonly Dictionary<int, Dictionary<KotaMobil, Antrean>> antre = new Dictionary<int, Dictionary<KotaMobil, Antrean>>();
    readonly Dictionary<long, bool> cacheKonflik = new Dictionary<long, bool>();
    readonly List<KotaMobil> hapusAntre = new List<KotaMobil>();
    List<int>[] outs;
    HashSet<int>[] tetangga;

    // status zebra
    int[] zebraDipakai;
    float[] zebraDimintaSampai;
    Dictionary<long, int> zebraDari;
    [HideInInspector] public List<int>[] pedAdj;

    void Awake() { Instance = this; Build(); }

    public void Build()
    {
        outs = new List<int>[nodes.Length];
        tetangga = new HashSet<int>[nodes.Length];
        for (int i = 0; i < nodes.Length; i++) { outs[i] = new List<int>(); tetangga[i] = new HashSet<int>(); }
        foreach (var e in edges) { outs[e.x].Add(e.y); tetangga[e.x].Add(e.y); tetangga[e.y].Add(e.x); }
        int nz = zebraPos != null ? zebraPos.Length : 0;
        zebraDipakai = new int[nz];
        zebraDimintaSampai = new float[nz];
        zebraDari = new Dictionary<long, int>();
        for (int i = 0; i < nz; i++) zebraDari[(long)zebraNode[i] * 1000 + zebraArm[i]] = i;
        pedAdj = new List<int>[pedNodes != null ? pedNodes.Length : 0];
        for (int i = 0; i < pedAdj.Length; i++) pedAdj[i] = new List<int>();
        if (pedEdges != null)
            for (int k = 0; k < pedEdges.Length; k++) { pedAdj[pedEdges[k].x].Add(k); pedAdj[pedEdges[k].y].Add(k); }
    }

    public int NextNode(int from, int at)
    {
        if (outs == null) Build();
        var options = new List<int>();
        foreach (var n in outs[at]) if (n != from) options.Add(n);
        if (options.Count == 0) return from;
        return options[Random.Range(0, options.Count)];
    }

    // ---------------- Zebra ----------------
    /// <summary>Indeks zebra di node 'at' pada lengan menuju 'arm' (-1 = tidak ada).</summary>
    public int Zebra(int at, int arm)
    {
        if (zebraDari == null) Build();
        int z;
        return zebraDari.TryGetValue((long)at * 1000 + arm, out z) ? z : -1;
    }
    public bool ZebraDipakai(int z) { return z >= 0 && zebraDipakai[z] > 0; }
    public bool ZebraDiminta(int z) { return z >= 0 && Time.time < zebraDimintaSampai[z]; }
    public void MasukZebra(int z) { if (z >= 0) zebraDipakai[z]++; }
    public void KeluarZebra(int z) { if (z >= 0) zebraDipakai[z] = Mathf.Max(0, zebraDipakai[z] - 1); }
    public void MintaZebra(int z) { if (z >= 0) zebraDimintaSampai[z] = Time.time + 0.3f; }

    /// <summary>Apakah titik p berada di atas badan jalan (aspal), bukan trotoar.</summary>
    public bool DiBadanJalan(Vector3 p, float margin = 0f)
    {
        float batas = lebarSetengah + margin;
        foreach (var e in edges)
        {
            if (e.x > e.y) continue;
            if (JarakRuas(p, nodes[e.x], nodes[e.y]) <= batas) return true;
        }
        foreach (var n in nodes)
            if (Mathf.Abs(p.x - n.x) <= batas && Mathf.Abs(p.z - n.z) <= batas) return true;
        return false;
    }

    static float JarakRuas(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a; ab.y = 0f; var ap = p - a; ap.y = 0f;
        float L = ab.sqrMagnitude;
        float t = L > 0f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / L) : 0f;
        // ruas berujung rata (tidak membulat) di node
        if (t <= 0f || t >= 1f) return 999f;
        var q = ap - ab * t;
        return q.magnitude;
    }

    public float TinggiPermukaan(Vector3 p) { return DiBadanJalan(p) ? tinggiJalan : tinggiTrotoar; }

    // ---------------- Pemesanan persimpangan ----------------
    public void Antre(int at, KotaMobil car, int from, int next, bool siap)
    {
        Dictionary<KotaMobil, Antrean> d;
        if (!antre.TryGetValue(at, out d)) antre[at] = d = new Dictionary<KotaMobil, Antrean>();
        Antrean a;
        if (!d.TryGetValue(car, out a)) d[car] = a = new Antrean { sejak = Time.time };
        a.from = from; a.next = next; a.siap = siap;
    }

    public void KeluarAntre(int at, KotaMobil car)
    {
        Dictionary<KotaMobil, Antrean> d;
        if (antre.TryGetValue(at, out d)) d.Remove(car);
    }

    public bool TryReserve(int at, KotaMobil car, int from, int next)
    {
        List<Pesanan> list;
        if (!pesanan.TryGetValue(at, out list)) pesanan[at] = list = new List<Pesanan>();
        list.RemoveAll(p => p.car == null || !p.car.isActiveAndEnabled);
        foreach (var p in list) if (p.car == car) { p.from = from; p.next = next; return true; }
        foreach (var p in list)
            if (Konflik(at, from, next, p.from, p.next)) return false;

        Dictionary<KotaMobil, Antrean> d;
        if (antre.TryGetValue(at, out d))
        {
            Antrean saya;
            float skorSaya = d.TryGetValue(car, out saya) ? Time.time - saya.sejak : 0f;
            hapusAntre.Clear();
            foreach (var kv in d)
            {
                if (kv.Key == null || !kv.Key.isActiveAndEnabled) { hapusAntre.Add(kv.Key); continue; }
                if (kv.Key == car) continue;
                if (kv.Value.siap && Time.time - kv.Value.sejak > skorSaya + 2.5f && Konflik(at, from, next, kv.Value.from, kv.Value.next)) return false;
            }
            foreach (var c in hapusAntre) d.Remove(c);
            d.Remove(car);
        }
        list.Add(new Pesanan { car = car, from = from, next = next });
        return true;
    }

    public void Release(int at, KotaMobil car)
    {
        List<Pesanan> list;
        if (pesanan.TryGetValue(at, out list)) list.RemoveAll(p => p.car == car);
        KeluarAntre(at, car);
    }

    public bool Konflik(int at, int f1, int n1, int f2, int n2)
    {
        if (f1 == f2) return n1 != n2;
        long key = ((((long)at * 64 + f1) * 64 + n1) * 64 + f2) * 64 + n2;
        bool hasil;
        if (cacheKonflik.TryGetValue(key, out hasil)) return hasil;
        Vector3 a0, a1, a2, b0, b1, b2;
        Kurva(f1, at, n1, out a0, out a1, out a2);
        Kurva(f2, at, n2, out b0, out b1, out b2);
        hasil = false;
        const int N = 14;
        for (int i = 0; i <= N && !hasil; i++)
        {
            var pa = Bez(a0, a1, a2, i / (float)N);
            for (int j = 0; j <= N; j++)
            {
                var pb = Bez(b0, b1, b2, j / (float)N);
                pa.y = pb.y = 0f;
                if ((pa - pb).sqrMagnitude < 2.6f * 2.6f) { hasil = true; break; }
            }
        }
        cacheKonflik[key] = hasil;
        return hasil;
    }

    public void Kurva(int from, int at, int next, out Vector3 p0, out Vector3 p1, out Vector3 p2)
    {
        p0 = SegEnd(from, at);
        p2 = SegStart(at, next);
        var d1 = Dir(from, at);
        var d2 = Dir(at, next);
        if (Vector3.Dot(d1, d2) < -0.9f) { p1 = (p0 + p2) * 0.5f + d1 * 5f; return; }
        var w = p2 - p0; w.y = 0f;
        float silang = d1.x * d2.z - d1.z * d2.x;
        if (Mathf.Abs(silang) < 0.05f) p1 = (p0 + p2) * 0.5f;
        else p1 = p0 + d1 * ((w.x * d2.z - w.z * d2.x) / silang);
        // belok kiri (pendek): titik kendali sedikit ditarik ke tengah supaya tidak memotong sudut trotoar
        if (silang > 0.5f)
        {
            var pusat = nodes[at]; pusat.y = p1.y;
            p1 = Vector3.Lerp(p1, pusat, 0.35f * Mathf.InverseLerp(0.5f, 1f, silang));
        }
    }

    public static Vector3 Bez(Vector3 p0, Vector3 p1, Vector3 p2, float u)
    {
        float v = 1f - u;
        return v * v * p0 + 2f * v * u * p1 + u * u * p2;
    }

    public static Vector3 Left(Vector3 d) { return new Vector3(-d.z, 0f, d.x); }

    public Vector3 Dir(int a, int b)
    {
        var d = nodes[b] - nodes[a];
        d.y = 0f;
        return d.normalized;
    }

    public bool IsTikungan(int n)
    {
        if (outs == null) Build();
        return lampu[n] == null && outs[n].Count == 2 && tetangga[n].Count == 2;
    }

    public float Radius(int n) { return nodeRadius != null && n < nodeRadius.Length && nodeRadius[n] > 0f ? nodeRadius[n] : 7f; }

    public float SudutBelok(int from, int at, int next) { return Vector3.Angle(Dir(from, at), Dir(at, next)); }

    public Vector3 SegStart(int a, int b)
    {
        var d = Dir(a, b);
        return nodes[a] + d * Radius(a) + Left(d) * laneOffset;
    }

    public Vector3 SegEnd(int a, int b)
    {
        var d = Dir(a, b);
        return nodes[b] - d * Radius(b) + Left(d) * laneOffset;
    }

    /// <summary>Titik di lajur (dari->ke) pada jarak s dari awal ruas.</summary>
    public Vector3 TitikRuas(int a, int b, float s)
    {
        var p0 = SegStart(a, b); var p1 = SegEnd(a, b);
        float L = Vector3.Distance(p0, p1);
        return Vector3.Lerp(p0, p1, L > 0f ? Mathf.Clamp01(s / L) : 0f);
    }

    void OnDrawGizmosSelected()
    {
        if (nodes == null || edges == null) return;
        Gizmos.color = Color.yellow;
        foreach (var n in nodes) Gizmos.DrawWireSphere(n, 1f);
        Gizmos.color = Color.cyan;
        foreach (var e in edges) Gizmos.DrawLine(SegStart(e.x, e.y) + Vector3.up * 0.4f, SegEnd(e.x, e.y) + Vector3.up * 0.4f);
        if (pedNodes != null && pedEdges != null)
        {
            foreach (var e in pedEdges)
            {
                Gizmos.color = e.z >= 0 ? Color.blue : new Color(1f, 0.6f, 0f);
                Gizmos.DrawLine(pedNodes[e.x] + Vector3.up * 0.5f, pedNodes[e.y] + Vector3.up * 0.5f);
            }
        }
    }
}
