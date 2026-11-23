using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mobil yang berkeliling kota mengikuti KotaJalan (lajur kiri):
/// menjaga jarak dengan mobil di depan, mematuhi lampu lalu lintas, berhenti di garis berhenti (tidak di atas zebra),
/// memesan lintasan di persimpangan (yang tidak bersilangan boleh jalan bersamaan), tidak masuk persimpangan
/// kalau jalan keluarnya macet / zebra di depannya sedang dipakai, berhenti untuk pejalan kaki dan pemain,
/// dan membunyikan klakson kalau lama dihalangi pemain. Ada pengurai macet supaya persimpangan tidak saling mengunci.
/// </summary>
public class KotaMobil : MonoBehaviour
{
    public static readonly List<KotaMobil> Semua = new List<KotaMobil>();

    public KotaJalan traffic;
    public int fromNode, toNode;
    public float startProgress;
    public float maxSpeed = 9f;
    public float turnSpeed = 4.5f;
    public float panjang = 5.4f, lebar = 2.4f;
    public float mundurGarisBerhenti = 6f;
    public Transform[] roda;
    public float jariRoda = 0.36f;
    public AudioClip suaraKlakson;

    enum State { Road, Turn }

    State state;
    float s, len, t, turnLen, speed;
    Vector3 a, b, p0, p1, p2;
    int nextNode = -1;
    bool holding;
    Transform player;
    NavMeshAgent agenPemain;
    float diamLama, dihalangiPemain, klaksonLagi;
    AudioSource suara;

    public bool OnRoad(int f, int to) { return state == State.Road && fromNode == f && toNode == to; }
    public float Progress { get { return s; } }
    public float Speed { get { return speed; } }
    public bool InTurn(int f, int to, int n) { return state == State.Turn && fromNode == f && toNode == to && nextNode == n; }
    public float TurnDone { get { return t * turnLen; } }

    static int uidCounter;
    int uid;

    void OnEnable()
    {
        uid = ++uidCounter;
        Semua.Add(this);
        if (traffic != null && !traffic.cars.Contains(this)) traffic.cars.Add(this);
    }

    void OnDisable()
    {
        Semua.Remove(this);
        if (traffic == null) return;
        traffic.cars.Remove(this);
        traffic.Release(toNode, this);
        holding = false;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { player = p.transform; agenPemain = p.GetComponent<NavMeshAgent>(); }
        suara = GetComponent<AudioSource>();
        EnterRoad(fromNode, toNode, startProgress);
        Place(Vector3.Lerp(a, b, s / len), b - a, 1f, true);
    }

    void EnterRoad(int f, int to, float s0)
    {
        fromNode = f; toNode = to;
        a = traffic.SegStart(f, to);
        b = traffic.SegEnd(f, to);
        len = Mathf.Max(0.1f, Flat(b - a).magnitude);
        s = Mathf.Clamp(s0, 0f, len);
        state = State.Road;
        nextNode = -1;
    }

    void StartTurn(float carry)
    {
        if (nextNode < 0) nextNode = traffic.NextNode(fromNode, toNode);
        traffic.Kurva(fromNode, toNode, nextNode, out p0, out p1, out p2);
        turnLen = 0f;
        Vector3 prev = p0;
        for (int i = 1; i <= 10; i++) { var q = Bez(i / 10f); turnLen += Flat(q - prev).magnitude; prev = q; }
        turnLen = Mathf.Max(0.5f, turnLen);
        t = carry / turnLen;
        state = State.Turn;
    }

    void LateUpdate()
    {
        if (traffic == null) return;
        diamLama = speed < 0.2f ? diamLama + Time.deltaTime : 0f;
        DorongPemain();
    }

    float Jarak(KotaMobil o) { return (panjang + o.panjang) * 0.5f + 1.6f; }

