using UnityEngine;

/// <summary>
/// Balon sapaan warga: selalu menghadap kamera, ukurannya disesuaikan jarak kamera
/// (tidak menutupi layar saat kamera dekat), dan memudar kalau kamera terlalu dekat.
/// </summary>
public class AntarBalon : MonoBehaviour
{
    public float skalaDasar = 0.0035f;
    CanvasGroup grup;

    void Awake()
    {
        grup = GetComponent<CanvasGroup>();
        if (grup == null) grup = gameObject.AddComponent<CanvasGroup>();
        grup.blocksRaycasts = false;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 ke = transform.position - cam.transform.position;
        float d = ke.magnitude;
        transform.rotation = Quaternion.LookRotation(ke);
        // ukuran tetap kira-kira sama di layar: makin dekat kamera, makin kecil balonnya
        transform.localScale = Vector3.one * Mathf.Min(skalaDasar, 0.00018f * d);
        grup.alpha = Mathf.Clamp01((d - 2.5f) / 2f);
    }
}
