using R3;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.UI.Focus
{
    /// <summary>
    /// アクティブなフォーカスグリッドのスタックを管理し、方向入力に応じて選択を切り替える契約。
    /// </summary>
    public interface IFocusNavigator
    {
        /// <summary>アクティブなグリッドを積む。候補パネル等の一時的なフォーカス領域はこれで切り替える。</summary>
        void PushGrid(FocusGrid grid);

        /// <summary>積んだグリッドを降ろす。指定グリッドがスタック最上位でない場合は何もしない。</summary>
        void PopGrid(FocusGrid grid);

        /// <summary>指定 Selectable を選択状態にし、列記憶を同期する。</summary>
        void SetSelected(Selectable selectable);

        /// <summary>アクティブグリッドの先頭有効セルへフォーカスする。</summary>
        void FocusFirst();

        /// <summary>方向入力ストリームを購読して方向解決を開始する。所有者が初期化時に一度だけ呼ぶ。</summary>
        void Initialize(Observable<FocusDirection> moveStream);

        /// <summary>
        /// 操作対象の EventSystem を差し替える。EventSystem はシーンごとに入れ替わるため、
        /// 各シーンの初期化時に、そのシーンの EventSystem を渡して呼ぶ。
        /// </summary>
        void SetEventSystem(EventSystem eventSystem);
    }
}
