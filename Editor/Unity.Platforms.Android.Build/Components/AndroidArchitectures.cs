using Unity.Build;
using Unity.Properties;
using UnityEditor;

namespace Unity.Platforms.Android.Build
{
    sealed class AndroidArchitectures : IBuildComponent
    {
        [CreateProperty]
        public AndroidArchitecture Architectures { get; set; } = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
    }
}
