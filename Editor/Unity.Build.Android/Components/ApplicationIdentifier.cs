using Unity.Build.Classic;
using Unity.Properties;
using Unity.Serialization;
using UnityEditor;

namespace Unity.Build.Android
{
    [FormerName("Unity.Platforms.Android.Build.ApplicationIdentifier, Unity.Platforms.Android.Build")]
    internal sealed class ApplicationIdentifier : IBuildComponent, ICustomBuildComponentConstructor
    {
        string m_PackageName;

        [CreateProperty]
        public string PackageName
        {
            get => !string.IsNullOrEmpty(m_PackageName) ? m_PackageName : "com.unity.DefaultPackage";
            set => m_PackageName = value;
        }

        void ICustomBuildComponentConstructor.Construct(BuildConfiguration.ReadOnly config)
        {
            var group = config.GetBuildTargetGroup();
            if (group == BuildTargetGroup.Unknown)
                return;

            m_PackageName = PlayerSettings.GetApplicationIdentifier(group);
        }
    }
}
