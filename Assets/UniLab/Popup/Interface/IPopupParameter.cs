using System;
using Cysharp.Threading.Tasks;

namespace UniLab.Popup
{
    public interface IPopupParameter
    {
        // BackKey を押したときに反応するかどうか
        public bool EnableBackKey { get; }

        // BackKey を押したときに実行したい関数を指定する。なにも指定しなければ基本的には閉じる実装
        public Func<UniTask> CustomBackAsync { get; }
        public bool EnableBackgroundClose { get; }
    }
}