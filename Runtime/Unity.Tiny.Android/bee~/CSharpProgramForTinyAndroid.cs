using JetBrains.Annotations;
using Unity.BuildSystem.NativeProgramSupport;

[UsedImplicitly]
class CustomizerForTinyAndroid : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.Android";

    // not exactly right, but good enough for now
    public override string[] ImplementationFor => new[] {"Unity.Tiny.Core"};

    public override void CustomizeSelf(AsmDefBasedDotsRuntimeCSharpProgram program)
    {
        program.NativeProgram.Libraries.Add(new SystemLibrary("log"));
        program.NativeProgram.Libraries.Add(new SystemLibrary("android"));
        program.NativeProgram.Libraries.Add(new SystemLibrary("GLESv3"));
    }
}
