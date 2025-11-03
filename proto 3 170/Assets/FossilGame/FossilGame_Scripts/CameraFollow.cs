using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -5f);
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 cameraRotation = new Vector3(90f, 0f, 0f);
    
    void LateUpdate()
    {
        if (target == null) return;
        
        // Calculate and move to desired position
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // Set camera rotation
        transform.rotation = Quaternion.Euler(cameraRotation);
    }
}

