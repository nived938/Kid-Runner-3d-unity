using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 3f, -5f);
    public float smoothSpeed = 5f;

    private void OnEnable()
    {
        // Subscribe to GameManager event
        GameManager.OnPlayerChanged += HandlePlayerChanged;
    }

    private void OnDisable()
    {
        // Unsubscribe to avoid memory leaks
        GameManager.OnPlayerChanged -= HandlePlayerChanged;
    }

    private void Start()
    {
        // Initial player reference (in case the event hasn't fired yet)
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
        }
        else
        {
            Debug.LogWarning("No GameObject found with tag 'Player' at start!");
        }
    }

    private void HandlePlayerChanged(Transform newPlayer)
    {
        // Update the camera’s target when player changes
        target = newPlayer;
        Debug.Log("CameraFollow: Target updated to new player " + newPlayer.name);
    }

    private void LateUpdate()
    {
        if (target == null || GameManager.instance == null) return;

        if (!GameManager.instance.IsGameStarted() || GameManager.instance.IsGameOver())
            return;

        Vector3 desiredPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
    }
}
