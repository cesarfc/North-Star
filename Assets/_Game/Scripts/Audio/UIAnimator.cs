using System.Collections;
using UnityEngine;

namespace NorthStar.Audio
{
    /// <summary>
    /// Lightweight UI tween helper: PopIn/PopOut (scale), FadeIn/FadeOut (CanvasGroup
    /// alpha) and SlideIn (anchored position). Implemented with plain Unity coroutines —
    /// <b>no DOTween dependency</b> — so it compiles and runs engine-only.
    ///
    /// <para>All tweens run on <b>unscaled</b> time, the coroutine equivalent of DOTween's
    /// <c>.SetUpdate(true)</c>, so menus and popups still animate while the game is paused
    /// (<c>Time.timeScale == 0</c>).</para>
    ///
    /// <para>If DOTween is added to the project later, define the <c>DOTWEEN</c> scripting
    /// symbol to route the same public API through DOTween; the coroutine path above is the
    /// default and is what ships today. The public signatures match INTERFACE.md exactly.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class UIAnimator : MonoBehaviour
    {
        [Tooltip("Distance (in anchored units) a SlideIn travels from off its target position.")]
        [SerializeField] private float _slideDistance = 200f;

        /// <summary>Pop a RectTransform in by scaling 0 → 1 with an overshoot ease.</summary>
        public void PopIn(RectTransform target, float duration = 0.25f)
        {
            if (target == null) return;
#if DOTWEEN
            target.localScale = Vector3.zero;
            target.gameObject.SetActive(true);
            DG.Tweening.ShortcutExtensions.DOScale(target, Vector3.one, duration)
                .SetEase(DG.Tweening.Ease.OutBack).SetUpdate(true);
#else
            RestartTween(target, CoScale(target, Vector3.zero, Vector3.one, duration, Overshoot, activateOnStart: true));
#endif
        }

        /// <summary>Pop a RectTransform out by scaling 1 → 0, deactivating it when done.</summary>
        public void PopOut(RectTransform target, float duration = 0.2f)
        {
            if (target == null) return;
#if DOTWEEN
            DG.Tweening.ShortcutExtensions.DOScale(target, Vector3.zero, duration)
                .SetEase(DG.Tweening.Ease.InBack).SetUpdate(true)
                .OnComplete(() => target.gameObject.SetActive(false));
#else
            RestartTween(target, CoScale(target, target.localScale, Vector3.zero, duration, EaseInBack, deactivateOnEnd: true));
#endif
        }

        /// <summary>Fade a CanvasGroup in from its current alpha to 1; makes it interactable.</summary>
        public void FadeIn(CanvasGroup group, float duration = 0.3f)
        {
            if (group == null) return;
#if DOTWEEN
            group.gameObject.SetActive(true);
            DG.Tweening.ShortcutExtensions.DOFade(group, 1f, duration).SetUpdate(true)
                .OnComplete(() => SetInteractable(group, true));
#else
            RestartTween(group, CoFade(group, group.alpha, 1f, duration, interactableAtEnd: true, activateOnStart: true));
#endif
        }

        /// <summary>Fade a CanvasGroup out to 0; disables interaction and deactivates it when done.</summary>
        public void FadeOut(CanvasGroup group, float duration = 0.3f)
        {
            if (group == null) return;
#if DOTWEEN
            SetInteractable(group, false);
            DG.Tweening.ShortcutExtensions.DOFade(group, 0f, duration).SetUpdate(true)
                .OnComplete(() => group.gameObject.SetActive(false));
#else
            SetInteractable(group, false);
            RestartTween(group, CoFade(group, group.alpha, 0f, duration, interactableAtEnd: false, deactivateOnEnd: true));
#endif
        }

        /// <summary>
        /// Slide a RectTransform in from off-screen in the given direction to its current
        /// anchored position.
        /// </summary>
        public void SlideIn(RectTransform target, SlideDirection dir, float duration = 0.3f)
        {
            if (target == null) return;

            Vector2 end = target.anchoredPosition;
            Vector2 start = end + OffsetFor(dir);
#if DOTWEEN
            target.anchoredPosition = start;
            target.gameObject.SetActive(true);
            DG.Tweening.ShortcutExtensions.DOAnchorPos(target, end, duration)
                .SetEase(DG.Tweening.Ease.OutCubic).SetUpdate(true);
#else
            RestartTween(target, CoSlide(target, start, end, duration));
#endif
        }

        // ── Coroutine implementations (DOTween-free default) ───────────────────────

        private IEnumerator CoScale(RectTransform target, Vector3 from, Vector3 to,
            float duration, System.Func<float, float> ease,
            bool activateOnStart = false, bool deactivateOnEnd = false)
        {
            if (activateOnStart) target.gameObject.SetActive(true);
            target.localScale = from;

            float t = 0f;
            while (t < duration && duration > 0f)
            {
                t += Time.unscaledDeltaTime;
                float k = ease(Mathf.Clamp01(t / duration));
                target.localScale = Vector3.LerpUnclamped(from, to, k);
                yield return null;
            }

            target.localScale = to;
            if (deactivateOnEnd) target.gameObject.SetActive(false);
        }

        private IEnumerator CoFade(CanvasGroup group, float from, float to, float duration,
            bool interactableAtEnd, bool activateOnStart = false, bool deactivateOnEnd = false)
        {
            if (activateOnStart) group.gameObject.SetActive(true);
            group.alpha = from;

            float t = 0f;
            while (t < duration && duration > 0f)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }

            group.alpha = to;
            SetInteractable(group, interactableAtEnd);
            if (deactivateOnEnd) group.gameObject.SetActive(false);
        }

        private IEnumerator CoSlide(RectTransform target, Vector2 from, Vector2 to, float duration)
        {
            target.gameObject.SetActive(true);
            target.anchoredPosition = from;

            float t = 0f;
            while (t < duration && duration > 0f)
            {
                t += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(t / duration));
                target.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                yield return null;
            }

            target.anchoredPosition = to;
        }

        // ── Tween bookkeeping ──────────────────────────────────────────────────────

        // One running coroutine per target so re-triggering a tween cancels the old one.
        private readonly System.Collections.Generic.Dictionary<Object, Coroutine> _running =
            new System.Collections.Generic.Dictionary<Object, Coroutine>();

        private void RestartTween(Object key, IEnumerator routine)
        {
            if (_running.TryGetValue(key, out var existing) && existing != null)
                StopCoroutine(existing);
            _running[key] = StartCoroutine(routine);
        }

        private static void SetInteractable(CanvasGroup g, bool on)
        {
            g.interactable = on;
            g.blocksRaycasts = on;
        }

        private Vector2 OffsetFor(SlideDirection dir)
        {
            switch (dir)
            {
                case SlideDirection.Left:  return new Vector2(-_slideDistance, 0f);
                case SlideDirection.Right: return new Vector2(_slideDistance, 0f);
                case SlideDirection.Up:    return new Vector2(0f, _slideDistance);
                case SlideDirection.Down:  return new Vector2(0f, -_slideDistance);
                default:                   return Vector2.zero;
            }
        }

        // ── Easing (so we don't need DOTween's Ease enum) ──────────────────────────

        private static float Overshoot(float k)
        {
            // OutBack ease: gentle overshoot past 1 then settle.
            const float s = 1.70158f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }

        private static float EaseInBack(float k)
        {
            const float s = 1.70158f;
            return k * k * ((s + 1f) * k - s);
        }

        private static float EaseOutCubic(float k)
        {
            float f = k - 1f;
            return f * f * f + 1f;
        }
    }
}
