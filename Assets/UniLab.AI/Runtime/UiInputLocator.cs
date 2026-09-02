using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>
    /// 入力注入と待機判定で同じ UI 解決規則を使い回し、押せる判断の不一致を避けるための探索器です。
    /// </summary>
    public static class UiInputLocator
    {
        /// <summary>
        /// パス末尾一致で GameObject を解決し、シナリオ JSON を短い名前で保つための入口です。
        /// </summary>
        public static GameObject FindByPathSegment(string objectPath)
        {
            if (string.IsNullOrEmpty(objectPath))
            {
                return null;
            }

            var pathSegments = objectPath.Split('/');
            var candidateTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (var candidateIndex = 0; candidateIndex < candidateTransforms.Length; candidateIndex++)
            {
                var candidateTransform = candidateTransforms[candidateIndex];
                if (candidateTransform.name != pathSegments[pathSegments.Length - 1])
                {
                    continue;
                }

                if (pathSegments.Length == 1)
                {
                    return candidateTransform.gameObject;
                }

                if (DoesPathMatch(candidateTransform, pathSegments))
                {
                    return candidateTransform.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// UI 要素名指定を座標指定と同じ扱いに落とし込み、ポインタ系 API を共通化するための中心座標です。
        /// </summary>
        public static bool TryGetElementCenter(string objectPath, out Vector2 screenPosition)
        {
            screenPosition = default;
            var target = FindByPathSegment(objectPath);
            if (target == null)
            {
                return false;
            }

            var rectTransform = target.transform as RectTransform;
            if (rectTransform == null)
            {
                return false;
            }

            Canvas.ForceUpdateCanvases();
            var worldCenter = rectTransform.TransformPoint(rectTransform.rect.center);
            screenPosition = RectTransformUtility.WorldToScreenPoint(ResolveCanvasCamera(target), worldCenter);
            return true;
        }

        /// <summary>
        /// モーダルやフェードの背面を押さないため、ランナーと同じ遮蔽判定をここへ集約します。
        /// </summary>
        public static GameObject FindBlockingObject(GameObject target)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            var targetRectTransform = target.transform as RectTransform;
            if (targetRectTransform == null)
            {
                return null;
            }

            var screenPoint = RectTransformUtility.WorldToScreenPoint(ResolveCanvasCamera(target), targetRectTransform.TransformPoint(targetRectTransform.rect.center));
            var pointerEventData = new PointerEventData(eventSystem)
            {
                position = screenPoint,
            };

            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerEventData, raycastResults);
            if (raycastResults.Count == 0)
            {
                return null;
            }

            var frontMostObject = raycastResults[0].gameObject;
            if (IsSelfOrDescendant(frontMostObject, target))
            {
                return null;
            }

            return frontMostObject;
        }

        /// <summary>
        /// 条件待ちとリプレイ同期で同じ「押せる」判定を使い、記録時だけ通る入力をなくすための判定です。
        /// </summary>
        public static bool IsInteractable(GameObject target)
        {
            var selectable = target.GetComponent<Selectable>();
            if (selectable == null)
            {
                return true;
            }

            return selectable.IsInteractable();
        }

        /// <summary>
        /// 実要素へ直接 submit を送る旧ショートカットを残しつつ、Input Vocabulary 専用ランナーから再利用するための入口です。
        /// </summary>
        public static bool TrySubmit(GameObject target)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || target == null)
            {
                return false;
            }

            var eventData = new BaseEventData(eventSystem);
            return ExecuteEvents.Execute(target, eventData, ExecuteEvents.submitHandler);
        }

        /// <summary>
        /// フォーカス移動完了を名前で待てるようにし、ゲームパッド UI の同期点を取るための判定です。
        /// </summary>
        public static bool IsFocused(string objectPath)
        {
            var selectedObject = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selectedObject == null)
            {
                return false;
            }

            var target = FindByPathSegment(objectPath);
            return target != null && selectedObject == target;
        }

        /// <summary>
        /// シナリオ待機と replay anchor の両方で同じシーン判定を共有するための判定です。
        /// </summary>
        public static bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return true;
            }

            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (scene.isLoaded && scene.name == sceneName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 文字待機を画像に頼らず実現し、TextMeshPro ベース UI のロード完了を同期するための判定です。
        /// </summary>
        public static bool HasVisibleText(string expectedText)
        {
            if (string.IsNullOrEmpty(expectedText))
            {
                return true;
            }

            var texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude);
            for (var textIndex = 0; textIndex < texts.Length; textIndex++)
            {
                var text = texts[textIndex];
                if (!text.isActiveAndEnabled)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                if (text.text.Contains(expectedText))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// anchor や待機条件を 1 箇所で評価し、記録時と再生時の解釈差を防ぐための判定です。
        /// </summary>
        public static bool IsAnchorSatisfied(InputReplayAnchor anchor)
        {
            if (anchor == null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(anchor.waitForScene) && !IsSceneLoaded(anchor.waitForScene))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(anchor.waitForText) && !HasVisibleText(anchor.waitForText))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(anchor.waitForFocus) && !IsFocused(anchor.waitForFocus))
            {
                return false;
            }

            if (string.IsNullOrEmpty(anchor.waitForObject))
            {
                return true;
            }

            var target = FindByPathSegment(anchor.waitForObject);
            if (target == null)
            {
                return false;
            }

            if (FindBlockingObject(target) != null)
            {
                return false;
            }

            return IsInteractable(target);
        }

        /// <summary>
        /// 画面内の同一要素を識別できるパスを返し、差分警告とログの説明を人間が追いやすくするためのパスです。
        /// </summary>
        public static string BuildPath(Transform targetTransform)
        {
            if (targetTransform == null)
            {
                return string.Empty;
            }

            var pathParts = new List<string>();
            var currentTransform = targetTransform;
            while (currentTransform != null)
            {
                pathParts.Insert(0, currentTransform.name);
                currentTransform = currentTransform.parent;
            }

            return string.Join("/", pathParts);
        }

        private static Camera ResolveCanvasCamera(GameObject target)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera;
        }

        private static bool DoesPathMatch(Transform targetTransform, string[] pathSegments)
        {
            var currentTransform = targetTransform;
            for (var pathIndex = pathSegments.Length - 1; pathIndex >= 0; pathIndex--)
            {
                if (currentTransform == null)
                {
                    return false;
                }

                if (currentTransform.name != pathSegments[pathIndex])
                {
                    return false;
                }

                currentTransform = currentTransform.parent;
            }

            return true;
        }

        private static bool IsSelfOrDescendant(GameObject candidate, GameObject target)
        {
            var currentTransform = candidate.transform;
            while (currentTransform != null)
            {
                if (currentTransform.gameObject == target)
                {
                    return true;
                }

                currentTransform = currentTransform.parent;
            }

            return false;
        }
    }
}
#endif
