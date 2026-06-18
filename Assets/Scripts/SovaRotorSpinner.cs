using UnityEngine;

public class SovaRotorSpinner : MonoBehaviour
{
    public Vector3 localAxis = Vector3.up;
    public float idleDegreesPerSecond = 900f;
    public float flightDegreesPerSecond = 2400f;
    public float acceleration = 8f;
    public bool spinInEditMode;

    private float currentSpeed;

    private void OnEnable()
    {
        currentSpeed = idleDegreesPerSecond;
    }

    private void Update()
    {
        if (!Application.isPlaying && !spinInEditMode)
        {
            return;
        }

        float targetSpeed = Application.isPlaying ? flightDegreesPerSecond : idleDegreesPerSecond;
        float blend = 1f - Mathf.Exp(-acceleration * Time.deltaTime);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, blend);
        transform.Rotate(localAxis.normalized, currentSpeed * Time.deltaTime, Space.Self);
    }
}
