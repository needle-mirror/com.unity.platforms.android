using Unity.Properties;
using Unity.Serialization;

namespace Unity.Build.Android
{
    internal enum AspectRatioMode
    {
        LegacyWideScreen,
        SuperWideScreen,
        Custom
    }

    [FormerName("Unity.Platforms.Android.Build.AndroidAspectRatio, Unity.Platforms.Android.Build")]
    internal sealed class AndroidAspectRatio : IBuildComponent
    {
        [CreateProperty]
        public AspectRatioMode AspectRatioMode { set; get; } = AspectRatioMode.SuperWideScreen;

        [CreateProperty]
        public float CustomAspectRatio { set; get; } = 2.1f;
    }
}
