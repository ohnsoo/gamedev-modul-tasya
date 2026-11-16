using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Kontrol Anas untuk kamera orang ketiga (TPP).
/// WASD / panah = jalan relatif terhadap arah kamera, Shift = jalan pelan.
/// Gerak memakai percepatan & perlambatan supaya tidak kaku, dan badan berbelok halus.
/// Saat naik skuter: lebih cepat, akselerasi lebih lembut, dan badan sedikit miring saat belok.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AntarPemain : MonoBehaviour
{
    public float kecepatanLari = 4.6f;
    public float kecepatanJalan = 1.8f;
    public float kecepatanSkuter = 9f;
    public Transform kamera;

    [HideInInspector] public bool naikSkuter;
    [HideInInspector] public Vector2 masukanLuar;
    [HideInInspector] public Transform gripKiri, gripKanan;   // pegangan stang skuter
    Animator anim;
    float bobotIK;   // masukan tambahan (mis. joystick layar / pengujian)

    NavMeshAgent agent;
    Vector3 vel;
    float miring;

    public Vector3 Kecepatan => vel;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        if (agent.isOnNavMesh) agent.ResetPath();
        if (kamera == null && Camera.main != null) kamera = Camera.main.transform;
        anim = GetComponent<Animator>();
        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector2 masuk = Vector2.zero;
        if (InputManager.bCanPlayerMove)
            masuk = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) + masukanLuar;

        Vector3 depan = kamera != null ? kamera.forward : Vector3.forward; depan.y = 0f; depan.Normalize();
        Vector3 kanan = new Vector3(depan.z, 0f, -depan.x);
        Vector3 arah = depan * masuk.y + kanan * masuk.x;
        if (arah.sqrMagnitude > 1f) arah.Normalize();

        float maks = naikSkuter ? kecepatanSkuter : (Input.GetKey(KeyCode.LeftShift) ? kecepatanJalan : kecepatanLari);
        Vector3 target = arah * maks;
        float percepatan = naikSkuter ? 7f : 16f;
        float rem = naikSkuter ? 9f : 20f;

        if (naikSkuter && vel.sqrMagnitude > 0.5f && target.sqrMagnitude > 0.01f)
        {
            // skuter tidak bisa bergeser ke samping: arahkan kecepatan mengikuti badan, lalu belokkan pelan
            Vector3 hadap = transform.forward; hadap.y = 0f;
            Vector3 baru = Vector3.RotateTowards(hadap, target.normalized, 2.6f * dt, 0f);
            float laju = Mathf.MoveTowards(vel.magnitude, maks * Mathf.Clamp01(Vector3.Dot(baru, target.normalized) * 0.5f + 0.6f), percepatan * dt);
            vel = baru * laju;
        }
        else
        {
            vel = Vector3.MoveTowards(vel, target, (target.sqrMagnitude > vel.sqrMagnitude ? percepatan : rem) * dt);
        }

        if (agent.enabled && agent.isOnNavMesh && vel.sqrMagnitude > 0.0001f)
        {
            Vector3 sebelum = transform.position;
            agent.Move(vel * dt);
            // tertahan tembok: kurangi kecepatan supaya tidak "mendorong" terus
            Vector3 nyata = (transform.position - sebelum) / Mathf.Max(dt, 0.0001f); nyata.y = 0f;
            if (nyata.magnitude < vel.magnitude * 0.3f) vel = Vector3.Lerp(vel, nyata, 0.5f);
        }

        // belok halus + miring di atas skuter
        Vector3 hadapBaru = transform.forward; hadapBaru.y = 0f;
        float yawLama = transform.eulerAngles.y;
        if (vel.sqrMagnitude > 0.05f)
        {
            float kecepatanBelok = naikSkuter ? 9f : 12f;
            hadapBaru = Vector3.Slerp(hadapBaru.normalized, vel.normalized, 1f - Mathf.Exp(-kecepatanBelok * dt));
        }
        float yawBaru = Quaternion.LookRotation(hadapBaru.sqrMagnitude > 0.001f ? hadapBaru : transform.forward).eulerAngles.y;
        float lajuBelok = Mathf.DeltaAngle(yawLama, yawBaru) / Mathf.Max(dt, 0.0001f);
        float targetMiring = naikSkuter ? Mathf.Clamp(-lajuBelok * 0.12f * Mathf.Clamp01(vel.magnitude / 4f), -14f, 14f) : 0f;
        miring = Mathf.Lerp(miring, targetMiring, 1f - Mathf.Exp(-6f * dt));
        transform.rotation = Quaternion.Euler(0f, yawBaru, miring);
    }

    // tangan memegang stang saat naik skuter (butuh "IK Pass" aktif di layer Animator)
    void OnAnimatorIK(int layer)
    {
        if (anim == null) return;
        bobotIK = Mathf.MoveTowards(bobotIK, naikSkuter && gripKiri != null ? 1f : 0f, Time.deltaTime * 4f);
        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, bobotIK);
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, bobotIK);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, bobotIK * 0.5f);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, bobotIK * 0.5f);
        if (bobotIK > 0f && gripKiri != null)
        {
            anim.SetIKPosition(AvatarIKGoal.LeftHand, gripKiri.position);
            anim.SetIKPosition(AvatarIKGoal.RightHand, gripKanan.position);
            anim.SetIKRotation(AvatarIKGoal.LeftHand, gripKiri.rotation);
            anim.SetIKRotation(AvatarIKGoal.RightHand, gripKanan.rotation);
        }
    }

    public void Berhenti()
    {
        vel = Vector3.zero;
        if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.velocity = Vector3.zero; }
    }

    /// <summary>Hadapkan badan ke arah tertentu (dipakai saat bicara / mengetuk pintu).</summary>
    public void Hadap(Vector3 arah)
    {
        arah.y = 0f;
        if (arah.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(arah);
    }
}
