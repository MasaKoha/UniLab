namespace UniLab.Notification.Platform
{
    public sealed class AndroidNotificationInformation
    {
        public string Id;
        public string Name;
        public AndroidImportance Importance = AndroidImportance.Default;
        public string Description;
        public bool EnableVibration;
        public bool EnableLights;
    }

    // AndroidImportance = Unity.Notifications.Android.Importance
    public enum AndroidImportance
    {
        None = 0,
        Low = 2,
        Default = 3,
        High = 4,
    }
}
