using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Designcoffers.Core;

namespace Designcoffers.WaterSort.Visuals
{
    public class TubeView : MonoBehaviour
    {
        [Header("UI References")]
        public int tubeIndex;
        public RectTransform tubeContainer;
        public Image tubeBackground;
        public Image tubeOutline;
        public Image selectionGlow;
        public RectTransform segmentsParent;
        public GameObject segmentPrefab;

        [Header("State")]
        public int capacity = 4;
        public bool isSelected = false;

        private Vector3 originalLocalPos;
        private List<Image> segmentImages = new List<Image>();
        private Image lockBadge;
        private Text lockText;
        private bool isLocked = false;

        private void Awake()
        {
            if (tubeContainer != null)
            {
                originalLocalPos = tubeContainer.localPosition;
            }
            else
            {
                originalLocalPos = transform.localPosition;
            }
        }

        public void Initialize(int index, int cap, List<int> initialColors)
        {
            tubeIndex = index;
            capacity = cap;
            isSelected = false;
            isLocked = false;

            if (tubeContainer != null)
            {
                tubeContainer.localPosition = originalLocalPos;
            }

            if (selectionGlow != null)
            {
                selectionGlow.gameObject.SetActive(false);
                selectionGlow.color = new Color(0.976f, 0.451f, 0.086f, 0.4f); // Orange glow
            }

            if (lockBadge != null)
            {
                lockBadge.gameObject.SetActive(false);
            }

            RenderSegments(initialColors);
        }

        /// <summary>
        /// Shows the locked-tube lever state: an orange badge with the moves-until-release
        /// countdown. remainingMoves &lt;= 0 hides the badge.
        /// </summary>
        public void SetLocked(bool locked, int remainingMoves)
        {
            isLocked = locked;
            if (lockBadge == null) lockBadge = CreateLockBadge();
            if (lockBadge == null) return;

            lockBadge.DOKill();
            if (!locked)
            {
                if (lockBadge.gameObject.activeSelf)
                {
                    lockBadge.DOFade(0f, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject)
                        .OnComplete(() => { if (lockBadge != null) lockBadge.gameObject.SetActive(false); });
                }
                return;
            }

            lockBadge.gameObject.SetActive(true);
            lockBadge.DOFade(0.95f, 0.15f).SetEase(Ease.OutQuad).SetLink(gameObject);
            if (lockText != null) lockText.text = remainingMoves > 0 ? remainingMoves.ToString() : string.Empty;
        }

        private Image CreateLockBadge()
        {
            if (transform == null) return null;
            RectTransform badgeRt = new GameObject("LockBadge").AddComponent<RectTransform>();
            badgeRt.SetParent(tubeContainer != null ? tubeContainer : transform, false);
            badgeRt.anchorMin = new Vector2(0.5f, 1f);
            badgeRt.anchorMax = new Vector2(0.5f, 1f);
            badgeRt.anchoredPosition = new Vector2(0f, -4f);
            badgeRt.sizeDelta = new Vector2(30f, 20f);

            Image badge = badgeRt.gameObject.AddComponent<Image>();
            // Orange accent — the locked lever is an active game element.
            badge.color = new Color(0.976f, 0.451f, 0.086f, 0.95f);
            badge.raycastTarget = false;

            Text text = badgeRt.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = string.Empty;
            text.raycastTarget = false;
            lockText = text;
            return badge;
        }

        public void RenderSegments(List<int> colors)
        {
            Transform parent = segmentsParent != null ? segmentsParent : transform;

            // Clear old segment images safely
            List<GameObject> childrenToDestroy = new List<GameObject>();
            foreach (Transform child in parent)
            {
                childrenToDestroy.Add(child.gameObject);
            }
            foreach (var child in childrenToDestroy)
            {
                if (child == null) continue;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            segmentImages.Clear();

            // Render from bottom to top
            for (int i = 0; i < capacity; i++)
            {
                GameObject segObj;
                if (segmentPrefab != null)
                {
                    segObj = Instantiate(segmentPrefab, parent);
                }
                else
                {
                    segObj = new GameObject($"Segment_{i}");
                    segObj.transform.SetParent(parent, false);
                    segObj.AddComponent<Image>();
                }

                RectTransform rt = segObj.GetComponent<RectTransform>();
                if (rt == null) rt = segObj.AddComponent<RectTransform>();
                
                // Position segment inside tube with padding
                rt.anchorMin = new Vector2(0.06f, (float)i / capacity + 0.015f);
                rt.anchorMax = new Vector2(0.94f, (float)(i + 1) / capacity - 0.015f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                Image segImg = segObj.GetComponent<Image>();
                if (segImg == null) segImg = segObj.AddComponent<Image>();

                if (colors != null && i < colors.Count && colors[i] > 0)
                {
                    segImg.color = ColorPalette.GetColor(colors[i]);
                    segImg.enabled = true;
                }
                else
                {
                    segImg.color = Color.clear;
                    segImg.enabled = false;
                }

                segmentImages.Add(segImg);
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            Transform targetTransform = tubeContainer != null ? tubeContainer : transform;
            targetTransform.DOKill();

            float targetY = selected ? originalLocalPos.y + 30f : originalLocalPos.y;
            targetTransform.DOLocalMoveY(targetY, 0.22f).SetEase(Ease.OutBack).SetLink(gameObject);
            targetTransform.DOScale(selected ? 1.035f : 1f, 0.18f).SetEase(Ease.OutQuad).SetLink(gameObject);

            if (selectionGlow != null)
            {
                selectionGlow.DOKill();
                if (selected)
                {
                    selectionGlow.gameObject.SetActive(true);
                    selectionGlow.DOFade(0.55f, 0.16f).SetEase(Ease.OutQuad).SetLink(gameObject);
                }
                else if (selectionGlow.gameObject.activeSelf)
                {
                    selectionGlow.DOFade(0f, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject)
                        .OnComplete(() =>
                        {
                            if (selectionGlow != null) selectionGlow.gameObject.SetActive(false);
                        });
                }
            }
        }

        public void PlayShakeAnimation()
        {
            RectTransform targetTransform = tubeContainer != null ? tubeContainer : transform as RectTransform;
            if (targetTransform == null) return;
            targetTransform.DOKill();
            targetTransform.DOShakeAnchorPos(0.22f, new Vector2(10f, 0f), 18, 90f, false, true)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }

        public void PlayLiquidSettle()
        {
            for (int i = 0; i < segmentImages.Count; i++)
            {
                Image segment = segmentImages[i];
                if (segment == null || !segment.enabled) continue;
                segment.DOKill();
                segment.transform.localScale = new Vector3(1f, 0.82f, 1f);
                segment.transform.DOScaleY(1f, 0.15f).SetEase(Ease.OutBack).SetLink(segment.gameObject);
            }
        }
    }
}
