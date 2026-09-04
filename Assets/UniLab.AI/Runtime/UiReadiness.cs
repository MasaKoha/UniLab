#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniLab.AI
{
    /// <summary>ランナーとゲートウェイの押下準備判定を一致させます。</summary>
    public static class UiReadiness
    {
        /// <summary>対象の存在・遮蔽・操作可否を確認し、準備できない理由を返します。</summary>
        public static bool IsSubmittable(string targetSpecification, out string failureMessage)
        {
            var target = UiInputLocator.FindTarget(targetSpecification);
            if (target == null)
            {
                failureMessage = $"操作対象が現れませんでした。 target={targetSpecification}";
                return false;
            }
            var blockingObject = UiInputLocator.FindBlockingObject(target);
            if (blockingObject != null)
            {
                failureMessage = $"対象が遮られています。 target={targetSpecification} blockedBy={blockingObject.name}";
                return false;
            }
            if (!UiInputLocator.IsInteractable(target))
            {
                failureMessage = $"対象が操作可能ではありません。 target={targetSpecification}";
                return false;
            }
            failureMessage = string.Empty;
            return true;
        }
    }
}
#endif
