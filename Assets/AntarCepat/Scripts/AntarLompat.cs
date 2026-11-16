using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lompatan pemain (tombol Spasi atau tombol LOMPAT di layar).
/// Pemain bergerak dengan NavMeshAgent, jadi lompatan dibuat dengan menaikkan
/// baseOffset agent mengikuti lengkung parabola, sambil memutar animasi lompat.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AntarLompat : MonoBehaviour
{
    public float tinggi = 1.3f;
    public float durasi = 0.65f;
    public AudioClip suaraLompat;

    NavMeshAgent agent;
    Animator anim;
    float offsetAwal;
    bool melompat;
    bool diminta;

    public bool SedangMelompat => melompat;
    [HideInInspector] public bool dikunci;   // mis. saat naik skuter

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        offsetAwal = agent.baseOffset;
        if (anim != null) anim.SetBool("Grounded_b", true);
    }

    /// <summary>Dipanggil tombol LOMPAT di layar.</summary>
    public void MintaLompat() { diminta = true; }

    void Update()
    {
        bool tekan = Input.GetKeyDown(KeyCode.Space) || diminta;
        diminta = false;
        if (tekan && !melompat && !dikunci && InputManager.bCanPlayerMove) StartCoroutine(Lompat());
    }

    IEnumerator Lompat()
    {
        melompat = true;
        if (suaraLompat) AudioSource.PlayClipAtPoint(suaraLompat, transform.position, 0.7f);
        if (anim != null) { anim.SetBool("Jump_b", true); anim.SetBool("Grounded_b", false); }
        for (float t = 0f; t < durasi; t += Time.deltaTime)
        {
            float u = t / durasi;
            agent.baseOffset = offsetAwal + 4f * tinggi * u * (1f - u);
            if (u > 0.15f && anim != null) anim.SetBool("Jump_b", false);
            yield return null;
        }
        agent.baseOffset = offsetAwal;
        if (anim != null) { anim.SetBool("Jump_b", false); anim.SetBool("Grounded_b", true); }
        melompat = false;
    }
}
