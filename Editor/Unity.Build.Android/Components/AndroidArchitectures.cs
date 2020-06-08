using Unity.Build.Classic.Private;
using Unity.Properties;
using Unity.Serialization;
using UnityEditor;

namespace Unity.Build.Android
{
    [FormerName("Unity.Platforms.Android.Build.AndroidArchitectures, Unity.Platforms.Android.Build")]
    internal sealed class AndroidArchitectures : IBuildComponent, ICustomBuildComponentConstructor
    {
        [CreateProperty]
        public AndroidArchitecture Architectures { get; set; } = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

        void ICustomBuildComponentConstructor.Construct(BuildConfiguration.ReadOnly config)
        {
            if (!(config.GetBuildPipeline() is ClassicPipelineBase))
                return;

            Architectures = PlayerSettings.Android.targetArchitectures;
        }
    }
}
