using UnityEngine;

public class DestructibleCube : MonoBehaviour
{
    [Header("Health Settings")]
    public float health = 3f; // Takes 3 punches to destroy

    public void TakeDamage(float damageAmount)
    {
        health -= damageAmount;
        
        // This sends a text alert to your Unity Console window
        Debug.Log(gameObject.name + " took damage! Remaining Health: " + health);

        if (health <= 0f)
        {
            Debug.Log(gameObject.name + " was obliterated by Adam.");
            Destroy(gameObject); // Deletes the object from the physical world
        }
    }
}