using UnityEngine;

[ExecuteAlways]
public class SovaDroneVisualBuilder : MonoBehaviour
{
    [SerializeField] private bool rebuildOnEnable = true;

    private const string VisualRootName = "SOVA_VisualRoot";

    private void OnEnable()
    {
        if (rebuildOnEnable)
        {
            Rebuild();
        }
    }

    [ContextMenu("Rebuild SOVA Drone Visual")]
    public void Rebuild()
    {
        ClearVisuals();

        Transform visualRoot = new GameObject(VisualRootName).transform;
        visualRoot.SetParent(transform, false);

        Material bodyMaterial = CreateMaterial("SOVA Black Composite", new Color(0.015f, 0.016f, 0.016f), 0.78f, 0.35f);
        Material panelMaterial = CreateMaterial("SOVA Gunmetal Panels", new Color(0.18f, 0.18f, 0.17f), 0.65f, 0.25f);
        Material lensMaterial = CreateMaterial("SOVA Blue IR Lens", new Color(0.02f, 0.08f, 0.12f), 0.95f, 0.05f);

        CreateBox(visualRoot, "Main_Faceted_Body", new Vector3(0f, 0f, 0f), new Vector3(1.35f, 0.28f, 0.82f), Quaternion.identity, bodyMaterial);
        CreateBox(visualRoot, "Forward_Nose", new Vector3(0f, -0.02f, 0.55f), new Vector3(0.74f, 0.32f, 0.62f), Quaternion.identity, bodyMaterial);
        CreateBox(visualRoot, "Top_Spine", new Vector3(0f, 0.21f, -0.08f), new Vector3(0.28f, 0.16f, 1.15f), Quaternion.identity, panelMaterial);
        CreateBox(visualRoot, "Rear_Power_Block", new Vector3(0f, 0.02f, -0.58f), new Vector3(0.88f, 0.28f, 0.42f), Quaternion.identity, bodyMaterial);
        CreateBox(visualRoot, "Camera_Housing", new Vector3(0f, -0.16f, 0.88f), new Vector3(0.34f, 0.24f, 0.24f), Quaternion.identity, bodyMaterial);

        GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lens.name = "Forward_IR_Lens";
        lens.transform.SetParent(visualRoot, false);
        lens.transform.localPosition = new Vector3(0f, -0.16f, 1.02f);
        lens.transform.localScale = new Vector3(0.16f, 0.16f, 0.08f);
        lens.GetComponent<Renderer>().sharedMaterial = lensMaterial;

        CreateBox(visualRoot, "Left_Duct_Arm", new Vector3(-0.98f, 0f, 0.18f), new Vector3(1.35f, 0.12f, 0.18f), Quaternion.Euler(0f, 18f, 0f), bodyMaterial);
        CreateBox(visualRoot, "Right_Duct_Arm", new Vector3(0.98f, 0f, 0.18f), new Vector3(1.35f, 0.12f, 0.18f), Quaternion.Euler(0f, -18f, 0f), bodyMaterial);

        CreateRotorAssembly(visualRoot, "Left", new Vector3(-1.55f, 0.03f, 0.2f), bodyMaterial, panelMaterial);
        CreateRotorAssembly(visualRoot, "Right", new Vector3(1.55f, 0.03f, 0.2f), bodyMaterial, panelMaterial);

        CreateBox(visualRoot, "Left_Rear_Fin", new Vector3(-0.34f, 0.24f, -0.72f), new Vector3(0.18f, 0.55f, 0.58f), Quaternion.Euler(-18f, 0f, -18f), panelMaterial);
        CreateBox(visualRoot, "Right_Rear_Fin", new Vector3(0.34f, 0.24f, -0.72f), new Vector3(0.18f, 0.55f, 0.58f), Quaternion.Euler(-18f, 0f, 18f), panelMaterial);
        CreateBox(visualRoot, "Center_Tail_Fin", new Vector3(0f, 0.34f, -0.86f), new Vector3(0.16f, 0.72f, 0.5f), Quaternion.Euler(-12f, 0f, 0f), panelMaterial);

        CreateBox(visualRoot, "Left_Front_Guard", new Vector3(-0.45f, -0.03f, 0.56f), new Vector3(0.08f, 0.58f, 0.13f), Quaternion.Euler(0f, 0f, -22f), panelMaterial);
        CreateBox(visualRoot, "Right_Front_Guard", new Vector3(0.45f, -0.03f, 0.56f), new Vector3(0.08f, 0.58f, 0.13f), Quaternion.Euler(0f, 0f, 22f), panelMaterial);

        DroneThermalSignature signature = GetComponent<DroneThermalSignature>();
        if (signature != null)
        {
            signature.droneRenderers = GetComponentsInChildren<Renderer>();
        }
    }

