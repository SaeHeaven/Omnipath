using UnityEngine;

public interface IDamageable
{
    // Any script with this interface MUST have this function
    void TakeDamage(float amount, Vector3 knockbackDir);
}