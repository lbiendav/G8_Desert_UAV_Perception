using UnityEngine;
using ithappy.Animals_FREE;

namespace DesertEnv
{
    /// <summary>
    /// Lightweight autonomous wander driver for the asset-pack animals.
    /// Picks random reachable points on gentle sand around a home anchor,
    /// walks or runs there through CreatureMover, idles, repeats. Rejects
    /// destinations in the lake, on steep rock, on the high hills, or whose
    /// straight-line path would cross the water.
    /// </summary>
    [RequireComponent(typeof(CreatureMover))]
    [DisallowMultipleComponent]
    public class CreatureWanderAI : MonoBehaviour
    {
        [Header("Roaming")]
        [SerializeField] private float m_WanderRadius = 50f;
        [SerializeField] private Vector2 m_IdleDuration = new Vector2(2f, 7f);
        [SerializeField, Range(0f, 1f)] private float m_RunChance = 0.2f;
        [SerializeField] private float m_ArriveDistance = 1.6f;
        [SerializeField] private float m_GiveUpTime = 14f;

        [Header("Terrain limits")]
        [SerializeField] private float m_MaxSlopeDegrees = 26f;
        [SerializeField] private float m_MinGroundHeight = 3.2f;
        [SerializeField] private float m_MaxGroundHeight = 60f;
        [SerializeField] private float m_EdgeMargin = 25f;

        private CreatureMover m_Mover;
        private Terrain m_Terrain;
        private Vector3 m_Home;

        private bool m_Walking;
        private bool m_Running;
        private Vector3 m_Destination;
        private float m_StateTimer;

        private float m_StuckTimer;
        private Vector3 m_StuckAnchor;

        private void Awake()
        {
            m_Mover = GetComponent<CreatureMover>();
            m_Terrain = Terrain.activeTerrain;
            m_Home = transform.position;
        }

        private void Start()
        {
            // stagger the herd so everyone doesn't move in sync
            m_StateTimer = Random.Range(0.3f, m_IdleDuration.y);
            m_Walking = false;
        }

        private void Update()
        {
            if (m_Walking)
            {
                TickWalk();
            }
            else
            {
                TickIdle();
            }
        }

        private void TickIdle()
        {
            m_StateTimer -= Time.deltaTime;
            m_Mover.SetInput(Vector2.zero, transform.position + transform.forward * 8f, false, false);

            if (m_StateTimer > 0f)
            {
                return;
            }

            Vector3 destination;
            if (TryPickDestination(out destination))
            {
                m_Destination = destination;
                m_Walking = true;
                m_Running = Random.value < m_RunChance;
                m_StateTimer = m_GiveUpTime;
                m_StuckTimer = 0f;
                m_StuckAnchor = transform.position;
            }
            else
            {
                m_StateTimer = Random.Range(0.5f, 1.5f);
            }
        }

        private void TickWalk()
        {
            m_StateTimer -= Time.deltaTime;

            Vector3 pos = transform.position;
            Vector3 toTarget = m_Destination - pos;
            toTarget.y = 0f;

            if (toTarget.magnitude <= m_ArriveDistance || m_StateTimer <= 0f)
            {
                EnterIdle();
                return;
            }

            // stuck check: barely any progress over a 2 second window
            m_StuckTimer += Time.deltaTime;
            if (m_StuckTimer >= 2f)
            {
                if ((pos - m_StuckAnchor).sqrMagnitude < 0.09f)
                {
                    EnterIdle();
                    return;
                }
                m_StuckTimer = 0f;
                m_StuckAnchor = pos;
            }

            Vector3 lookTarget = pos + toTarget.normalized * 10f;
            lookTarget.y = pos.y;
            m_Mover.SetInput(new Vector2(0f, 1f), lookTarget, m_Running, false);
        }

        private void EnterIdle()
        {
            m_Walking = false;
            m_StateTimer = Random.Range(m_IdleDuration.x, m_IdleDuration.y);
            m_Mover.SetInput(Vector2.zero, transform.position + transform.forward * 8f, false, false);
        }

        private bool TryPickDestination(out Vector3 destination)
        {
            for (int attempt = 0; attempt < 14; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle;
                if (offset.sqrMagnitude < 0.02f)
                {
                    continue;
                }

                Vector3 candidate = m_Home + new Vector3(offset.x, 0f, offset.y) * m_WanderRadius;
                if (!IsPointValid(candidate))
                {
                    continue;
                }

                // the straight path must stay valid too (no cutting through the lake)
                Vector3 from = transform.position;
                bool pathOk = true;
                for (int s = 1; s <= 4; s++)
                {
                    Vector3 sample = Vector3.Lerp(from, candidate, s / 4f);
                    if (!IsPointValid(sample))
                    {
                        pathOk = false;
                        break;
                    }
                }
                if (!pathOk)
                {
                    continue;
                }

                candidate.y = SampleWorldHeight(candidate);
                destination = candidate;
                return true;
            }

            destination = Vector3.zero;
            return false;
        }

        private bool IsPointValid(Vector3 point)
        {
            if (m_Terrain == null)
            {
                return false;
            }

            Vector3 origin = m_Terrain.transform.position;
            Vector3 size = m_Terrain.terrainData.size;

            float nx = (point.x - origin.x) / size.x;
            float nz = (point.z - origin.z) / size.z;
            float margin = m_EdgeMargin / size.x;
            if (nx < margin || nx > 1f - margin || nz < margin || nz > 1f - margin)
            {
                return false;
            }

            float height = SampleWorldHeight(point);
            if (height < m_MinGroundHeight || height > m_MaxGroundHeight)
            {
                return false;
            }

            return m_Terrain.terrainData.GetSteepness(nx, nz) <= m_MaxSlopeDegrees;
        }

        private float SampleWorldHeight(Vector3 point)
        {
            return m_Terrain.SampleHeight(point) + m_Terrain.transform.position.y;
        }
    }
}
