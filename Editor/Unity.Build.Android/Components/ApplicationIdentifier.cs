using Unity.Build.Classic;
using Unity.Properties;
using Unity.Serialization;
using UnityEditor;

namespace Unity.Build.Android
{
    [FormerName("Unity.Platforms.Android.Build.ApplicationIdentifier, Unity.Platforms.Android.Build")]
    internal sealed partial class ApplicationIdentifier : IBuildComponent, ICustomBuildComponentConstructor
    {
        void ICustomBuildComponentConstructor.Construct(BuildConfiguration.ReadOnly config)
        {
            var group = config.GetBuildTargetGroup();
            if (group == BuildTargetGroup.Unknown)
                return;

            m_PackageName = PlayerSettings.GetApplicationIdentifier(group);
        }
    }
}
