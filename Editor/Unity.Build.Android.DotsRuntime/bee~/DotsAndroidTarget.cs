using Bee.Toolchain.Android;
using DotsBuildTargets;
using Bee.NativeProgramSupport;

abstract class DotsAndroidTarget : DotsBuildSystemTarget
{
    protected const bool k_UseStatic = true;
    public override bool CanUseBurst { get; } = true;
}

class DotsAndroidTargetMain : DotsAndroidTarget
{
    public override string Identifier => "android_armv7";

    public override ToolChain ToolChain => AndroidApkToolchain.GetToolChain(k_UseStatic, true);

    public override DotsBuildSystemTarget ComplementaryTarget
    {
        get
        {
            var target = DotsAndroidTargetComplementary.GetInstance();
            return target.ToolChain != null ? target : null;
        }
    }
}

internal class DotsAndroidTargetComplementary : DotsAndroidTarget
{
    public override string Identifier => "android_complementary";

    public override ToolChain ToolChain => AndroidApkToolchain.GetToolChain(k_UseStatic, false);

    public static DotsAndroidTargetComplementary GetInstance()
    {
        if (m_Instance == null)
        {
            m_Instance = new DotsAndroidTargetComplementary();
        }
        return m_Instance;
    }
    private static DotsAndroidTargetComplementary m_Instance = null;
}
