using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Designcoffers.Core
{
    /// <summary>
    /// Shared press feedback for collection UI controls. It uses only transform
    /// scale, remains responsive under repeated taps, and is safe to add to any
    /// Button without knowing a game's mechanics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TapMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Range(0.88f, 1f)] public float pressedScale = 0.94f;
        [Range(0.08f, 0.2f)] public float duration = 0.12f;

        private Vector3 restingScale;

        private void Awake()
        {
            restingScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.DOKill();
            transform.DOScale(restingScale * pressedScale, duration).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Restore();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Restore();
        }

        private void Restore()
        {
            transform.DOKill();
            transform.DOScale(restingScale, duration).SetEase(Ease.OutBack).SetLink(gameObject);
        }
    }
}
