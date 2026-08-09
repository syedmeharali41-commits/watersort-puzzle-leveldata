using System;
using System.Collections;
using UnityEngine;

namespace Designcoffers.Core
{
    /// <summary>
    /// Micro-animation utility wrapper providing clean 150-300ms ease-out animations across all UI & gameplay interactions.
    /// Provides coroutine-based fallbacks and DOTween compatibility.
    /// </summary>
    public static class TweenUtils
    {
        public static IEnumerator AnimateScale(Transform transform, Vector3 targetScale, float duration, Action onComplete = null)
        {
            if (transform == null) yield break;

            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-Out-Quad curve
                float easeT = 1f - (1f - t) * (1f - t);
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, easeT);
                yield return null;
            }

            if (transform != null)
            {
                transform.localScale = targetScale;
            }
            onComplete?.Invoke();
        }

        public static IEnumerator AnimatePosition(Transform transform, Vector3 targetPos, float duration, Action onComplete = null)
        {
            if (transform == null) yield break;

            Vector3 startPos = transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeT = 1f - (1f - t) * (1f - t);
                transform.position = Vector3.Lerp(startPos, targetPos, easeT);
                yield return null;
            }

            if (transform != null)
            {
                transform.position = targetPos;
            }
            onComplete?.Invoke();
        }

        public static IEnumerator AnimateShake(Transform transform, float duration, float strength = 10f)
        {
            if (transform == null) yield break;

            Vector3 originalPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damp = 1f - (elapsed / duration);
                float offsetX = UnityEngine.Random.Range(-1f, 1f) * strength * damp;
                float offsetY = UnityEngine.Random.Range(-1f, 1f) * strength * damp;
                transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }

            if (transform != null)
            {
                transform.localPosition = originalPos;
            }
        }
    }
}
