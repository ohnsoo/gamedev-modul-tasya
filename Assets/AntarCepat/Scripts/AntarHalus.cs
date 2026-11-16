using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Membuat gerak karakter (pemain & NPC) lebih halus:
/// - kecepatan animasi diambil dari gerak sebenarnya lalu diperhalus (tidak loncat-loncat),
/// - jalan pelan memakai animasi jalan, cepat memakai animasi lari,
/// - kecepatan putar kaki disesuaikan supaya kaki tidak "meluncur",
/// - NPC berbelok pelan mengikuti arah jalan (bukan berputar mendadak).
/// Dijalankan setelah skrip lain (execution order 100) supaya nilai Speed_f di sini yang dipakai.
/// </summary>
[DefaultExecutionOrder(100)]
public class AntarHalus : MonoBehaviour
{
    [Tooltip("Di bawah kecepatan ini (m/s) karakter memakai animasi jalan")]
    public float batasJalan = 2.4f;
    [Tooltip("Kecepatan asli animasi jalan & lari (m/s) untuk menyesuaikan langkah kaki")]
    public float langkahJalan = 1.6f, langkahLari = 4.6f;
    public float kehalusan = 8f;
    [Tooltip("Belokkan badan mengikuti arah jalan (untuk NPC)")]
    public bool putarIkutArah = true;
    public float kecepatanPutar = 7f;

    [HideInInspector] public bool paksaDiam;   // mis. saat berdiri di atas skuter

    Animator anim;
    NavMeshAgent agent;
    Vector3 posLalu;
    float v;
    bool punyaSpeed;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        if (anim != null)
        {
            anim.applyRootMotion = false;   // posisi diatur NavMeshAgent, bukan animasi
            foreach (var p in anim.parameters) if (p.name == "Speed_f") punyaSpeed = true;
        }
    }

    void Start()
    {
        posLalu = transform.position;
        if (agent != null && putarIkutArah)
        {
            agent.updateRotation = false;
            agent.acceleration = Mathf.Min(agent.acceleration, 6f);
        }
    }

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        var d = transform.position - posLalu; d.y = 0f;
        posLalu = transform.position;
        float mentah = Mathf.Min(d.magnitude / dt, 14f);
        if (d.magnitude > 3f) mentah = 0f;               // teleport/warp: abaikan
        v = Mathf.Lerp(v, mentah, 1f - Mathf.Exp(-kehalusan * dt));

        // belok halus mengikuti arah gerak agent
        if (putarIkutArah && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            var arah = agent.desiredVelocity; arah.y = 0f;
            if (arah.sqrMagnitude > 0.04f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(arah), 1f - Mathf.Exp(-kecepatanPutar * dt));
        }

        if (anim == null || !punyaSpeed) return;
        float nilai, laju = 1f;
        if (paksaDiam || v < 0.12f) { nilai = 0f; }
        else if (v < batasJalan)
        {
            nilai = Mathf.Lerp(0.15f, 0.45f, v / batasJalan);
            laju = Mathf.Clamp(v / langkahJalan, 0.6f, 1.5f);
        }
        else
        {
            nilai = Mathf.Lerp(0.6f, 1f, (v - batasJalan) / 4f);
            laju = Mathf.Clamp(v / langkahLari, 0.8f, 1.35f);
        }
        anim.SetFloat("Speed_f", nilai);
        anim.speed = Mathf.Lerp(anim.speed, laju, 1f - Mathf.Exp(-6f * dt));
    }

    /// <summary>Kecepatan gerak (m/s) yang sudah diperhalus.</summary>
    public float Kecepatan => v;
}