    void Update()
    {
        if (traffic == null) return;
        float dt = Time.deltaTime;
        float target = maxSpeed;

        // pengurai macet: mobil yang sudah di dalam persimpangan dan tertahan lama tetap jalan pelan
        bool urai = diamLama > 3f && (state == State.Turn || holding);
        float gap = GapAhead();
        if (!urai) target = Mathf.Min(target, Mathf.Max(0f, (gap - 2.5f) * 1.4f));
        bool halangan = SomeoneAhead();
        if (halangan) target = 0f;
        if (urai && !halangan) target = Mathf.Max(Mathf.Min(target, turnSpeed), 2.5f);

        if (state == State.Road)
        {
            float remain = len - s;
            float leader = LeaderProgress();
            KotaMobil depan = LeaderCar();
            bool first = depan == null;
            if (holding && !first && remain > mundurGarisBerhenti + 0.3f) { traffic.Release(toNode, this); holding = false; }
            if (nextNode < 0 && remain < 18f) nextNode = traffic.NextNode(fromNode, toNode);
            if (!holding && remain < 12f && traffic.IsTikungan(toNode)) holding = true;
            bool simpang = !traffic.IsTikungan(toNode);
            bool masihSempatBerhenti = remain - mundurGarisBerhenti > speed * speed / 12f + 1f;
            if (holding && simpang && remain > mundurGarisBerhenti + 0.3f && (!ExitClear() || (!LightAllows(remain) && masihSempatBerhenti))) { traffic.Release(toNode, this); holding = false; }
            if (simpang && !holding && first && nextNode >= 0 && remain < 16f)
                traffic.Antre(toNode, this, fromNode, nextNode, LightAllows(remain) && ExitClear() && ZebraKeluarBebas());
            if (!holding && remain < 12f + mundurGarisBerhenti)
            {
                if (first && nextNode >= 0 && LightAllows(remain) && ExitClear() && ZebraKeluarBebas() && traffic.TryReserve(toNode, this, fromNode, nextNode)) holding = true;
                else target = Mathf.Min(target, Mathf.Max(0f, (remain - mundurGarisBerhenti) * 1.3f));
            }
            if (nextNode >= 0) target = Mathf.Min(target, KecepatanBelok() + remain * 0.45f);

            speed = Mathf.MoveTowards(speed, target, (target > speed ? 2.6f : 9f) * dt);
            float sLalu = s;
            s += speed * dt;
            float garis = len - mundurGarisBerhenti;
            if (!holding && simpang && sLalu <= garis && s > garis) { s = garis; speed = 0f; }
            if (!first && s > leader - Jarak(depan)) { s = Mathf.Min(s, Mathf.Max(leader - Jarak(depan), sLalu)); speed = Mathf.Min(speed, depan.speed); }
            if (s >= len)
            {
                if (holding) StartTurn(s - len);
                else s = len;
            }
            if (state == State.Road) Place(Vector3.Lerp(a, b, s / len), b - a, dt, false);
        }
        if (state == State.Turn)
        {
            target = Mathf.Min(target, KecepatanBelok());
            speed = Mathf.MoveTowards(speed, target, (target > speed ? 2.6f : 9f) * dt);
            t += speed * dt / turnLen;
            if (t >= 1f)
            {
                traffic.Release(toNode, this);
                holding = false;
                int f = toNode;
                EnterRoad(f, nextNode, (t - 1f) * turnLen);
                Place(Vector3.Lerp(a, b, s / len), b - a, dt, false);
            }
            else Place(Bez(t), BezDir(t), dt, false);
        }

        // roda berputar
        if (roda != null)
        {
            float deg = speed * dt / Mathf.Max(0.1f, jariRoda) * Mathf.Rad2Deg;
            foreach (var r in roda) if (r != null) r.Rotate(-deg, 0f, 0f, Space.Self);
        }
        // suara mesin mengikuti kecepatan
        if (suara != null)
        {
            float k = Mathf.Clamp01(speed / maxSpeed);
            suara.pitch = Mathf.Lerp(suara.pitch, 0.7f + 0.6f * k, dt * 3f);
            suara.volume = Mathf.Lerp(suara.volume, 0.25f + 0.45f * k, dt * 3f);
        }
        // klakson kalau lama dihalangi pemain
        if (halangan && dihalangiPemain > 2.5f && Time.time > klaksonLagi && suaraKlakson != null)
        {
            AudioSource.PlayClipAtPoint(suaraKlakson, transform.position + Vector3.up, 0.8f);
            klaksonLagi = Time.time + Random.Range(4f, 7f);
        }
    }

