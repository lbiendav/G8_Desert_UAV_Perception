using UnityEngine;

public class DroneCheckpointMover : MonoBehaviour
{
    public enum LoopMode
    {
        StopAtLast,
        Loop,
        PingPong
    }

    [Header("Route")]
    public Transform[] checkpoints;
    public LoopMode loopMode = LoopMode.Loop;
    public bool playOnStart = true;
    public float reachDistance = 1.5f;

    [Header("Movement")]
    public float speed = 18f;
    public float turnSpeed = 5f;
    public bool faceMovementDirection = true;

    [Header("Altitude")]
    public Terrain terrain;
    public bool keepAboveTerrain = true;
    public float minimumGroundClearance = 2f;

    private int currentIndex;
    private int direction = 1;
    private bool isMoving;

    private void Start()
    {
        isMoving = playOnStart;
    }

    private void Update()
    {
        if (!isMoving || checkpoints == null || checkpoints.Length == 0)
        {
            return;
        }

        Transform target = checkpoints[currentIndex];
        if (target == null)
        {
            AdvanceCheckpoint();
            return;
        }

        Vector3 targetPosition = ClampAboveTerrain(target.position);
        Vector3 toTarget = targetPosition - transform.position;

        if (toTarget.magnitude <= reachDistance)
        {
            AdvanceCheckpoint();
            return;
        }

        Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        transform.position = ClampAboveTerrain(nextPosition);

        if (faceMovementDirection && toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            float turnBlend = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnBlend);
        }
    }

    public void Play()
    {
        isMoving = true;
    }

    public void Pause()
    {
        isMoving = false;
    }

    public void RestartRoute()
    {
        currentIndex = 0;
        direction = 1;
        isMoving = true;
    }

    private void AdvanceCheckpoint()
    {
        if (checkpoints.Length <= 1)
        {
            isMoving = false;
            return;
        }

        switch (loopMode)
        {
            case LoopMode.StopAtLast:
                currentIndex++;
                if (currentIndex >= checkpoints.Length)
                {
                    currentIndex = checkpoints.Length - 1;
                    isMoving = false;
                }
                break;

            case LoopMode.PingPong:
                currentIndex += direction;
                if (currentIndex >= checkpoints.Length)
                {
                    direction = -1;
                    currentIndex = checkpoints.Length - 2;
                }
                else if (currentIndex < 0)
                {
                    direction = 1;
                    currentIndex = 1;
                }
                break;

            default:
                currentIndex = (currentIndex + 1) % checkpoints.Length;
                break;
        }
    }

    private Vector3 ClampAboveTerrain(Vector3 position)
    {
        if (!keepAboveTerrain || terrain == null)
        {
            return position;
        }

        float groundHeight = terrain.SampleHeight(position) + terrain.transform.position.y;
        position.y = Mathf.Max(position.y, groundHeight + minimumGroundClearance);
        return position;
    }
}
