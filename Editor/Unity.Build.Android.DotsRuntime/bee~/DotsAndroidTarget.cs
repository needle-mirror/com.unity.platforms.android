using Bee.NativeProgramSupport.Building;
using Bee.Toolchain.Android;
using DotsBuildTargets;
using Unity.BuildSystem.NativeProgramSupport;

abstract class DotsAndroidTarget : DotsBuildSystemTarget
{
    protected const bool k_UseStatic = true;
    public override bool CanUseBurst { get; } = true;
}

class DotsAndroidTargetArmv7 : DotsAndroidTarget
{
    public override string Identifier => "android_armv7";

    public override ToolChain ToolChain => AndroidApkToolchain.GetToolChain(k_UseStatic, new ARMv7Architecture(), AndroidApkToolchain.TargetType.Single);
}

class DotsAndroidTargetArm64 : DotsAndroidTarget
{
    public override string Identifier => "android_arm64";

    public override ToolChain ToolChain => AndroidApkToolchain.GetToolChain(k_UseStatic, new Arm64Architecture(), AndroidApkToolchain.TargetType.Single);
}

class DotsAndroidTargetFat : DotsAndroidTarget
{
    public override string Identifier => "android_fat";

    public override ToolChain ToolChain => AndroidApkToolchain.GetToolChain(k_UseStatic, new ARMv7Architecture(), AndroidApkToolchain.TargetType.Main);

    public override DotsBuildSystemTarget ComplementaryTarget => DotsAndroidTargetArm64Support.GetInstance();
}

internal class DotsAndroidTargetArm64Support : DotsAndroidTarget
{
    public override string Identifier => "android_complementary_arm64";

    public override ToolChain ToolChain => AndroidApkToolchain.GetToolChain(k_UseStatic, new Arm64Architecture(), AndroidApkToolchain.TargetType.Complementary);

    public static DotsAndroidTargetArm64Support GetInstance()
    {
        if (m_Instance == null)
        {
            m_Instance = new DotsAndroidTargetArm64Support();
        }
        return m_Instance;
    }
    private static DotsAndroidTargetArm64Support m_Instance = null;
}
