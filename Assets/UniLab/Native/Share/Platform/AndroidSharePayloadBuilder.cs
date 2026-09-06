using System.Collections.Generic;

namespace UniLab.Native.Share.Platform
{
    internal static class AndroidSharePayloadBuilder
    {
        public static AndroidSharePayload Build(in ShareContent content)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(content.Text))
            {
                lines.Add(content.Text);
            }

            if (!string.IsNullOrEmpty(content.Url))
            {
                lines.Add(content.Url);
            }

            var body = string.Join("\n", lines);
            var hasImage = !string.IsNullOrEmpty(content.ImagePath);
            var mimeType = hasImage ? "image/png" : "text/plain";
            return new AndroidSharePayload(body, mimeType, hasImage);
        }
    }
}
