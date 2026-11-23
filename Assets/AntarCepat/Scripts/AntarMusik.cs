using UnityEngine;

/// <summary>Musik latar yang diputar berulang, dengan fade-in di awal. Dipakai di menu utama dan di dalam game.</summary>
[RequireComponent(typeof(AudioSource))]
public class AntarMusik : MonoBehaviour
{
    public AudioClip musik;
    [Range(0f, 1f)] public float volume = 0.35f;
    public float fadeIn = 2.5f;
    [Tooltip("Volume musik diturunkan saat objek ini aktif (mis. panel dialog)")]
    public GameObject redamSaatAktif;
    [Range(0f, 1f)] public float volumeRedam = 0.6f;

    AudioSource src;
    float t;
    bool sudahCari;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.clip = musik;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
        src.priority = 0;
    }

    void Start() { if (musik != null) src.Play(); }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        // panel dialog Antar Cepat dibuat saat game berjalan: cari sekali kalau belum diisi
        if (redamSaatAktif == null && !sudahCari && t > 0.5f)
        {
            sudahCari = true;
            var game = FindAnyObjectByType<AntarGame>();
            if (game != null)
            {
                var f = typeof(AntarGame).GetField("panelDialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (f != null) redamSaatAktif = f.GetValue(game) as GameObject;
            }
        }
        float k = fadeIn > 0f ? Mathf.Clamp01(t / fadeIn) : 1f;
        float target = volume * k * (redamSaatAktif != null && redamSaatAktif.activeInHierarchy ? volumeRedam : 1f);
        src.volume = Mathf.MoveTowards(src.volume, target, Time.unscaledDeltaTime * 0.6f);
    }
}
