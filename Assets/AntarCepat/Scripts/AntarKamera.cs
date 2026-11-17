using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Kamera orang ketiga (TPP) di belakang Anas.
/// - Geser layar (sentuh atau klik + seret mouse) untuk memutar kamera, seperti game mobile.
///   Saat jalan kaki kamera hanya berputar kalau digeser; arah jalan Anas mengikuti arah kamera.
///   Saat naik skuter kamera ikut ke belakang skuter.
/// - Kamera maju mendekat kalau terhalang tembok/gedung supaya Anas tetap terlihat.
/// - Saat naik skuter kamera sedikit lebih jauh & lebih tinggi.
/// </summary>
[DefaultExecutionOrder(200)]
public class AntarKamera : MonoBehaviour
{
    public Transform target;
    public float jarak = 8f;
    public float jarakSkuter = 10f;
    public float tinggiLihat = 2.7f;
    public float sudutAwal = 14f;
    [Tooltip("Derajat per piksel geseran (pada tinggi layar 1080)")]
    public float sensitivitas = 0.22f;
    public float ikutBelakang = 1.8f;
    public float radiusTabrakan = 0.35f;

    float yaw, pitch, jarakSekarang;
    int jari = -1;
    float sudutHalus = 14f, bobotDialog, sisi = 1f, sejakGeser = 99f;
    Transform lawanTerakhir, sisiDipilih;
    [HideInInspector] public Transform lawanBicara;   // diisi AntarGame saat dialog dengan NPC
    bool seretMouse;
    Vector2 mouseLalu;
    AntarPemain pemain;
    Vector3 pivotHalus;

    void Start()
    {
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        pemain = target.GetComponent<AntarPemain>();
        yaw = target.eulerAngles.y;
        pitch = sudutAwal; sudutHalus = sudutAwal;
        jarakSekarang = jarak;
        pivotHalus = target.position + Vector3.up * tinggiLihat;
        var lama = GetComponent<ChaseCamera>();
        if (lama != null) lama.enabled = false;
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Time.deltaTime;
        bool skuter = pemain != null && pemain.naikSkuter;

        // putar kamera dengan geseran layar / mouse
        Vector2 geser = BacaGeseran();
        if (geser != Vector2.zero)
        {
            float k = sensitivitas * 1080f / Mathf.Max(Screen.height, 1);
            yaw += geser.x * k;
            sejakGeser = 0f;
            pitch = Mathf.Clamp(pitch - geser.y * k, 4f, 55f);
        }

        // di atas skuter kamera ikut ke belakang skuter (kecuali baru saja digeser)
        sejakGeser += dt;
        if (skuter && sejakGeser > 1.2f)
            yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, 1f - Mathf.Exp(-3f * dt));

        float jarakIngin = skuter ? jarakSkuter : jarak;
        float sudutDasar = skuter ? Mathf.Max(pitch, 20f) : pitch;
        // pivot diperhalus sedikit supaya lompat/turun-naik tidak menyentak
        Vector3 pivotTarget = target.position + Vector3.up * tinggiLihat;
        pivotHalus = Vector3.Lerp(pivotHalus, pivotTarget, 1f - Mathf.Exp(-14f * dt));
        pivotHalus.x = pivotTarget.x; pivotHalus.z = pivotTarget.z;

        // kalau di belakang Anas ada gedung, kamera naik dan melihat dari atas bahu
        float sudut = sudutDasar;
        float boleh = JarakBebas(pivotHalus, Quaternion.Euler(sudut, yaw, 0f) * Vector3.back, jarakIngin);
        for (float tambah = 12f; boleh < jarakIngin * 0.75f && tambah <= 48f; tambah += 12f)
        {
            float s2 = Mathf.Min(sudutDasar + tambah, 72f);
            float b2 = JarakBebas(pivotHalus, Quaternion.Euler(s2, yaw, 0f) * Vector3.back, jarakIngin);
            if (b2 > boleh + 0.5f) { boleh = b2; sudut = s2; }
        }
        if (boleh < jarakIngin * 0.5f) sudut = Mathf.Max(sudut, sudutDasar + 30f);   // tetap sempit: lihat dari atas
        sudutHalus = Mathf.Lerp(sudutHalus, sudut, 1f - Mathf.Exp(-5f * dt));

