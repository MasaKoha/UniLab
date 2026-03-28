using System;
using Cysharp.Threading.Tasks;
using UniLab.Notification.Platform.Interface;

namespace UniLab.Notification.Platform
{
    public class StandaloneNotification : ILocalMobileNotification
    {
        public UniTask RequestNotificationPermission()
        {
            Debug.Log("Requesting notification permission on Standalone platform.");
            return UniTask.CompletedTask;
        }

        public bool IsNotificationPermissionGranted()
        {
            Debug.Log("Checking notification permission on Standalone platform.");
            return true;
        }

        public NotificationPermissionStatus GetNotificationPermissionStatus()
        {
            Debug.Log("Getting notification permission status on Standalone platform.");
            return NotificationPermissionStatus.Authorized;
        }

        public void ScheduleNotification(int identifier, string title, string message, int delaySeconds)
        {
            Debug.Log($"Scheduling notification with ID {identifier}: Title: {title}, Message: {message}, Delay: {delaySeconds} seconds");
        }

        public void ScheduleNotificationAtDateTime(int identifier, string title, string message, DateTime fireTime)
        {
            var now = DateTime.Now;
            var delay = fireTime > now ? fireTime - now : TimeSpan.Zero;
            Debug.Log($"Scheduling notification with ID {identifier} at {fireTime}: Title: {title}, Message: {message}, Delay: {delay.TotalSeconds} seconds");
        }

        public void CancelNotification(int identifier)
        {
            Debug.Log($"Cancelling notification with ID {identifier}.");
        }

        public void CancelAllNotifications()
        {
            Debug.Log("Cancelling all notifications on Standalone platform.");
        }
    }
}
