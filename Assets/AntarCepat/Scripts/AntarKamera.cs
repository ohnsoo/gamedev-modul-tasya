using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Kamera orang ketiga (TPP) di belakang Anas.
/// - Geser layar (sentuh atau klik + seret mouse) untuk memutar kamera, seperti game mobile.
///   Kamera hanya berputar kalau digeser; arah jalan Anas mengikuti arah kamera.
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
        pitch = sudutAwal;
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
            pitch = Mathf.Clamp(pitch - geser.y * k, 4f, 55f);
        }

        float jarakIngin = skuter ? jarakSkuter : jarak;
        float sudut = skuter ? Mathf.Max(pitch, 20f) : pitch;
        // pivot diperhalus sedikit supaya lompat/turun-naik tidak menyentak
        Vector3 pivotTarget = target.position + Vector3.up * tinggiLihat;
        pivotHalus = Vector3.Lerp(pivotHalus, pivotTarget, 1f - Mathf.Exp(-14f * dt));
        pivotHalus.x = pivotTarget.x; pivotHalus.z = pivotTarget.z;

        Quaternion rot = Quaternion.Euler(sudut, yaw, 0f);
        Vector3 mundur = rot * Vector3.back;

        // cek halangan (abaikan pemain, NPC, trigger, dan benda bergerak)
        float boleh = jarakIngin;
        var hits = Physics.SphereCastAll(pivotHalus, radiusTabrakan, mundur, jarakIngin, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider.transform.root == target.root) continue;
            if (h.collider.attachedRigidbody != null) continue;
            if (h.collider.GetComponentInParent<AntarNpc>() != null) continue;
            if (h.distance <= 0f) continue;
            boleh = Mathf.Min(boleh, Mathf.Max(h.distance - 0.15f, 1.2f));
        }
        float cepat = boleh < jarakSekarang ? 18f : 3f;   // maju cepat, mundur pelan
        jarakSekarang = Mathf.Lerp(jarakSekarang, boleh, 1f - Mathf.Exp(-cepat * dt));

        transform.position = pivotHalus + mundur * jarakSekarang;
        transform.rotation = Quaternion.LookRotation(pivotHalus - transform.position);
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
