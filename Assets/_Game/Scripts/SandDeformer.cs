using System.Collections.Generic;
using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Real-time sand deformation for a Terrain. SandFootprint components
    /// request stamps (world position + radius + depth); this presses the
    /// heightmap down with a smooth falloff, then slowly lets the sand flow
    /// back after a delay, as if wind refills the tracks.
    ///
    /// Play-mode heightmap edits modify the TerrainData asset itself, so the
    /// pristine heightmap is cached on enable and restored on disable - the
    /// saved terrain is never permanently altered.
    /// </summary>
    [RequireComponent(typeof(Terrain))]
    [DisallowMultipleComponent]
    public class SandDeformer : MonoBehaviour
    {
        public static SandDeformer Instance { get; private set; }

        [Header("Footprints")]
        [SerializeField] private float m_MaxDepth = 0.09f;
        [SerializeField] private float m_MaxSandSlopeDegrees = 38f;

        [Header("Recovery")]
        [SerializeField] private float m_RecoverDelay = 5f;
        [SerializeField] private float m_RecoverSpeed = 0.012f;

        [Header("Performance")]
        [SerializeField] private float m_TickInterval = 0.08f;
        [SerializeField] private float m_SyncInterval = 1.0f;

        private const int k_TileSize = 64;

        private struct StampRequest
        {
            public Vector3 position;
            public float radius;
            public float depth;
        }

        private Terrain m_Terrain;
        private TerrainData m_Data;
        private int m_Res;
        private int m_TilesPerRow;
        private Vector3 m_Origin;
        private Vector3 m_Size;

        private float[,] m_BaseHeights;
        private float[,] m_Heights;

        private readonly List<StampRequest> m_Requests = new List<StampRequest>();
        private readonly Dictionary<int, float> m_ActiveCells = new Dictionary<int, float>();
        private readonly HashSet<int> m_DirtyTiles = new HashSet<int>();
        private readonly List<int> m_Finished = new List<int>();

        private float m_NextTick;
        private float m_NextSync;
        private bool m_LodDirty;

        private void OnEnable()
        {
            m_Terrain = GetComponent<Terrain>();
            m_Data = m_Terrain.terrainData;
            m_Res = m_Data.heightmapResolution;
            m_TilesPerRow = (m_Res + k_TileSize - 1) / k_TileSize;
            m_Origin = m_Terrain.transform.position;
            m_Size = m_Data.size;

            m_BaseHeights = m_Data.GetHeights(0, 0, m_Res, m_Res);
            m_Heights = (float[,])m_BaseHeights.Clone();

            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // put the pristine heightmap back so the asset is left untouched
            if (m_Data != null && m_BaseHeights != null)
            {
                m_Data.SetHeights(0, 0, m_BaseHeights);
            }

            m_Requests.Clear();
            m_ActiveCells.Clear();
            m_DirtyTiles.Clear();
            m_BaseHeights = null;
            m_Heights = null;
        }

        public void RequestStamp(Vector3 worldPos, float radius, float depth)
        {
            StampRequest r;
            r.position = worldPos;
            r.radius = radius;
            r.depth = depth;
            m_Requests.Add(r);
        }

        private void LateUpdate()
        {
            if (Time.time < m_NextTick)
            {
                return;
            }
            m_NextTick = Time.time + m_TickInterval;

            ApplyStamps();
            RecoverSand();
            FlushDirtyTiles();

            if (m_LodDirty && Time.time >= m_NextSync)
            {
                m_Data.SyncHeightmap();
                m_NextSync = Time.time + m_SyncInterval;
                m_LodDirty = false;
            }
        }

        private void ApplyStamps()
        {
            float now = Time.time;
            float maxDepthN = m_MaxDepth / m_Size.y;

            for (int i = 0; i < m_Requests.Count; i++)
            {
                StampRequest req = m_Requests[i];

                float u = (req.position.x - m_Origin.x) / m_Size.x;
                float v = (req.position.z - m_Origin.z) / m_Size.z;
                if (u < 0f || u > 1f || v < 0f || v > 1f)
                {
                    continue;
                }

                // steep ground is rock, not loose sand - no dents there
                if (m_Data.GetSteepness(u, v) > m_MaxSandSlopeDegrees)
                {
                    continue;
                }

                int cx = Mathf.RoundToInt(u * (m_Res - 1));
                int cz = Mathf.RoundToInt(v * (m_Res - 1));
                int r = Mathf.Max(1, Mathf.CeilToInt(req.radius / m_Size.x * (m_Res - 1)));
                float depthN = req.depth / m_Size.y;

                for (int dz = -r; dz <= r; dz++)
                {
                    int z = cz + dz;
                    if (z < 0 || z >= m_Res) continue;

                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cx + dx;
                        if (x < 0 || x >= m_Res) continue;

                        float dist01 = Mathf.Sqrt(dx * dx + dz * dz) / r;
                        if (dist01 > 1f) continue;

                        float press = depthN * (1f - dist01 * dist01);
                        float floorN = m_BaseHeights[z, x] - maxDepthN;
                        float h = Mathf.Max(m_Heights[z, x] - press, floorN);

                        int key = z * m_Res + x;
                        if (h < m_Heights[z, x])
                        {
                            m_Heights[z, x] = h;
                            m_ActiveCells[key] = now;
                            m_DirtyTiles.Add(TileKey(x, z));
                        }
                        else if (m_ActiveCells.ContainsKey(key))
                        {
                            m_ActiveCells[key] = now; // keep dent fresh while occupied
                        }
                    }
                }
            }

            m_Requests.Clear();
        }

        private void RecoverSand()
        {
            if (m_ActiveCells.Count == 0)
            {
                return;
            }

            float now = Time.time;
            float step = (m_RecoverSpeed / m_Size.y) * m_TickInterval;
            m_Finished.Clear();

            foreach (KeyValuePair<int, float> cell in m_ActiveCells)
            {
                if (now - cell.Value < m_RecoverDelay)
                {
                    continue;
                }

                int z = cell.Key / m_Res;
                int x = cell.Key % m_Res;

                float h = m_Heights[z, x] + step;
                if (h >= m_BaseHeights[z, x])
                {
                    h = m_BaseHeights[z, x];
                    m_Finished.Add(cell.Key);
                }

                m_Heights[z, x] = h;
                m_DirtyTiles.Add(TileKey(x, z));
            }

            for (int i = 0; i < m_Finished.Count; i++)
            {
                m_ActiveCells.Remove(m_Finished[i]);
            }
        }

        private int TileKey(int x, int z)
        {
            return (z / k_TileSize) * m_TilesPerRow + (x / k_TileSize);
        }

        private void FlushDirtyTiles()
        {
            if (m_DirtyTiles.Count == 0)
            {
                return;
            }

            foreach (int key in m_DirtyTiles)
            {
                int tx = key % m_TilesPerRow;
                int tz = key / m_TilesPerRow;
                int x0 = tx * k_TileSize;
                int z0 = tz * k_TileSize;
                int w = Mathf.Min(k_TileSize, m_Res - x0);
                int h = Mathf.Min(k_TileSize, m_Res - z0);

                float[,] patch = new float[h, w];
                for (int z = 0; z < h; z++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        patch[z, x] = m_Heights[z0 + z, x0 + x];
                    }
                }

                m_Data.SetHeightsDelayLOD(x0, z0, patch);
            }

            m_DirtyTiles.Clear();
            m_LodDirty = true;
        }
    }
}
