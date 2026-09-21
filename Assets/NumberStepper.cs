using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NumberStepper : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Button btnUp;
    [SerializeField] private Button btnDown;

    [Header("Configuration")]
    public int minValue = 1;
    public int maxValue = 100;
    public int step = 1;
    [SerializeField] private int currentValue = 0;

    public int Value
    {
        get => currentValue;
        set
        {
            currentValue = Mathf.Clamp(value, minValue, maxValue);
            UpdateDisplay();
        }
    }

    void Awake()
    {
        if (btnUp != null) btnUp.onClick.AddListener(Increment);
        if (btnDown != null) btnDown.onClick.AddListener(Decrement);
        UpdateDisplay();
    }

    public void Increment()
    {
        Value += step;
    }

    public void Decrement()
    {
        Value -= step;
    }

    private void UpdateDisplay()
    {
        if (valueText != null)
        {
            valueText.text = currentValue.ToString();
        }

        // Optional: Dim/disable buttons when hitting min or max
        if (btnUp != null) btnUp.interactable = (currentValue < maxValue);
        if (btnDown != null) btnDown.interactable = (currentValue > minValue);
    }
}