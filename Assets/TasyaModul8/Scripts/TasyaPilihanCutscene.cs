using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Mengatur apakah cutscene (kamera sinematik) di Activity1Start diputar.
/// - Pasang di scene modul (mis. modul 4 / modul 5) dengan mode Setel: memilih cutscene dipakai atau tidak
///   untuk scene berikutnya (Activity1Start) yang dimasuki lewat portal.
/// - Pasang di Activity1Start (objek "Modul 8 - Cutscene") dengan mode Terapkan: kalau modul sebelumnya
///   memilih tanpa cutscene, semua Timeline cutscene dihentikan dan kamera langsung mengikuti pemain.
/// Kalau Activity1Start dimainkan langsung (tanpa lewat modul), cutscene tetap diputar seperti biasa.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class TasyaPilihanCutscene : MonoBehaviour
{
    public enum Mode { Setel, Terapkan }

    public Mode mode = Mode.Setel;
    [Tooltip("Mode Setel: scene tujuan memutar cutscene (kamera sinematik) atau tidak")]
    public bool pakaiCutscene = true;

    /// <summary>null = belum dipilih (pakai perilaku bawaan scene).</summary>
    public static bool? Pilihan;

    bool matikan;

    void Awake()
    {
        if (mode == Mode.Setel) { Pilihan = pakaiCutscene; return; }

        matikan = Pilihan.HasValue && !Pilihan.Value;
        Pilihan = null;   // hanya berlaku sekali
        if (!matikan) return;
        foreach (var d in GetComponentsInChildren<PlayableDirector>(true)) { d.playOnAwake = false; d.Stop(); }
        foreach (var t in GetComponentsInChildren<Modul8CharacterTrigger>(true)) t.enabled = false;
    }

    void Start()
    {
        if (mode != Mode.Terapkan || !matikan) return;
        foreach (var d in GetComponentsInChildren<PlayableDirector>(true)) { d.Stop(); d.enabled = false; }
        foreach (var c in GetComponentsInChildren<Modul8Cutscene>(true))
        {
            if (c.chaseCamera != null) c.chaseCamera.enabled = true;
            c.enabled = false;
        }
        InputManager.bCanPlayerMove = true;
    }
}
