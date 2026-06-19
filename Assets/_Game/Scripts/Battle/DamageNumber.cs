using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>A floating combat number that rises and fades, then self-destructs. Attach to the prefab.</summary>
public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private float _lifetime = 0.8f;
    [SerializeField] private float _riseDistance = 40f;
    [SerializeField] private Color _damageColor = Color.white;
    [SerializeField] private Color _healColor = Color.green;

    /// <summary>Show the number and start the rise/fade animation, then destroy this object.</summary>
    /// <param name="anchoredPosition">Start position in the container's anchored space.</param>
    /// <param name="amount">Magnitude to display.</param>
    /// <param name="isHeal">True to style as a heal (prefixed +, heal color).</param>
    public void Play(Vector2 anchoredPosition, int amount, bool isHeal)
    {
        var rect = transform as RectTransform;
        if (rect != null) rect.anchoredPosition = anchoredPosition;

        if (_label != null)
        {
            _label.text = isHeal ? $"+{amount}" : amount.ToString();
            _label.color = isHeal ? _healColor : _damageColor;
        }

        StartCoroutine(CoRiseAndFade());
    }

    private IEnumerator CoRiseAndFade()
    {
        var rect = transform as RectTransform;
        Vector2 start = rect != null ? rect.anchoredPosition : Vector2.zero;
        float elapsed = 0f;

        while (elapsed < _lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _lifetime);

            if (rect != null)
                rect.anchoredPosition = start + Vector2.up * (_riseDistance * t);
            if (_label != null)
            {
                var c = _label.color;
                c.a = 1f - t;
                _label.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
