#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using Unity.Build;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;

namespace Unity.Build.Android.Classic
{
    class GraphCopyDefaultResources : BuildStepBase
    {
        public override BuildResult Run(BuildContext context)
        {
            var classicContext = context.GetValue<IncrementalClassicSharedData>();
            var androidContext = context.GetValue<AndroidBuildContext>();
            var target = classicContext.DataDeployDirectory.Combine("unity default resources");
            CopyTool.Instance().Setup(
                target,
                classicContext.PlayerPackageDirectory.Combine("Data", "unity default resources"));
            androidContext.AddGradleProjectFile(target);
            // TODO: unity_builtin_extra (seems to be generated by BuildPlayerData)
            return context.Success();
        }
    }
}
#endif