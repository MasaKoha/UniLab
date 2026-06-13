using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.AssetVault.Sample
{
    /// <summary>
    /// dependency injection を使わずに、単体の asset vault sample を開始します。
    /// </summary>
    public sealed class AssetVaultSampleBootstrap : MonoBehaviour
    {
        [SerializeField] private string _downloadLabel = "sample";
        [SerializeField] private string _assetKey = "sample_sprite";

        private AssetVaultSamplePresenter _presenter;
        private AddressablesAssetVaultService _service;
        private GameObject _canvasObject;

        private void Awake()
        {
            EnsureEventSystem();
            _canvasObject = CreateCanvas();
            _service = new AddressablesAssetVaultService();
            var view = new AssetVaultSampleView(_canvasObject.transform);
            _presenter = new AssetVaultSamplePresenter(_service, view, _downloadLabel, _assetKey);
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            _service?.Dispose();

            // 単体実行の終了時に孤立した Canvas を残さないよう、生成した UI を破棄する。
            if (_canvasObject != null)
            {
                Destroy(_canvasObject);
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystemObject);
        }

        private static GameObject CreateCanvas()
        {
            var canvasObject = new GameObject("AssetVaultSampleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1080f, 1920f);
            canvasScaler.matchWidthOrHeight = 0.5f;

            var rectTransform = canvasObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            return canvasObject;
        }
    }
}
