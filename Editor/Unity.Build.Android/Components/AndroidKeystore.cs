using Unity.Build.Classic;
using Unity.Properties;
using Unity.Serialization;
using UnityEditor;

namespace Unity.Build.Android
{
    internal sealed partial class AndroidKeystore : IBuildComponent, ICustomBuildComponentConstructor
    {
        public static AndroidKeystore NewInstanceWithAlias(AndroidKeystore old, string aliasName)
        {
            return new AndroidKeystore()
            {
                KeystoreFullPath = old.KeystoreFullPath,
                KeystorePass = old.KeystorePass,
                KeyaliasName = aliasName,
                KeyaliasPass = old.KeyaliasPass
            };
        }

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
