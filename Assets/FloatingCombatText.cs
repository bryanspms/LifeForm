using UnityEngine;
using TMPro;

public class FloatingCombatText : MonoBehaviour
{
    private TextMeshPro textMesh;
    public float moveSpeed = 1.6f;
    public float lifetime = 0.85f;
    public Vector3 moveDirection = new Vector3(0f, 1f, 0f);

    private Color initialColor;
    private float timer = 0f;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh != null)
        {
            textMesh.sortingOrder = 50;
            initialColor = textMesh.color;
        }
    }
    
    public void Setup(int damageAmount, bool isCritical = false, bool isDamage = true)
    {
        if (textMesh == null)
            textMesh = GetComponent<TextMeshPro>();

        if (textMesh == null) return;

        // 1. Force Overflow and disable wrapping via the modern property
        textMesh.enableAutoSizing = false;
        textMesh.textWrappingMode = TextWrappingModes.NoWrap;
        textMesh.overflowMode = TextOverflowModes.Overflow;

        // 2. Format text
        string prefix = isDamage ? "-" : "+";
        textMesh.text = $"<b>{prefix}{damageAmount}</b>";

        // 3. Match the proven overhead text scale (overhead is 5f, so 6.5f-8f is bold and legible)
        textMesh.fontSize = isCritical ? 8f : 6.5f;

        // 4. Clean standard scale
        transform.localScale = Vector3.one;

        // 5. Colors & Sorting
        if (isDamage)
        {
            initialColor = isCritical ? new Color(1f, 0.85f, 0.1f, 1f) : new Color(1f, 0.25f, 0.25f, 1f);
        }
        else
        {
            initialColor = new Color(0.2f, 1f, 0.2f, 1f);
        }

        initialColor.a = 1f;
        textMesh.color = initialColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.sortingOrder = 50;
        textMesh.ForceMeshUpdate();

        timer = 0f;
    }

    private void Update()
    {
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / lifetime);

        if (textMesh != null)
        {
            Color c = initialColor;
            c.a = Mathf.Lerp(1f, 0f, progress);
            textMesh.color = c;
        }

        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}