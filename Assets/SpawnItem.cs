using UnityEngine;

public class SpawnItem : MonoBehaviour
{
    public static bool AllowRespawn = true;

    // Make this public so SimulationManager can assign it directly if needed
    public Vector2 bounds = new Vector2(25f, 15f);

    public void SetBounds(Vector2 newBounds)
    {
        bounds = newBounds;
    }

    public void Respawn()
    {
        if (AllowRespawn)
        {
            float x = Random.Range(-bounds.x + 1f, bounds.x - 1f);
            float y = Random.Range(-bounds.y + 1f, bounds.y - 1f);
            transform.position = new Vector3(x, y, 0f);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}