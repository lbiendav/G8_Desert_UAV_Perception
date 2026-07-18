using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ThermalLayerViewMenu
{
    private const string NormalViewSessionKey = "DesertUAV_ThermalLayerView_NormalInitialized";

    static ThermalLayerViewMenu()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(NormalViewSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(NormalViewSessionKey, true);
            ShowNormalView();
        };
    }

    [MenuItem("Tools/Desert UAV/Thermal/Scene View - ThermalOverlay + Default")]
    public static void ShowThermalView()
    {
        ApplySceneViewMask((1 << 0) | (1 << LayerMask.NameToLayer("ThermalOverlay")));
    }

    [MenuItem("Tools/Desert UAV/Thermal/Scene View - Normal")]
    public static void ShowNormalView()
    {
        int thermalLayer = LayerMask.NameToLayer("ThermalOverlay");
        int mask = ~0;
        if (thermalLayer >= 0)
        {
            mask &= ~(1 << thermalLayer);
        }

        ApplySceneViewMask(mask);
    }

    private static void ApplySceneViewMask(int mask)
    {
        foreach (SceneView sceneView in SceneView.sceneViews)
        {
            sceneView.camera.cullingMask = mask;
            sceneView.Repaint();
        }
    }
}
