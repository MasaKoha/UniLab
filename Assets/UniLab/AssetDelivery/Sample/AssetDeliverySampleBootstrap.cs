using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.AssetDelivery.Sample
{
    /// <summary>
    /// Starts the standalone asset delivery sample without dependency injection.
    /// </summary>
    public sealed class AssetDeliverySampleBootstrap : MonoBehaviour
    {
        [SerializeField] private string _downloadLabel = "sample";
        [SerializeField] private string _assetKey = "sample_sprite";

        private AssetDeliverySamplePresenter _presenter;
        private AddressablesAssetDeliveryService _service;
        private GameObject _canvasObject;

        private void Awake()
        {
            EnsureEventSystem();
            _canvasObject = CreateCanvas();
            _service = new AddressablesAssetDeliveryService();
            var view = new AssetDeliverySampleView(_canvasObject.transform);
            _presenter = new AssetDeliverySamplePresenter(_service, view, _downloadLabel, _assetKey);
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            _service?.Dispose();

            // Destroy the generated UI so a standalone teardown does not leave an orphan Canvas.
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
            var canvasObject = new GameObject("AssetDeliverySampleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

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
