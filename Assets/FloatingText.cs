using System.Collections;
using TMPro;
using UnityEngine;

public class FloatingText : MonoBehaviour
{
    private TextMeshPro tmp;
    private float floatSpeed = 1.8f;
    private float duration = 0.8f;

    public void Initialize(string text, Color color, Vector3 spawnPos)
	{
	    transform.position = spawnPos;

	    tmp = gameObject.AddComponent<TextMeshPro>();
	    tmp.text = text;
	    
	    // Tripled font size (was 8f -> now 24f)
	    tmp.fontSize = 24f; 
	    tmp.color = color;
	    tmp.alignment = TextAlignmentOptions.Center;
	    tmp.sortingOrder = 30; // High sorting order to stay above all sprites
	    tmp.textWrappingMode = TextWrappingModes.NoWrap;

	    // Expanded bounds to prevent clipping large text
	    RectTransform rt = GetComponent<RectTransform>();
	    rt.sizeDelta = new Vector2(12f, 4f);

	    StartCoroutine(FloatAndFadeRoutine());
	}

    private IEnumerator FloatAndFadeRoutine()
    {
        float elapsed = 0f;
        Color initialColor = tmp.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // Float upward slightly drifting
            transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

            // Smooth alpha fade-out
            tmp.color = new Color(initialColor.r, initialColor.g, initialColor.b, 1f - progress);

            yield return null;
        }

        Destroy(gameObject);
    }
}