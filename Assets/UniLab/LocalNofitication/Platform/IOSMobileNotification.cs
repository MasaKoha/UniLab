using System;
using Cysharp.Threading.Tasks;
using UniLab.LocalNotification.Platform.Interface;

#if UNITY_IOS
using Unity.Notifications.iOS;

namespace UniLab.LocalNotification.Platform
{
    public class IOSMobileNotification : ILocalMobileNotification
    {
        public async UniTask RequestNotificationPermission()
        {
            const AuthorizationOption authorizationOption =
                AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound;
            using var req = new AuthorizationRequest(authorizationOption, true);
            while (!req.IsFinished)
            {
                await UniTask.Delay(200);
            }
        }

        public bool IsNotificationPermissionGranted()
        {
            var settings = iOSNotificationCenter.GetNotificationSettings();
            return settings.AuthorizationStatus == AuthorizationStatus.Authorized;
        }

        public NotificationPermissionStatus GetNotificationPermissionStatus()
        {
            var settings = iOSNotificationCenter.GetNotificationSettings();
            return settings.AuthorizationStatus switch
            {
                AuthorizationStatus.NotDetermined => NotificationPermissionStatus.NotDetermined,
                AuthorizationStatus.Denied => NotificationPermissionStatus.Denied,
                AuthorizationStatus.Authorized => NotificationPermissionStatus.Authorized,
                AuthorizationStatus.Provisional => NotificationPermissionStatus.Provisional,
                AuthorizationStatus.Ephemeral => NotificationPermissionStatus.Ephemeral,
                _ => NotificationPermissionStatus.NotDetermined
            };
        }

        public void ScheduleNotification(int identifier, string title, string message, int delaySeconds)
        {
            // 既存のdelaySeconds指定
            var timeTrigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = new TimeSpan(0, 0, delaySeconds),
                Repeats = false
            };

            var notification = new iOSNotification
            {
                Identifier = identifier.ToString(),
                Title = title,
                Body = message,
                Trigger = timeTrigger
            };
            iOSNotificationCenter.ScheduleNotification(notification);
        }

        public void ScheduleNotificationAtDateTime(int identifier, string title, string message, DateTime fireTime)
        {
            var now = DateTime.Now;
            var delay = fireTime > now ? fireTime - now : TimeSpan.Zero;
            var timeTrigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = delay,
                Repeats = false
            };

            var notification = new iOSNotification
            {
                Identifier = identifier.ToString(),
                Title = title,
                Body = message,
                Trigger = timeTrigger
            };

            iOSNotificationCenter.ScheduleNotification(notification);
        }

        public void CancelNotification(int identifier)
        {
            iOSNotificationCenter.RemoveScheduledNotification(identifier.ToString());
        }

        public void CancelAllNotifications()
        {
            iOSNotificationCenter.RemoveAllScheduledNotifications();
        }
    }
}

#endif