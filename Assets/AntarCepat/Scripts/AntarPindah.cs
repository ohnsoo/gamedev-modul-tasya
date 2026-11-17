using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Sistem perpindahan antar scene dengan efek layar memudar (fade).
/// Pemakaian: AntarPindah.Ke("AntarCepat");
/// </summary>
public class AntarPindah : MonoBehaviour
{
    public const string SCENE_MENU = "MainMenu";
    public const string SCENE_GAME = "AntarCepat";

    static AntarPindah aktif;
    Image tirai;

    public static bool SedangPindah => aktif != null;

    public static void Ke(string namaScene)
    {
        if (aktif != null) return;   // jangan pindah dua kali
        var go = new GameObject("Perpindahan Scene");
        DontDestroyOnLoad(go);
        aktif = go.AddComponent<AntarPindah>();
        aktif.StartCoroutine(aktif.Jalankan(namaScene));
    }

    IEnumerator Jalankan(string namaScene)
    {
        var kanvas = gameObject.AddComponent<Canvas>();
        kanvas.renderMode = RenderMode.ScreenSpaceOverlay; kanvas.sortingOrder = 999;
        gameObject.AddComponent<GraphicRaycaster>();     // menahan klik selama pindah
        var g = new GameObject("Tirai", typeof(RectTransform), typeof(Image));
        var r = g.GetComponent<RectTransform>(); r.SetParent(transform, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        tirai = g.GetComponent<Image>();
        tirai.color = new Color(0.17f, 0.11f, 0.23f, 0f);

        yield return Pudar(0f, 1f, 0.35f);
        var muat = SceneManager.LoadSceneAsync(namaScene);
        while (!muat.isDone) yield return null;
        yield return null;
        yield return Pudar(1f, 0f, 0.45f);
        aktif = null;
        Destroy(gameObject);
    }

    IEnumerator Pudar(float dari, float ke, float durasi)
    {
        var c = tirai.color;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / durasi)
        {
            c.a = Mathf.Lerp(dari, ke, Mathf.SmoothStep(0f, 1f, t));
            tirai.color = c;
            yield return null;
        }
        c.a = ke; tirai.color = c;
    }
}
