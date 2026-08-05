using UnityEngine;

public class SkateboardPowerUp : MonoBehaviour
{
    public float duration = 7f;
    public float rotateSpeed = 180f;


    private void Update()
    {
        // rotate around Y axis
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.ActivateSkateboard(duration);
            }

            // Hide or destroy pickup
            Destroy(gameObject);
        }
    }
}
