using UnityEngine;

public class FloatingDiamonds : MonoBehaviour
{
    public float floatAmplitude = 0.25f; // how high it moves up and down
    public float floatFrequency = 2f;    // how fast it moves

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // Calculate new Y position
        float newY = startPos.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;

        // Apply floating motion while keeping rotation
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}
