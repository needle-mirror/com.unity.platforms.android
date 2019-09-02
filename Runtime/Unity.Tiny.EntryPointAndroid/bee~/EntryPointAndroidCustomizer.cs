using JetBrains.Annotations;

[UsedImplicitly]
class CustomizerForEntryPointAndroid : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.EntryPointAndroid";

    public override string[] ImplementationFor => new [] { "Unity.Tiny.EntryPoint" };
}
