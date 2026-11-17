#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Saat tombol Play ditekan di Editor, game selalu dimulai dari scene MainMenu
/// (sama seperti hasil build), walaupun yang sedang dibuka adalah scene AntarCepat.
/// Matikan lewat menu: Antar Cepat > Mulai dari Main Menu.
/// </summary>
[InitializeOnLoad]
public static class AntarMulaiDariMenu
{
    const string MENU = "Antar Cepat/Mulai dari Main Menu";
    const string KUNCI = "AntarCepat.MulaiDariMenu";
    const string PATH = "Assets/Scenes/MainMenu.unity";

    static AntarMulaiDariMenu()
    {
        EditorApplication.delayCall += Terapkan;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Terapkan(); };
    }

    static bool Aktif { get => EditorPrefs.GetBool(KUNCI, true); set => EditorPrefs.SetBool(KUNCI, value); }

    static void Terapkan()
    {
        EditorSceneManager.playModeStartScene = Aktif ? AssetDatabase.LoadAssetAtPath<SceneAsset>(PATH) : null;
        Menu.SetChecked(MENU, Aktif);
    }

    [MenuItem(MENU)]
    static void Alihkan() { Aktif = !Aktif; Terapkan(); }
}
#endif
