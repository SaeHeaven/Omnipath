using UnityEngine;

public class CameraFollowWeather : MonoBehaviour
{
    [Header("Target & Offsets")]
    public Transform targetCamera;
    public Vector3 spawnOffset = new Vector3(0f, 6f, 2f);

    void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        // Follow ONLY the position of the camera, completely ignoring its rotation
        Vector3 targetPosition = targetCamera.position + Vector3.up * spawnOffset.y 
                                                        + Vector3.ProjectOnPlane(targetCamera.forward, Vector3.up).normalized * spawnOffset.z;

        transform.position = targetPosition;
        
        // Lock rotation to world coordinates (always face straight)
        transform.rotation = Quaternion.identity;
    }
}