using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC "Antar Cepat". Satu skrip untuk beberapa perilaku dasar:
/// - Diam   : berdiri / duduk di tempat (idle)
/// - Dialog : diam di tempat, menoleh ke pemain dan bisa diajak bicara (tombol E / AKSI)
/// - Patroli: berjalan bolak-balik melewati titik-titik patroli (pakai NavMeshAgent)
/// - Ikut   : mengikuti pemain dari belakang (pakai NavMeshAgent)
/// </summary>
public class AntarNpc : MonoBehaviour
{
    public enum Perilaku { Diam, Dialog, Patroli, Ikut }

    public string nama = "Warga";
    public Perilaku perilaku = Perilaku.Dialog;
    [TextArea(2, 4)] public string[] dialog;

    [Header("Patroli")]
    public Transform[] titikPatroli;
    public float jedaDiTitik = 1.5f;

    [Header("Ikut pemain")]
    public float jarakIkut = 2.2f;

    [Header("Animasi")]
    public bool duduk;

    [HideInInspector] public bool sedangBicara;

    NavMeshAgent agent;
    Animator anim;
    Transform pemain;
    Quaternion arahAwal;
    int titik;
    float tungguSampai;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        arahAwal = transform.rotation;
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) pemain = p.transform;
        if (anim != null)
        {
            if (Punya(anim, "Grounded_b")) anim.SetBool("Grounded_b", true);
            if (duduk && Punya(anim, "Sit")) anim.SetTrigger("Sit");
        }
        if (agent != null && (perilaku == Perilaku.Diam || perilaku == Perilaku.Dialog)) agent.enabled = false;
        if (perilaku == Perilaku.Patroli) MulaiPatroli();
        if (perilaku == Perilaku.Ikut) MulaiIkut();
    }

    void Update()
    {
        float kecepatan = 0f;
        if (sedangBicara && pemain != null)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            Hadapkan(pemain.position);
        }
        else switch (perilaku)
        {
            case Perilaku.Patroli:
                if (agent == null || !agent.enabled || titikPatroli == null || titikPatroli.Length == 0) break;
                agent.isStopped = false;
                if (!agent.pathPending && agent.remainingDistance < 0.4f)
                {
                    if (tungguSampai <= 0f) tungguSampai = Time.time + jedaDiTitik;
                    else if (Time.time > tungguSampai)
                    {
                        titik = (titik + 1) % titikPatroli.Length;
                        agent.SetDestination(titikPatroli[titik].position);
                        tungguSampai = 0f;
                    }
                }
                kecepatan = agent.velocity.magnitude;
                break;
            case Perilaku.Ikut:
                if (agent == null || !agent.enabled || pemain == null) break;
                agent.isStopped = false;
                float jarak = Vector3.Distance(transform.position, pemain.position);
                if (jarak > jarakIkut) agent.SetDestination(pemain.position - pemain.forward * 1.2f);
                else agent.ResetPath();
                // tertinggal jauh (mis. terhalang): pindah ke dekat pemain
                if (jarak > 25f) agent.Warp(pemain.position - pemain.forward * 1.5f);
                kecepatan = agent.velocity.magnitude;
                break;
            case Perilaku.Dialog:
            case Perilaku.Diam:
                transform.rotation = Quaternion.RotateTowards(transform.rotation, arahAwal, 240f * Time.deltaTime);
                break;
        }
        if (anim != null && Punya(anim, "Speed_f")) anim.SetFloat("Speed_f", kecepatan);
    }

    static bool Punya(Animator a, string nama)
    {
        foreach (var p in a.parameters) if (p.name == nama) return true;
        return false;
    }

    void Hadapkan(Vector3 titikLihat)
    {
        var d = titikLihat - transform.position; d.y = 0f;
        if (d.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
    }

    /// <summary>Mulai mengikuti pemain (dipakai untuk Dodi setelah diajak bicara).</summary>
    public void MulaiIkut()
    {
        perilaku = Perilaku.Ikut;
        if (agent != null)
        {
            agent.enabled = true;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas)) agent.Warp(hit.position);
        }
    }

    public void Lambai()
    {
        if (anim != null && Punya(anim, "Wave")) anim.SetTrigger("Wave");
    }

    public void MulaiPatroli()
    {
        if (agent != null && titikPatroli != null && titikPatroli.Length > 0)
        {
            agent.enabled = true;
            agent.SetDestination(titikPatroli[0].position);
        }
    }
}
