using System;
using Cysharp.Threading.Tasks;

namespace UniLab.Notification.Platform.Interface
{
    public interface ILocalMobileNotification
    {
        public UniTask RequestNotificationPermission();
        public bool IsNotificationPermissionGranted();
        public NotificationPermissionStatus GetNotificationPermissionStatus();
        public void ScheduleNotification(int identifier, string title, string message, int delaySeconds);
        public void ScheduleNotificationAtDateTime(int identifier, string title, string message, DateTime fireTime);
        void CancelNotification(int identifier);
        void CancelAllNotifications();
    }
}
