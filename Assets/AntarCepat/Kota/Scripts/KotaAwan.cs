using UnityEngine;

/// <summary>Awan di langit bergerak pelan terbawa angin dan muncul lagi dari sisi seberang (melingkar di area kota).</summary>
public class KotaAwan : MonoBehaviour
{
    public Vector3 angin = new Vector3(2.2f, 0f, 0.8f);
    public Vector3 pusat = new Vector3(60f, 0f, 55f);
    public float radius = 650f;

    void Update()
    {
        var d = angin * Time.deltaTime;
        foreach (Transform c in transform)
        {
            var p = c.position + d * (0.8f + 0.4f * Mathf.PerlinNoise(c.GetSiblingIndex() * 0.37f, 0f));
            var v = p - pusat; v.y = 0f;
            if (v.magnitude > radius) p -= v.normalized * radius * 1.95f;   // pindah ke sisi seberang
            c.position = p;
        }
    }
}
