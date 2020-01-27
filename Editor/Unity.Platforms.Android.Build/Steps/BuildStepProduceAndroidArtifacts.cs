using System.IO;
using Unity.Build;

namespace Unity.Platforms.Android.Build
{
    [BuildStep(Name = "Produce Android Artifacts", Description = "Producing Android Artifacts", Category = "Android Platform")]
    public sealed class BuildStepProduceAndroidArtifacts : BuildStep
    {
        public override BuildStepResult RunBuildStep(BuildContext context)
        {
            var report = context.GetValue<UnityEditor.Build.Reporting.BuildReport>();
            var artifact = context.GetOrCreateValue<BuildArtifactAndroid>();
            artifact.OutputTargetFile = new FileInfo(report.summary.outputPath);
            return Success();
        }
    }
}
