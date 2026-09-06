namespace UniLab.Native.Share.Platform
{
    internal readonly struct AndroidSharePayload
    {
        public string Body { get; }
        public string MimeType { get; }
        public bool HasImage { get; }

        public AndroidSharePayload(string body, string mimeType, bool hasImage)
        {
            Body = body;
            MimeType = mimeType;
            HasImage = hasImage;
        }
    }
}
