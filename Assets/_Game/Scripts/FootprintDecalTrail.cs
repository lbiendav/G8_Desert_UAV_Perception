using System.Collections.Generic;
using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Drops footprint decals behind a moving creature, complementing the
    /// 3D dents made by SandFootprint. All creatures share one fixed-size
    /// pool, so old prints are recycled and the scene never accumulates
    /// decals (cheap and bounded).
    /// </summary>
    [DisallowMultipleComponent]
    public class FootprintDecalTrail : MonoBehaviour
    {
        [SerializeField] private GameObject m_DecalPrefab;
        [SerializeField] private float m_Spacing = 0.9f;
        [SerializeField] private float m_SideOffset = 0.16f;
        [SerializeField] private float m_Scale = 1f;

        private const int k_PoolSize = 48;
        private static readonly List<GameObject> s_Pool = new List<GameObject>();
        private static int s_Next;
        private static Transform s_PoolRoot;

        private Vector3 m_LastPos;
        private bool m_LeftSide;

        private void OnEnable()
        {
            m_LastPos = transform.position;
        }

        private void Update()
        {
            if (m_DecalPrefab == null)
            {
                return;
            }

            Vector3 pos = transform.position;
            Vector3 delta = pos - m_LastPos;
            delta.y = 0f;
            if (delta.sqrMagnitude < m_Spacing * m_Spacing)
            {
                return;
            }

            Vector3 dir = delta.normalized;
            m_LastPos = pos;
            m_LeftSide = !m_LeftSide;

            Terrain terrain = Terrain.activeTerrain;
            float y = terrain != null
                ? terrain.transform.position.y + terrain.SampleHeight(pos)
                : pos.y;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (m_LeftSide ? m_SideOffset : -m_SideOffset);
            Vector3 p = pos + side;
            p.y = y + 0.4f;

            GameObject decal = Rent();
            if (decal == null)
            {
                return;
            }
            // decal projectors project along +Z: aim it straight down with
            // the print's "up" following the walk direction
            decal.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.down, dir));
            decal.transform.localScale = m_DecalPrefab.transform.localScale * m_Scale;
            decal.SetActive(true);
        }

        private GameObject Rent()
        {
            if (s_PoolRoot == null)
            {
                GameObject root = GameObject.Find("Footprint_Decals");
                if (root == null)
                {
                    root = new GameObject("Footprint_Decals");
                }
                s_PoolRoot = root.transform;
                s_Pool.Clear();
                s_Next = 0;
            }

            if (s_Pool.Count < k_PoolSize)
            {
                GameObject fresh = Instantiate(m_DecalPrefab, s_PoolRoot);
                s_Pool.Add(fresh);
                return fresh;
            }

            GameObject reused = s_Pool[s_Next];
            s_Next = (s_Next + 1) % s_Pool.Count;
            return reused;
        }
    }
}
