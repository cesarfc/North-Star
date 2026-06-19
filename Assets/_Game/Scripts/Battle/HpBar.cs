using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A single unit's HP bar (slider + labels). Attach to the HP-bar prefab and wire fields.</summary>
public class HpBar : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TMP_Text _nameLabel;
    [SerializeField] private TMP_Text _valueLabel;
    [SerializeField] private CanvasGroup _canvasGroup;

    /// <summary>Set the unit name and initial HP values.</summary>
    public void Initialize(string unitName, int current, int max)
    {
        if (_nameLabel != null) _nameLabel.text = unitName;
        SetValue(current, max);
    }

    /// <summary>Update the bar fill and value label.</summary>
    public void SetValue(int current, int max)
    {
        if (_slider != null)
        {
            _slider.maxValue = Mathf.Max(1, max);
            _slider.value = Mathf.Clamp(current, 0, max);
        }
        if (_valueLabel != null) _valueLabel.text = $"{current}/{max}";
    }

    /// <summary>Visually mark the unit as dead (empty bar, dimmed).</summary>
    public void SetDead()
    {
        SetValue(0, _slider != null ? Mathf.RoundToInt(_slider.maxValue) : 0);
        if (_canvasGroup != null) _canvasGroup.alpha = 0.4f;
    }
}
