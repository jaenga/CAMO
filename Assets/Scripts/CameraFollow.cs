using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float followSpeed = 5f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = new Vector3(
            target.position.x,
            currentPosition.y,
            currentPosition.z);

        transform.position = Vector3.Lerp(
            currentPosition,
            targetPosition,
            followSpeed * Time.deltaTime);
    }
}
