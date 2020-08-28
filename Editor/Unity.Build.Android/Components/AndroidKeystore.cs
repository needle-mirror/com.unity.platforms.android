using Unity.Build.Classic;
using Unity.Properties;
using Unity.Serialization;
using UnityEditor;

namespace Unity.Build.Android
{
    internal sealed partial class AndroidKeystore : IBuildComponent, ICustomBuildComponentConstructor
    {
        void ICustomBuildComponentConstructor.Construct(BuildConfiguration.ReadOnly config)
        {
            var group = config.GetBuildTargetGroup();
            if (group == BuildTargetGroup.Unknown)
                return;

            KeystoreFullPath = PlayerSettings.Android.keystoreName;

            KeystorePass = PlayerSettings.Android.keystorePass;

            KeyaliasName = PlayerSettings.Android.keyaliasName;

            KeyaliasPass = PlayerSettings.Android.keyaliasPass;
        }
    }
}
