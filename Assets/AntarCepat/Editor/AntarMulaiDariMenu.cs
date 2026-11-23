#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Tombol Play di Editor mengikuti scene yang sedang dibuka:
/// - Scene Antar Cepat (AntarCepat / MainMenu) -> game dimulai dari MainMenu (sama seperti hasil build).
/// - Scene lain (mis. "tasya modul 4") -> scene itu sendiri yang dimainkan, lalu alurnya berjalan seperti biasa
///   (modul 4 pindah ke Activity1Final lewat portal).
/// Matikan lewat menu: Antar Cepat > Mulai dari Main Menu (Play selalu memainkan scene yang dibuka).
/// </summary>
[InitializeOnLoad]
public static class AntarMulaiDariMenu
{
    const string MENU = "Antar Cepat/Mulai dari Main Menu";
    const string KUNCI = "AntarCepat.MulaiDariMenu";
    const string PATH = "Assets/Scenes/MainMenu.unity";
    static readonly string[] SCENE_ANTAR = { "Assets/Scenes/AntarCepat.unity", "Assets/Scenes/MainMenu.unity" };

    static AntarMulaiDariMenu()
    {
        EditorApplication.delayCall += Terapkan;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Terapkan(); };
        EditorSceneManager.activeSceneChangedInEditMode += (a, b) => Terapkan();
    }

    static bool Aktif { get => EditorPrefs.GetBool(KUNCI, true); set => EditorPrefs.SetBool(KUNCI, value); }

    static bool SceneAntarCepatDibuka()
    {
        var path = SceneManager.GetActiveScene().path;
        foreach (var s in SCENE_ANTAR) if (path == s) return true;
        return false;
    }

    static void Terapkan()
    {
        EditorSceneManager.playModeStartScene = Aktif && SceneAntarCepatDibuka() ? AssetDatabase.LoadAssetAtPath<SceneAsset>(PATH) : null;
        Menu.SetChecked(MENU, Aktif);
    }

    [MenuItem(MENU)]
    static void Alihkan() { Aktif = !Aktif; Terapkan(); }
}
#endif
