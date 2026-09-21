using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(Slider))]
public class SliderTooltip : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Tooltip References")]
    public GameObject tooltipRoot;
    public TextMeshProUGUI txtTooltip;

    [Header("Formatting")]
    [Tooltip("Text appended after the number (e.g., % or ms)")]
    public string suffix = "%";

    private Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        UpdateTooltipText(slider.value);
        
        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(false);
        }

        slider.onValueChanged.AddListener(UpdateTooltipText);
    }

    private void UpdateTooltipText(float value)
    {
        if (txtTooltip != null)
        {
            txtTooltip.text = $"{Mathf.RoundToInt(value)}{suffix}";
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (tooltipRoot != null) tooltipRoot.SetActive(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Keeps the text live as the user slides
        UpdateTooltipText(slider.value);
    }

    void OnDisable()
    {
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
    }
}