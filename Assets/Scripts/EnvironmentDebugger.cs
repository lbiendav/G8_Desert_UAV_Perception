using UnityEngine;

[ExecuteAlways]
public class EnvironmentDebugger : MonoBehaviour
{
    public DesertEnvironmentController environmentController;
    public bool displayDebugInfo = true;
    
    private GUIStyle debugStyle;

    private void OnGUI()
    {
        if (!displayDebugInfo || environmentController == null)
            return;

        if (debugStyle == null)
        {
            debugStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 10, 10)
            };
            debugStyle.normal.textColor = Color.white;
        }

        GUI.Box(new Rect(10, 10, 350, 250), "", debugStyle);

        GUI.Label(new Rect(20, 20, 330, 25), $"<b>═══ ENVIRONMENT DEBUG ═══</b>", debugStyle);
        
        GUI.Label(new Rect(20, 50, 330, 20), $"Time of Day: <color=cyan>{environmentController.timeOfDay:F1}h</color>", debugStyle);
        GUI.Label(new Rect(20, 75, 330, 20), $"Sunlight: <color=yellow>{environmentController.DayFactor:F2}</color> (0-1)", debugStyle);
        GUI.Label(new Rect(20, 100, 330, 20), $"Temperature: <color=red>{environmentController.CurrentTemperatureCelsius:F1}°C</color>", debugStyle);
        GUI.Label(new Rect(20, 125, 330, 20), $"Heat Intensity: <color=magenta>{environmentController.CurrentHeatIntensity:F2}</color> (Thermal)", debugStyle);
        
        GUI.Label(new Rect(20, 155, 330, 20), $"Rain Intensity: <color=blue>{environmentController.rainIntensity:F2}</color>", debugStyle);
        GUI.Label(new Rect(20, 180, 330, 20), $"Wind: <color=cyan>{environmentController.windIntensity:F2}</color> | Dust: <color=yellow>{environmentController.dustIntensity:F2}</color>", debugStyle);
        GUI.Label(new Rect(20, 205, 330, 20), $"Animate Time: <color={(environmentController.animateTime ? "lime" : "red")}>{environmentController.animateTime}</color>", debugStyle);
        
        DrawThermalStatus();
    }

    private void DrawThermalStatus()
    {
        string thermalStatus = environmentController.CurrentHeatIntensity > 0.3f ? 
            "<color=red>🔥 THERMAL VISIBLE</color>" : 
            "<color=blue>❄️ LOW THERMAL</color>";

        GUI.Label(new Rect(20, 230, 330, 20), $"Status: {thermalStatus}", debugStyle);
    }

    [ContextMenu("Toggle Debug Display")]
    public void ToggleDebugDisplay()
    {
        displayDebugInfo = !displayDebugInfo;
    }
}
