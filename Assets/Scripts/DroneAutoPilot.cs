using UnityEngine;

[RequireComponent(typeof(DroneCheckpointMover))]
public class DroneAutoPilot : MonoBehaviour
{
    public enum AutopilotMode
    {
        Disabled,
        Orbit,
        Patrol,
        Waypoint
    }

    [Header("Autopilot")]
    public AutopilotMode currentMode = AutopilotMode.Disabled;
    public bool autoStartOnEnable = false;

    [Header("Orbit Settings")]
    public Vector3 orbitCenter = Vector3.zero;
    public float orbitRadius = 50f;
    public float orbitHeight = 30f;
    public float orbitSpeed = 0.5f;
    private float orbitAngle;

    [Header("Patrol Settings")]
    public Transform[] patrolPoints;
    public float patrolSpacing = 10f;

    [Header("Movement")]
    public float speed = 18f;
    public float turnSpeed = 5f;
    public Terrain terrain;
    public float minimumGroundClearance = 2f;

    private DroneCheckpointMover checkpointMover;
    private bool isAutopilotActive;
    private int patrolIndex;

    private void OnEnable()
    {
        checkpointMover = GetComponent<DroneCheckpointMover>();
        if (autoStartOnEnable)
        {
            StartAutopilot(currentMode);
        }
    }

    private void Update()
    {
        if (!isAutopilotActive || checkpointMover == null)
            return;

        switch (currentMode)
        {
            case AutopilotMode.Orbit:
                UpdateOrbit();
                break;
            case AutopilotMode.Patrol:
                UpdatePatrol();
                break;
            case AutopilotMode.Waypoint:
                break;
        }
    }

    public void StartAutopilot(AutopilotMode mode)
    {
        currentMode = mode;
        isAutopilotActive = true;
        checkpointMover.Pause();

        switch (mode)
        {
            case AutopilotMode.Orbit:
                orbitAngle = 0f;
                break;
            case AutopilotMode.Patrol:
                patrolIndex = 0;
                if (patrolPoints == null || patrolPoints.Length == 0)
                {
                    GeneratePatrolPoints();
                }
                break;
        }
    }

    public void StopAutopilot()
    {
        isAutopilotActive = false;
        if (checkpointMover != null)
        {
            checkpointMover.Play();
        }
    }

    public void SwitchMode(AutopilotMode newMode)
    {
        StopAutopilot();
        StartAutopilot(newMode);
    }

    private void UpdateOrbit()
    {
        orbitAngle += orbitSpeed * Time.deltaTime;
        orbitAngle = orbitAngle % 360f;

        float radians = orbitAngle * Mathf.Deg2Rad;
        Vector3 targetPos = orbitCenter + new Vector3(
            Mathf.Cos(radians) * orbitRadius,
            orbitHeight,
            Mathf.Sin(radians) * orbitRadius
        );

        targetPos = ClampAboveTerrain(targetPos);
        MoveTowardTarget(targetPos);
    }

    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        Transform currentTarget = patrolPoints[patrolIndex];
        Vector3 targetPos = ClampAboveTerrain(currentTarget.position);
        Vector3 toTarget = targetPos - transform.position;

        MoveTowardTarget(targetPos);

        if (toTarget.magnitude <= 2f)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
    }

    private void MoveTowardTarget(Vector3 targetPos)
    {
        Vector3 toTarget = targetPos - transform.position;

        Vector3 nextPosition = Vector3.MoveTowards(
            transform.position,
            targetPos,
            speed * Time.deltaTime
        );
        transform.position = nextPosition;

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            float turnBlend = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnBlend);
        }
    }

    private Vector3 ClampAboveTerrain(Vector3 position)
    {
        if (terrain == null)
            return position;

        float groundHeight = terrain.SampleHeight(position) + terrain.transform.position.y;
        position.y = Mathf.Max(position.y, groundHeight + minimumGroundClearance);
        return position;
    }

    private void GeneratePatrolPoints()
    {
        patrolPoints = new Transform[8];
        
        GameObject patrolContainer = new GameObject("PatrolPoints");
        patrolContainer.transform.SetParent(transform, false);

        for (int i = 0; i < 8; i++)
        {
            float angle = (i / 8f) * 360f * Mathf.Deg2Rad;
            Vector3 pos = orbitCenter + new Vector3(
                Mathf.Cos(angle) * orbitRadius,
                orbitHeight,
                Mathf.Sin(angle) * orbitRadius
            );

            GameObject point = new GameObject($"PatrolPoint_{i}");
            point.transform.SetParent(patrolContainer.transform, false);
            point.transform.position = pos;
            patrolPoints[i] = point.transform;
        }
    }

    public void SetOrbitCenter(Vector3 center)
    {
        orbitCenter = center;
    }

    public void SetOrbitRadius(float radius)
    {
        orbitRadius = Mathf.Max(10f, radius);
    }

    public void SetOrbitSpeed(float newSpeed)
    {
        orbitSpeed = newSpeed;
    }

    public bool IsAutopilotActive => isAutopilotActive;

    public AutopilotMode CurrentMode => currentMode;
}

