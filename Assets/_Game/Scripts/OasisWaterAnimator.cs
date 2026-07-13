using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Keeps the oasis water alive: scrolls the normal map of the HDRP/Lit
    /// water material (via MaterialPropertyBlock, so the material asset is
    /// never dirtied) and, in play mode, bobs the surface a few centimeters
    /// so the waterline breathes against the shore. Runs in edit mode too,
    /// so the lake never looks frozen while working in the editor.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class OasisWaterAnimator : MonoBehaviour
    {
        [Header("Ripple scroll")]
        [SerializeField] private Renderer m_Renderer;
        [SerializeField] private Vector2 m_ScrollDirection = new Vector2(0.8f, 0.6f);
        [SerializeField] private float m_ScrollSpeed = 0.02f;      // UV per second
        [SerializeField] private float m_WanderAmplitude = 0.02f;  // sideways UV sway
        [SerializeField] private float m_WanderFrequency = 0.12f;  // Hz

        [Header("Surface bob (play mode only)")]
        [SerializeField] private float m_BobAmplitude = 0.035f;
        [SerializeField] private float m_BobFrequency = 0.16f;

        private static readonly int k_BaseMapST = Shader.PropertyToID("_BaseColorMap_ST");

        private MaterialPropertyBlock m_Block;
        private Vector2 m_Tiling = new Vector2(20f, 20f);
        private float m_BaseY;
        private bool m_HasBaseY;

        private void OnEnable()
        {
            if (m_Renderer == null)
            {
                m_Renderer = GetComponent<Renderer>();
            }
            m_Block = new MaterialPropertyBlock();
            if (m_Renderer != null && m_Renderer.sharedMaterial != null &&
                m_Renderer.sharedMaterial.HasProperty("_BaseColorMap"))
            {
                m_Tiling = m_Renderer.sharedMaterial.GetTextureScale("_BaseColorMap");
            }
            if (!m_HasBaseY)
            {
                m_BaseY = transform.position.y;
                m_HasBaseY = true;
            }
        }

        private void OnDisable()
        {
            if (m_HasBaseY)
            {
                Vector3 p = transform.position;
                p.y = m_BaseY;
                transform.position = p;
            }
            if (m_Renderer != null)
            {
                m_Renderer.SetPropertyBlock(null);
            }
        }

        private void Update()
        {
            if (m_Renderer == null)
            {
                return;
            }

            // realtime clock so ripples keep moving in edit mode as well
            float t = Time.realtimeSinceStartup;

            Vector2 dir = m_ScrollDirection.sqrMagnitude > 0.0001f
                ? m_ScrollDirection.normalized
                : Vector2.right;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 offset = dir * (m_ScrollSpeed * t)
                           + perp * (Mathf.Sin(t * m_WanderFrequency * 2f * Mathf.PI) * m_WanderAmplitude);
            offset.x = Mathf.Repeat(offset.x, 1f);
            offset.y = Mathf.Repeat(offset.y, 1f);

            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetVector(k_BaseMapST, new Vector4(m_Tiling.x, m_Tiling.y, offset.x, offset.y));
            m_Renderer.SetPropertyBlock(m_Block);

            if (Application.isPlaying && m_BobAmplitude > 0f)
            {
                Vector3 p = transform.position;
                p.y = m_BaseY + Mathf.Sin(t * m_BobFrequency * 2f * Mathf.PI) * m_BobAmplitude;
                transform.position = p;
            }
        }
    }
}
