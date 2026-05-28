using UnityEngine;

public class GlobalThermalController : MonoBehaviour
{
    [Range(0f, 1f)] public float timeOfDay = 0f;
    [Range(0f, 1f)] public float rainIntensity = 0f;

    [Header("Auto Day/Night")]
    public bool autoCycle = true;
    public float daySpeed = 0.05f;

    void Update()
    {
        // Auto chạy ngày đêm
        if (autoCycle)
        {
            timeOfDay += Time.deltaTime * daySpeed;
            if (timeOfDay > 1f) timeOfDay = 0f;
        }

        // Tính sun intensity (mượt kiểu hình sin)
        float sun = Mathf.Clamp01(Mathf.Sin(timeOfDay * Mathf.PI));

        // Set global shader
        Shader.SetGlobalFloat("_TimeOfDay", timeOfDay);
        Shader.SetGlobalFloat("_SunIntensity", sun);
        Shader.SetGlobalFloat("_RainIntensity", rainIntensity);
    }
}