using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Attach to anything that should leave tracks in the sand. Every
    /// m_StampSpacing meters of horizontal travel it asks the SandDeformer
    /// to press a dent into the terrain under the current position.
    /// </summary>
    [DisallowMultipleComponent]
    public class SandFootprint : MonoBehaviour
    {
        [SerializeField] private float m_Radius = 0.35f;
        [SerializeField] private float m_Depth = 0.05f;
        [SerializeField] private float m_StampSpacing = 0.7f;

        private Vector3 m_LastStampPos;

        private void OnEnable()
        {
            m_LastStampPos = transform.position;
        }

        private void Update()
        {
            SandDeformer deformer = SandDeformer.Instance;
            if (deformer == null)
            {
                return;
            }

            Vector3 pos = transform.position;
            Vector3 delta = pos - m_LastStampPos;
            delta.y = 0f;

            if (delta.sqrMagnitude >= m_StampSpacing * m_StampSpacing)
            {
                m_LastStampPos = pos;
                deformer.RequestStamp(pos, m_Radius, m_Depth);
            }
        }
    }
}
