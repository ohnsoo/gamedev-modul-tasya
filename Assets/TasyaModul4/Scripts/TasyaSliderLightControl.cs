using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Bonus Challenge Modul 4:
// Slider di World Space UI NPC dipakai untuk mengatur intensitas lampu (Directional Light).
// Cara kerjanya mirip Button: kita mendaftarkan method ke event Slider.
// Bedanya, Button memakai onClick (tanpa nilai), sedangkan Slider memakai
// onValueChanged yang mengirim nilai float (0..1) setiap kali slider digeser.
public class TasyaSliderLightControl : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Slider yang ada di Canvas NPC")]
    private Slider lightSlider;

    [SerializeField]
    [Tooltip("Lampu yang diatur. Kalau kosong, otomatis memakai Directional Light di scene")]
    private Light targetLight;

    [SerializeField]
    [Tooltip("Teks TMP untuk menampilkan nilai slider (opsional)")]
    private TextMeshProUGUI valueLabel;

    [SerializeField]
    [Tooltip("Pengali terang saat slider paling kiri (dikali intensitas awal lampu)")]
    private float minMultiplier = 0.7f;

    [SerializeField]
    [Tooltip("Pengali terang saat slider paling kanan (dikali intensitas awal lampu)")]
    private float maxMultiplier = 1.3f;

    private float baseIntensity = 1f;  // intensitas awal lampu, supaya warna map tetap sama di posisi tengah

    private void Start()
    {
        // Kalau lampu belum diisi di Inspector, pakai Directional Light yang ada di scene
        if (targetLight == null)
        {
            targetLight = RenderSettings.sun;
        }

        if (targetLight == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    targetLight = l;
                    break;
                }
            }
        }

        if (targetLight != null)
        {
            baseIntensity = targetLight.intensity;
        }

        if (lightSlider != null)
        {
            lightSlider.onValueChanged.AddListener(OnSliderChanged);
            OnSliderChanged(lightSlider.value);
        }
    }

    private void OnDestroy()
    {
        if (lightSlider != null)
        {
            lightSlider.onValueChanged.RemoveListener(OnSliderChanged);
        }
    }

    // Dipanggil setiap kali nilai slider berubah
    public void OnSliderChanged(float value)
    {
        float t = Mathf.InverseLerp(lightSlider.minValue, lightSlider.maxValue, value);

        if (targetLight != null)
        {
            targetLight.intensity = baseIntensity * Mathf.Lerp(minMultiplier, maxMultiplier, t);
        }

        if (valueLabel != null)
        {
            valueLabel.text = "Lampu: " + Mathf.RoundToInt(t * 100f) + "%";
        }
    }
}
