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

        /// <summary>積んだグリッドを降ろす。スタックのどこにあっても取り除く（積まれていなければ何もしない）。</summary>
        void PopGrid(FocusGrid grid);

        /// <summary>指定 Selectable を選択状態にし、列記憶を同期する。</summary>
        void SetSelected(Selectable selectable);

        /// <summary>
        /// 現在選択中の Selectable。未選択・EventSystem 未設定なら null。
        /// ポップアップのように一時的にフォーカスを奪うものが、閉じたあと元の位置へ戻すために控える用途で使う。
        /// </summary>
        Selectable CurrentSelected { get; }

        /// <summary>
        /// アクティブグリッドの startRowIndex 行目以降で最初の有効セルへフォーカスする。
        /// タブバーのような共通行を先頭に持つ画面では、その行数を渡して中身の先頭へ移す。
        /// </summary>
        void FocusFirst(int startRowIndex);

        /// <summary>
        /// 方向入力ストリームと操作対象の EventSystem を受け取り、方向解決を開始する。
        /// このシーンの Presenter が初期化時に一度だけ呼ぶ。
        /// </summary>
        void Initialize(Observable<FocusDirection> moveStream, EventSystem eventSystem, bool focusNonInteractable, FocusWrapMode defaultWrapMode);

        /// <summary>
        /// 押せない項目（interactable=false）にもフォーカスを乗せるか。Initialize で決まる。
        /// 可視化ツールが解決結果を再現するために公開する。
        /// </summary>
        bool FocusNonInteractable { get; }

        /// <summary>
        /// グリッドがラップモードを指定しなかった場合に使う既定値。Initialize で決まる。
        /// 画面ごとに個別指定したい場合は FocusGridBuilder.SetWrapMode で上書きする。
        /// </summary>
        FocusWrapMode DefaultWrapMode { get; }
    }
}
