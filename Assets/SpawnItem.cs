using UnityEngine;

public class SpawnItem : MonoBehaviour
{
    // Independent respawn toggles
    public static bool AllowFoodRespawn = true;
    public static bool AllowPoisonRespawn = true;

    public Vector2 bounds;

    public void Respawn()
    {
        // Check tag to determine whether this item is permitted to respawn
        if (CompareTag("Food") && !AllowFoodRespawn)
        {
            Destroy(gameObject);
            return;
        }

        if (CompareTag("Poison") && !AllowPoisonRespawn)
        {
            Destroy(gameObject);
            return;
        }

        // Standard respawn positioning
        transform.position = new Vector2(
            Random.Range(-bounds.x, bounds.x),
            Random.Range(-bounds.y, bounds.y)
        );
    }
}