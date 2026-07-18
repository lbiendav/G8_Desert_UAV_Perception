using UnityEngine;

public class AnimalWanderer : MonoBehaviour
{
    public float wanderSpeed = 2f;
    public float stoppingDistance = 0.5f;
    public float wanderRadius = 30f;
    public float changeDirectionInterval = 5f;

    private Vector3 targetPosition;
    private float nextChangeTime;
    private Rigidbody rb;
    private Terrain terrain;
    private static Terrain cachedTerrain;
    private static bool terrainSearched;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (!terrainSearched)
        {
            Terrain[] allTerrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (allTerrains.Length > 0)
            {
                cachedTerrain = allTerrains[0];
            }
            terrainSearched = true;
        }
        
        terrain = cachedTerrain;

        PickNewTarget();
    }

    private void Update()
    {
        if (Time.time >= nextChangeTime)
        {
            PickNewTarget();
            nextChangeTime = Time.time + changeDirectionInterval;
        }

        MoveTowardTarget();
    }

    private void PickNewTarget()
    {
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float randomDistance = Random.Range(wanderRadius * 0.3f, wanderRadius);

        Vector3 newTarget = transform.position + new Vector3(
            Mathf.Cos(randomAngle) * randomDistance,
            0f,
            Mathf.Sin(randomAngle) * randomDistance
        );

        if (terrain != null)
        {
            float groundHeight = terrain.SampleHeight(newTarget) + terrain.transform.position.y;
            newTarget.y = groundHeight + 0.1f;
        }

        targetPosition = newTarget;
    }

    private void MoveTowardTarget()
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude > stoppingDistance)
        {
            Vector3 moveDirection = toTarget.normalized;
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(
                    moveDirection.x * wanderSpeed,
                    rb.linearVelocity.y,
                    moveDirection.z * wanderSpeed
                );
            }

            if (toTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 3f);
            }
        }
        else if (rb != null)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }
}
