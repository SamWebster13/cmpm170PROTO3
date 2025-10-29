using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.12f;
    public Vector3 offset = new Vector3(0f, 20f, 0f); // height above player
    Vector3 _vel;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 desired = new Vector3(target.position.x, 0f, target.position.z) + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
        // Main Camera is a child at local (0,0,0), rotation already top-down
    }
}
