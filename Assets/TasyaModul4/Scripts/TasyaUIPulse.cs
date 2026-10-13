using UnityEngine;

public sealed class TasyaUIPulse : MonoBehaviour
{
    [SerializeField, Min(0f)] private float scaleAmount = 0.035f;
    [SerializeField, Min(0f)] private float pulseSpeed = 2.2f;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * scaleAmount;
        transform.localScale = baseScale * pulse;
    }
}