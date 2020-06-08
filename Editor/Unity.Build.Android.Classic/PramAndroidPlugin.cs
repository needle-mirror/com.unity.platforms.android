#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using NiceIO;
using System.IO;
using System.Collections.Generic;
using Unity.Build.Classic.Private;

namespace Unity.Build.Android.Classic
{
#if UNITY_ANDROID
    sealed class PramAndroidPlugin : PramPlatformPlugin
    {
        public override string[] Providers { get; } = { "android" };
        public override NPath PlatformAssemblyLoadPath
        {
            get { return Path.GetFullPath("Packages/com.unity.platforms.android/Editor/Unity.Build.Android.Classic/pram~"); }
        }

        public override IReadOnlyDictionary<string, string> Environment { get; } =
            new Dictionary<string, string>
            {
                ["JAVA_HOME"] = UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath,
                ["ANDROID_HOME"] = UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath,
            };
    }
#endif
}
#endif