    float KecepatanBelok()
    {
        if (nextNode < 0) return turnSpeed;
        float sudut = traffic.SudutBelok(fromNode, toNode, nextNode);
        if (sudut > 150f) return turnSpeed * 0.7f;
        return Mathf.Lerp(maxSpeed * 0.9f, turnSpeed, Mathf.InverseLerp(20f, 70f, sudut));
    }

    // Hijau: boleh. Kuning: hanya kalau sudah terlalu dekat untuk berhenti dengan nyaman. Merah: tidak.
    bool LightAllows(float remain)
    {
        var l = traffic.lampu != null && toNode < traffic.lampu.Length ? traffic.lampu[toNode] : null;
        if (l == null) return true;
        var w = l.Mobil(traffic.Dir(fromNode, toNode));
        if (w == KotaLampu.Warna.Hijau) return true;
        if (w == KotaLampu.Warna.Kuning) return remain - mundurGarisBerhenti < speed * speed / 12f && speed > 3f;
        return false;
    }

    bool IsAhead(KotaMobil o) { return o.s > s || (Mathf.Approximately(o.s, s) && o.uid < uid); }

    KotaMobil LeaderCar()
    {
        float best = 999f; KotaMobil hasil = null;
        foreach (var o in traffic.cars)
            if (o != null && o != this && o.OnRoad(fromNode, toNode) && IsAhead(o) && o.s < best) { best = o.s; hasil = o; }
        return hasil;
    }

    float LeaderProgress() { var c = LeaderCar(); return c != null ? c.s : 999f; }

    bool ExitClear()
    {
        if (nextNode < 0) return true;
        float panjangKeluar = Flat(traffic.SegEnd(toNode, nextNode) - traffic.SegStart(toNode, nextNode)).magnitude;
        float ruang = panjangKeluar - (traffic.IsTikungan(nextNode) ? 0f : mundurGarisBerhenti);
        float pakai = 0f;
        foreach (var o in traffic.cars)
        {
            if (o == null || o == this) continue;
            if (o.OnRoad(toNode, nextNode))
            {
                if (o.s < o.panjang * 0.5f + 1.5f && o.speed < 1.5f) return false;
                if (!o.holding) pakai += o.panjang + 1.6f;
            }
            else if (o.state == State.Turn && o.toNode == toNode && o.nextNode == nextNode) pakai += o.panjang + 1.6f;
        }
        return ruang - pakai >= panjang + 1f;
    }

    /// <summary>Mobil ini sudah berkomitmen melewati zebra 'z' (berbelok / segera masuk persimpangan ke lengan zebra itu).</summary>
    public bool AkanLewat(int z)
    {
        if (traffic == null || nextNode < 0 || z < 0) return false;
        if (traffic.zebraNode[z] == toNode)
        {
            bool keLengan = traffic.zebraArm[z] == nextNode;
            bool dariLengan = traffic.zebraArm[z] == fromNode;
            if (state == State.Turn && keLengan) return true;
            if (state == State.Road && keLengan && holding) return true;
            if (state == State.Road && dariLengan && (holding || (len - s < mundurGarisBerhenti + 6f && speed > 2f))) return true;
        }
        // baru saja keluar persimpangan dan masih di atas zebra lengan keluar
        if (state == State.Road && traffic.zebraNode[z] == fromNode && traffic.zebraArm[z] == toNode && s < 5f) return true;
        return false;
    }

