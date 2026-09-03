using NUnit.Framework;
using TMPro;
using UniLab.UI;
using UnityEngine;

namespace UniLab.Tests.EditMode.UI
{
    /// <summary>
    /// NumberBounceText の即時反映と開始状態を検証する EditMode テスト。
    /// </summary>
    public class NumberBounceTextTest
    {
        private GameObject _gameObject;
        private NumberBounceText _numberBounceText;
        private TextMeshProUGUI _textMeshPro;

        /// <summary>
        /// SetValue は現在の書式で即時表示する。
        /// </summary>
        [Test]
        public void SetValue_AppliesFormatToText()
        {
            CreateView();
            _numberBounceText.Format = "Lv{0}";

            _numberBounceText.SetValue(12);

            Assert.That(_textMeshPro.text, Is.EqualTo("Lv12"));
        }

        /// <summary>
        /// Bounce 開始直後は再生中フラグが立つ。
        /// </summary>
        [Test]
        public void Bounce_ImmediatelyAfterStart_IsPlayingTrue()
        {
            CreateView();

            _numberBounceText.Bounce(5, 0.25f, 1.6f);

            Assert.That(_numberBounceText.IsPlaying, Is.True);
        }

        /// <summary>
        /// Format 変更後は保持値を使って表示も更新する。
        /// </summary>
        [Test]
        public void Format_AfterValueChange_RefreshesDisplayedText()
        {
            CreateView();
            _numberBounceText.SetValue(8);

            _numberBounceText.Format = "Combo {0}";

            Assert.That(_textMeshPro.text, Is.EqualTo("Combo 8"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        private void CreateView()
        {
            _gameObject = new GameObject("NumberBounceText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            _numberBounceText = _gameObject.AddComponent<NumberBounceText>();
            _textMeshPro = _gameObject.GetComponent<TextMeshProUGUI>();
        }
    }
}