    private void ClearVisuals()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == VisualRootName || child.GetComponent<Renderer>() != null)
            {
                DestroyVisualObject(child.gameObject);
            }
        }
    }

    private static void CreateRotorAssembly(Transform parent, string side, Vector3 center, Material bodyMaterial, Material panelMaterial)
    {
        GameObject duct = new GameObject($"{side}_Duct_Ring");
        duct.transform.SetParent(parent, false);
        duct.transform.localPosition = center;
        duct.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        duct.AddComponent<MeshFilter>().sharedMesh = CreateTorusMesh($"{side}_Duct_Ring_Mesh", 0.62f, 0.07f, 64, 10);
        duct.AddComponent<MeshRenderer>().sharedMaterial = bodyMaterial;

        CreateBox(parent, $"{side}_Rotor_Crossbar", center, new Vector3(1.02f, 0.08f, 0.1f), Quaternion.identity, bodyMaterial);

        GameObject rotorPivot = new GameObject($"{side}_Rotor_Spin_Pivot");
        rotorPivot.transform.SetParent(parent, false);
        rotorPivot.transform.localPosition = center;
        SovaRotorSpinner spinner = rotorPivot.AddComponent<SovaRotorSpinner>();
        spinner.flightDegreesPerSecond = side == "Left" ? 2400f : -2400f;

        CreateBox(rotorPivot.transform, $"{side}_Rotor_Motor", new Vector3(0f, 0.02f, 0f), new Vector3(0.24f, 0.14f, 0.24f), Quaternion.identity, panelMaterial);
        CreateBox(rotorPivot.transform, $"{side}_Rotor_Blade_A", new Vector3(0f, 0.035f, 0f), new Vector3(0.88f, 0.025f, 0.12f), Quaternion.Euler(0f, 18f, 0f), panelMaterial);
        CreateBox(rotorPivot.transform, $"{side}_Rotor_Blade_B", new Vector3(0f, 0.04f, 0f), new Vector3(0.88f, 0.025f, 0.12f), Quaternion.Euler(0f, 108f, 0f), panelMaterial);
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localRotation = localRotation;
        box.transform.localScale = localScale;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static Mesh CreateTorusMesh(string name, float majorRadius, float minorRadius, int majorSegments, int minorSegments)
    {
        Vector3[] vertices = new Vector3[majorSegments * minorSegments];
        Vector3[] normals = new Vector3[vertices.Length];
        int[] triangles = new int[majorSegments * minorSegments * 6];

        for (int major = 0; major < majorSegments; major++)
        {
            float majorAngle = major * Mathf.PI * 2f / majorSegments;
            Vector3 majorCenter = new Vector3(Mathf.Cos(majorAngle) * majorRadius, Mathf.Sin(majorAngle) * majorRadius, 0f);

            for (int minor = 0; minor < minorSegments; minor++)
            {
                float minorAngle = minor * Mathf.PI * 2f / minorSegments;
                Vector3 normal = new Vector3(Mathf.Cos(majorAngle) * Mathf.Cos(minorAngle), Mathf.Sin(majorAngle) * Mathf.Cos(minorAngle), Mathf.Sin(minorAngle));
                int vertexIndex = major * minorSegments + minor;
                vertices[vertexIndex] = majorCenter + normal * minorRadius;
                normals[vertexIndex] = normal;
            }
        }

        int triangleIndex = 0;
        for (int major = 0; major < majorSegments; major++)
        {
            int nextMajor = (major + 1) % majorSegments;
            for (int minor = 0; minor < minorSegments; minor++)
            {
                int nextMinor = (minor + 1) % minorSegments;
                int a = major * minorSegments + minor;
                int b = nextMajor * minorSegments + minor;
                int c = nextMajor * minorSegments + nextMinor;
                int d = major * minorSegments + nextMinor;

                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = d;
            }
        }

        Mesh mesh = new Mesh
        {
            name = name,
            vertices = vertices,
            normals = normals,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateMaterial(string materialName, Color color, float smoothness, float metallic)
    {
        Shader shader = Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };

        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", smoothness);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }

        return material;
    }

    private static void DestroyVisualObject(Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