    bool ZebraKeluarBebas()
    {
        if (nextNode < 0 || traffic.IsTikungan(toNode)) return true;
        int z = traffic.Zebra(toNode, nextNode);
        if (z < 0) return true;
        if (traffic.ZebraDipakai(z)) return false;
        // warga yang menunggu hanya didahulukan bila lampu penyeberangnya memang hijau
        var l = traffic.lampu[toNode];
        bool bolehSeberang = l == null || l.Penyeberang(traffic.zebraAxis[z]) == KotaLampu.Warna.Hijau;
        return !(bolehSeberang && traffic.ZebraDiminta(z));
    }

    float GapAhead()
    {
        float best = 999f;
        foreach (var o in traffic.cars)
        {
            if (o == null || o == this) continue;
            float jarakBadan = (panjang + o.panjang) * 0.5f;
            if (state == State.Road)
            {
                if (o.OnRoad(fromNode, toNode) && IsAhead(o)) best = Mathf.Min(best, o.s - s - jarakBadan);
                else if (nextNode >= 0 && o.InTurn(fromNode, toNode, nextNode)) best = Mathf.Min(best, (len - s) + o.TurnDone - jarakBadan);
                else if (nextNode >= 0 && o.OnRoad(toNode, nextNode)) best = Mathf.Min(best, (len - s) + 10f + o.s - jarakBadan);
            }
            else if (o.InTurn(fromNode, toNode, nextNode) && o.t > t) best = Mathf.Min(best, (o.t - t) * turnLen - jarakBadan);
            else if (o.OnRoad(toNode, nextNode)) best = Mathf.Min(best, (1f - t) * turnLen + o.s - jarakBadan);
        }
        return best;
    }

    bool SomeoneAhead()
    {
        var pos = transform.position;
        var fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        float depan = panjang * 0.5f;
        foreach (var w in KotaWarga.Semua)
            if (w != null && !w.SedangMenunggu && Blocks(w.transform.position - pos, fwd, depan, 1.4f) && traffic.DiBadanJalan(w.transform.position, -0.2f)) return true;
        bool pemain = player != null && Blocks(player.position - pos, fwd, depan, lebar * 0.5f + 0.6f) && traffic.DiBadanJalan(player.position, 0.3f);
        dihalangiPemain = pemain && speed < 0.3f ? dihalangiPemain + Time.deltaTime : 0f;
        return pemain;
    }

    static bool Blocks(Vector3 v, Vector3 fwd, float depan, float lebarSisi)
    {
        v.y = 0f;
        float f = Vector3.Dot(v, fwd);
        if (f < depan - 0.5f || f > depan + 6.5f) return false;
        return Mathf.Abs(Vector3.Dot(v, KotaJalan.Left(fwd))) < lebarSisi;
    }

    // Mobil terasa padat: pemain yang menabrak badan mobil didorong keluar
    void DorongPemain()
    {
        if (player == null || agenPemain == null || !agenPemain.enabled || !agenPemain.isOnNavMesh) return;
        var lokal = transform.InverseTransformPoint(player.position);
        float hx = lebar * 0.5f + 0.45f, hz = panjang * 0.5f + 0.45f;
        if (lokal.y > 2.5f || Mathf.Abs(lokal.x) >= hx || Mathf.Abs(lokal.z) >= hz) return;
        float dx = hx - Mathf.Abs(lokal.x), dz = hz - Mathf.Abs(lokal.z);
        Vector3 dorong = dx < dz ? new Vector3(Mathf.Sign(lokal.x) * dx, 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(lokal.z) * dz);
        var dunia = transform.TransformVector(dorong); dunia.y = 0f;
        agenPemain.Move(dunia);
    }

    void Place(Vector3 p, Vector3 dir, float dt, bool langsung)
    {
        float y = traffic.tinggiJalan;
        transform.position = new Vector3(p.x, y, p.z);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            var r = Quaternion.LookRotation(dir);
            transform.rotation = langsung ? r : Quaternion.Slerp(transform.rotation, r, Mathf.Clamp01(dt * 12f));
        }
    }

    Vector3 Bez(float u) { float v = 1f - u; return v * v * p0 + 2f * v * u * p1 + u * u * p2; }
    Vector3 BezDir(float u) { return 2f * (1f - u) * (p1 - p0) + 2f * u * (p2 - p1); }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