        Vector3 mundur = Quaternion.Euler(sudutHalus, yaw, 0f) * Vector3.back;
        boleh = JarakBebas(pivotHalus, mundur, jarakIngin);
        float cepat = boleh < jarakSekarang ? 18f : 3f;   // maju cepat, mundur pelan
        jarakSekarang = Mathf.Lerp(jarakSekarang, boleh, 1f - Mathf.Exp(-cepat * dt));
        Vector3 posNormal = pivotHalus + mundur * jarakSekarang;
        Quaternion rotNormal = Quaternion.LookRotation(pivotHalus - posNormal);

        // saat bicara dengan NPC: kamera pindah ke samping supaya Anas & lawan bicara terlihat
        if (lawanBicara != null) lawanTerakhir = lawanBicara;
        bobotDialog = Mathf.MoveTowards(bobotDialog, lawanBicara != null ? 1f : 0f, dt * 2.2f);
        if (bobotDialog > 0.001f && lawanTerakhir != null)
        {
            Vector3 posD; Quaternion rotD;
            HitungShotDialog(out posD, out rotD);
            float w = Mathf.SmoothStep(0f, 1f, bobotDialog);
            transform.position = Vector3.Lerp(posNormal, posD, w);
            transform.rotation = Quaternion.Slerp(rotNormal, rotD, w);
        }
        else
        {
            sisiDipilih = null;
            transform.position = posNormal;
            transform.rotation = rotNormal;
        }
    }

    void HitungShotDialog(out Vector3 pos, out Quaternion rot)
    {
        Vector3 a = target.position, b = lawanTerakhir.position;
        Vector3 arah = b - a; arah.y = 0f;
        float jarakAB = arah.magnitude;
        arah = jarakAB > 0.1f ? arah / jarakAB : target.forward;
        Vector3 tengah = (a + b) * 0.5f + Vector3.up * 2.1f;
        Vector3 samping = Vector3.Cross(Vector3.up, arah);
        float jauh = Mathf.Clamp(jarakAB * 1.3f + 4.5f, 5.5f, 9f);
        if (sisiDipilih != lawanTerakhir)
        {
            // pilih sisi kiri/kanan yang paling tidak terhalang (sekali per percakapan)
            float kanan = JarakBebas(tengah, OffsetDialog(samping, arah, jauh, 1f).normalized, jauh);
            float kiri = JarakBebas(tengah, OffsetDialog(samping, arah, jauh, -1f).normalized, jauh);
            sisi = kanan >= kiri ? 1f : -1f;
            sisiDipilih = lawanTerakhir;
        }
        Vector3 ofs = OffsetDialog(samping, arah, jauh, sisi);
        float d = JarakBebas(tengah, ofs.normalized, ofs.magnitude);
        pos = tengah + ofs.normalized * d;
        rot = Quaternion.LookRotation(tengah - pos);
    }

    static Vector3 OffsetDialog(Vector3 samping, Vector3 arah, float jauh, float sisi)
    {
        return samping * sisi * jauh * 0.85f - arah * jauh * 0.4f + Vector3.up * 1.8f;
    }

    /// <summary>Jarak terjauh kamera dari titik asal ke arah tertentu sebelum menabrak sesuatu.</summary>
    float JarakBebas(Vector3 asal, Vector3 arah, float maks)
    {
        float boleh = maks;
        var hits = Physics.SphereCastAll(asal, radiusTabrakan, arah, maks, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider.transform.root == target.root) continue;
            if (h.collider.attachedRigidbody != null) continue;
            if (h.collider.GetComponentInParent<AntarNpc>() != null) continue;
            if (h.distance <= 0f) continue;
            boleh = Mathf.Min(boleh, Mathf.Max(h.distance - 0.15f, 1.6f));
        }
        return boleh;
    }

    Vector2 BacaGeseran()
    {
        Vector2 delta = Vector2.zero;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && jari < 0 && !DiAtasUI(t.fingerId)) jari = t.fingerId;
                if (t.fingerId != jari) continue;
                if (t.phase == TouchPhase.Moved) delta += t.deltaPosition;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) jari = -1;
            }
            return delta;
        }
        jari = -1;
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            seretMouse = !DiAtasUI(-1);
            mouseLalu = Input.mousePosition;
        }
        if (seretMouse && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
        {
            Vector2 m = Input.mousePosition;
            delta = m - mouseLalu;
            mouseLalu = m;
        }
        else seretMouse = false;
        return delta;
    }

    static bool DiAtasUI(int id)
    {
        if (EventSystem.current == null) return false;
        return id >= 0 ? EventSystem.current.IsPointerOverGameObject(id) : EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>Langsung taruh kamera di belakang target (dipakai saat mulai / naik skuter).</summary>
    public void KeBelakang() { if (target != null) yaw = target.eulerAngles.y; }
}
