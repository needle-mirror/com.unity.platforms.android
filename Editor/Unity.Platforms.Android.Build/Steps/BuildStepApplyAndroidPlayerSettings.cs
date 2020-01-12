using Unity.Platforms.Build;
using Unity.Platforms.Build.Classic;
using UnityEditor;

namespace Unity.Platforms.Android.Build
{

    [BuildStep(Name = "Apply Android Player Settings", Description = "Applying Android Player Settings", Category = "Android Platform")]
    public sealed class BuildStepApplyAndroidPlayerSettings : BuildStep
    {
        public override BuildStepResult RunBuildStep(BuildContext context)
        {
            var target = context.GetValue<ClassicBuildProfile>().Target;
            if (target != BuildTarget.Android)
                return Failure($"Expected Android build target, but was {target}");

            var projectSettings = AssetDatabase.LoadAssetAtPath<PlayerSettings>("ProjectSettings/ProjectSettings.asset");
            /* TODO
            var androidSettings = settings.GetComponent<AndroidSettings>();
            AndroidBuildType androidBuildType;
            switch (profile.Configuration)
            {
                case BuildConfiguration.Debug: androidBuildType = AndroidBuildType.Debug; break;
                case BuildConfiguration.Develop: androidBuildType = AndroidBuildType.Development; break;
                case BuildConfiguration.Release: androidBuildType = AndroidBuildType.Release; break;
                default: throw new NotImplementedException("AndroidBuildType");
            }
            EditorUserBuildSettings.androidBuildType = androidBuildType;
            PlayerSettings.Android.targetArchitectures = androidSettings.TargetArchitectures;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, androidSettings.PackageName);
            */

            EditorUtility.ClearDirty(projectSettings);

            return Success();
        }
    }
}
