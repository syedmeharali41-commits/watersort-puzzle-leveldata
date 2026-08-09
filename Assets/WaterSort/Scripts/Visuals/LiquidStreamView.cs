using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Designcoffers.Core;

namespace Designcoffers.WaterSort.Visuals
{
    public class LiquidStreamView : MonoBehaviour
    {
        public Image streamImage;

        private void Awake()
        {
            if (streamImage == null) streamImage = GetComponent<Image>();
            if (streamImage != null) streamImage.enabled = false;
        }

        public void PlayPourStream(Vector3 startWorldPos, Vector3 endWorldPos, Color liquidColor, float duration, Action onComplete)
        {
            StartCoroutine(AnimateStream(startWorldPos, endWorldPos, liquidColor, duration, onComplete));
        }

        private IEnumerator AnimateStream(Vector3 startPos, Vector3 endPos, Color color, float duration, Action onComplete)
        {
            if (streamImage != null)
            {
                streamImage.DOKill();
                streamImage.color = color;
                streamImage.enabled = true;
                
                RectTransform rt = streamImage.rectTransform;
                rt.position = startPos;
                
                Vector3 diff = endPos - startPos;
                float distance = diff.magnitude;
                float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

                rt.rotation = Quaternion.Euler(0, 0, angle - 90f);
                rt.sizeDelta = new Vector2(16f, 0f);

                bool complete = false;
                Sequence streamSequence = DOTween.Sequence();
                streamSequence.Append(rt.DOSizeDelta(new Vector2(16f, distance), duration * 0.42f).SetEase(Ease.OutCubic));
                streamSequence.AppendInterval(duration * 0.22f);
                streamSequence.Append(streamImage.DOFade(0f, duration * 0.18f).SetEase(Ease.OutQuad));
                streamSequence.SetLink(gameObject).OnComplete(() => complete = true);
                yield return new WaitUntil(() => complete);

                streamImage.enabled = false;
                Color resetColor = streamImage.color;
                resetColor.a = 1f;
                streamImage.color = resetColor;
            }
            else
            {
                yield return new WaitForSeconds(duration);
            }

            onComplete?.Invoke();
        }
    }
}
