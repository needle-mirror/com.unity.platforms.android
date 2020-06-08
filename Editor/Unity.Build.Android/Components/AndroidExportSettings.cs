using Unity.Properties;
using Unity.Serialization;

namespace Unity.Build.Android
{
    internal enum AndroidBuildSystem
    {
        Gradle,
        VisualStudio
    }

    internal enum AndroidTargetType
    {
        AndroidPackage,
        AndroidAppBundle
    }

    [FormerName("Unity.Platforms.Android.Build.AndroidExportSettings, Unity.Platforms.Android.Build")]
    internal sealed class AndroidExportSettings : IBuildComponent
    {
        [CreateProperty] public AndroidTargetType TargetType { set; get; } = AndroidTargetType.AndroidPackage;
        [CreateProperty] public bool ExportProject { set; get; } = false;
        [CreateProperty] public AndroidBuildSystem BuildSystem { set; get; } = AndroidBuildSystem.Gradle;
    }
}
