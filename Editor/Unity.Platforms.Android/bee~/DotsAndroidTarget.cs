using Bee.NativeProgramSupport.Building;
using Bee.Toolchain.Android;
using DotsBuildTargets;
using Unity.BuildSystem.NativeProgramSupport;

// TODO: create Arm64 target
class DotsAndroidTargetArmv7 : DotsBuildSystemTarget
{
    protected override NativeProgramFormat GetExecutableFormatForConfig(DotsConfiguration config, bool enableManagedDebugger)
    {
        return new AndroidApkMainModuleFormat(ToolChain as AndroidApkToolchain);
    }

    protected override string Identifier => "android_armv7";

    protected override ToolChain ToolChain => AndroidApkToolchain.ToolChain_AndroidArmv7;
}
