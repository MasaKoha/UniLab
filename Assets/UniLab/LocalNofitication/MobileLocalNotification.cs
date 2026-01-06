using System;
using Cysharp.Threading.Tasks;
using UniLab.Common;
using UniLab.LocalNotification.Platform;
using UniLab.LocalNotification.Platform.Interface;

namespace UniLab.LocalNotification
{
    public sealed class MobileLocalNotification : SingletonPureClass<MobileLocalNotification>
    {
        private ILocalMobileNotification _localMobileNotification;

        public void Initialize(AndroidNotificationInformation information = null)
        {
            ILocalMobileNotification localMobileNotification = null;
#if UNITY_EDITOR
            localMobileNotification = new StandaloneNotification();
#elif UNITY_ANDROID
            localMobileNotification = new AndroidMobileNotification(information);
#elif UNITY_IOS
            localMobileNotification = new IOSMobileNotification();
#endif
            _localMobileNotification = localMobileNotification;
        }

        public UniTask RequestNotificationPermission()
        {
            return _localMobileNotification.RequestNotificationPermission();
        }

        public bool IsNotificationPermissionGranted()
        {
            return _localMobileNotification.IsNotificationPermissionGranted();
        }

        public NotificationPermissionStatus GetNotificationPermissionStatus()
        {
            return _localMobileNotification.GetNotificationPermissionStatus();
        }

        public void ScheduleNotification(int identifier, string title, string message, int delaySeconds)
        {
            _localMobileNotification.ScheduleNotification(identifier, title, message, delaySeconds);
        }

        public void ScheduleNotificationAtDateTime(int identifier, string title, string message, DateTime fireTime)
        {
            _localMobileNotification.ScheduleNotificationAtDateTime(identifier, title, message, fireTime);
        }

        public void CancelNotification(int identifier)
        {
            _localMobileNotification.CancelNotification(identifier);
        }

        public void CancelAllNotifications()
        {
            _localMobileNotification.CancelAllNotifications();
        }
    }
}