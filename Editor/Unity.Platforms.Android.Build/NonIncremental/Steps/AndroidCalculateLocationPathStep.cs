using System.IO;
using Unity.Build;
using Unity.Build.Classic;
using Unity.Build.Common;

namespace Unity.Platforms.Android.Build
{
    class AndroidCalculateLocationPathStep : CalculateLocationPathStep
    {
        protected override string CalculatePath(BuildContext context)
        {
            var gradleOutput = context.GetValue<AndroidNonIncrementalData>().GradleOuputDirectory;
            // Set AndroidProjectArtifact, since there's no better place
            var gradleArtifact = context.GetOrCreateValue<AndroidProjectArtifact>();
            gradleArtifact.ProjectDirectory = new DirectoryInfo(gradleOutput);
            return gradleOutput;
        }
    }
}
