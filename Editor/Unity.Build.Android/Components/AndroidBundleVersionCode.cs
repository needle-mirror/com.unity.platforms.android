using System;
using Unity.Build.Classic.Private;
using UnityEditor;

namespace Unity.Build.Android
{
    internal sealed partial class AndroidBundleVersionCode : IBuildComponent, ICustomBuildComponentConstructor
    {
        void ICustomBuildComponentConstructor.Construct(BuildConfiguration.ReadOnly config)
        {
            if (!(config.GetBuildPipeline() is ClassicPipelineBase))
                return;

            m_VersionCode = PlayerSettings.Android.bundleVersionCode;
        }
    }
}
