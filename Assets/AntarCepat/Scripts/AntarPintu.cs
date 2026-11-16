using System.Collections;
using UnityEngine;

/// <summary>
/// Pintu rumah pelanggan. Saat Tasya mengetuk (Interaksi), pintu berayun terbuka
/// lalu pelanggan keluar ke teras.
/// </summary>
public class AntarPintu : MonoBehaviour
{
    [Tooltip("Daun pintu yang diputar (pivot = engsel)")]
    public Transform daunPintu;
    public float sudutBuka = -100f;
    public AntarNpc pelanggan;
    [Tooltip("Pelanggan berjalan sejauh ini keluar dari pintu")]
    public float jarakKeluar = 1.3f;
    public AudioClip suaraKetuk;
    public AudioClip suaraBuka;

    public bool Terbuka { get; private set; }

    void Start()
    {
        if (pelanggan != null) pelanggan.gameObject.SetActive(false);
    }

    public IEnumerator Buka()
    {
        if (Terbuka) yield break;
        Terbuka = true;
        if (suaraKetuk) AudioSource.PlayClipAtPoint(suaraKetuk, transform.position, 0.9f);
        yield return new WaitForSeconds(0.8f);
        if (suaraBuka) AudioSource.PlayClipAtPoint(suaraBuka, transform.position, 0.8f);
        var awal = daunPintu.localRotation;
        var akhir = awal * Quaternion.Euler(0f, sudutBuka, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            daunPintu.localRotation = Quaternion.Slerp(awal, akhir, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        daunPintu.localRotation = akhir;

        if (pelanggan != null)
        {
            // pelanggan muncul di ambang pintu lalu melangkah keluar
            var keluar = transform.forward;
            var dari = transform.position + Vector3.up * 0.0f;
            pelanggan.transform.position = dari;
            pelanggan.transform.rotation = Quaternion.LookRotation(keluar);
            pelanggan.gameObject.SetActive(true);
            var anim = pelanggan.GetComponentInChildren<Animator>();
            if (anim != null) { foreach (var pr in anim.parameters) if (pr.name == "Grounded_b") anim.SetBool("Grounded_b", true); anim.SetFloat("Speed_f", 1.5f); }
            var ke = dari + keluar * jarakKeluar;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                pelanggan.transform.position = Vector3.Lerp(dari, ke, t);
                yield return null;
            }
            if (anim != null) anim.SetFloat("Speed_f", 0f);
            pelanggan.Lambai();
        }
    }
}
