using System;
using UniLab.Notification.Platform.Interface;
using Cysharp.Threading.Tasks;
#if UNITY_ANDROID
using UnityEngine.Android;
using Unity.Notifications.Android;
using UnityEngine;

namespace UniLab.Notification.Platform
{
    public class AndroidMobileNotification : ILocalMobileNotification
    {
        private readonly AndroidNotificationChannel _channel;
        private const string NotificationPermissionKey = "android.permission.POST_NOTIFICATIONS";

        public async UniTask RequestNotificationPermission()
        {
            Permission.RequestUserPermission(NotificationPermissionKey);
            while (!IsNotificationPermissionGranted())
            {
                await UniTask.Delay(200);
            }
        }

        public bool IsNotificationPermissionGranted()
        {
            return Permission.HasUserAuthorizedPermission(NotificationPermissionKey);
        }

        public NotificationPermissionStatus GetNotificationPermissionStatus()
        {
            return Permission.HasUserAuthorizedPermission(NotificationPermissionKey)
                ? NotificationPermissionStatus.Authorized
                : NotificationPermissionStatus.Denied;
        }

        public AndroidMobileNotification(AndroidNotificationInformation information)
        {
            AndroidNotificationCenter.Initialize();
            _channel = new AndroidNotificationChannel
            {
                Id = information.Id,
                Name = information.Name,
                Importance = (Importance)information.Importance,
                Description = information.Description,
                EnableVibration = information.EnableVibration,
                EnableLights = information.EnableLights,
            };
            AndroidNotificationCenter.RegisterNotificationChannel(_channel);
        }

        public void ScheduleNotification(int identifier, string title, string message, int delaySeconds)
        {
            var notification = new AndroidNotification
            {
                Title = title,
                Text = message,
                FireTime = DateTime.Now.AddSeconds(delaySeconds)
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, _channel.Id, identifier);
            Debug.Log($"Scheduled notification with ID {identifier}.");
        }

        public void ScheduleNotificationAtDateTime(int identifier, string title, string message, DateTime fireTime)
        {
            var notification = new AndroidNotification
            {
                Title = title,
                Text = message,
                FireTime = fireTime
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, _channel.Id, identifier);
        }

        public void CancelNotification(int identifier)
        {
            AndroidNotificationCenter.CancelNotification(identifier);
        }

        public void CancelAllNotifications()
        {
            AndroidNotificationCenter.CancelAllNotifications();
        }
    }
}
#endif
