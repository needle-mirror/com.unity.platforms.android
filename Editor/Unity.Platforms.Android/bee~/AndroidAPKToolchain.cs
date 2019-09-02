using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Bee.NativeProgramSupport.Building;
using Bee.Core;
using Bee.DotNet;
using Bee.Stevedore;
using Bee.Toolchain.GNU;
using Bee.Toolchain.Extension;
using Bee.BuildTools;
using NiceIO;
using Unity.BuildSystem.NativeProgramSupport;
using Unity.BuildTools;

namespace Bee.Toolchain.Android
{
    internal class AndroidApkToolchain : AndroidNdkToolchain
    {
        public static ToolChain ToolChain_AndroidArmv7 { get; } = new AndroidApkToolchain(AndroidNdk.LocatorArmv7.FindSdkInDownloadsOrSystem(AndroidNdkR16B.k_Version).WithForcedApiLevel(21)); // to support GLES3
        public static ToolChain ToolChain_AndroidArm64 { get; } = new AndroidApkToolchain(AndroidNdk.LocatorArm64.FindSdkInDownloadsOrSystem(AndroidNdkR16B.k_Version));

        public override NativeProgramFormat DynamicLibraryFormat { get; }
        public override NativeProgramFormat ExecutableFormat { get; }

        public NPath SdkPath { get; private set; }
        public NPath JavaPath { get; private set; }
        public NPath GradlePath { get; private set; }

        public AndroidApkToolchain(AndroidNdk ndk) : base(ndk)
        {
            DynamicLibraryFormat = new AndroidApkDynamicLibraryFormat(this);
            ExecutableFormat = new AndroidApkMainModuleFormat(this);

            var sdk = new StevedoreArtifact(HostPlatform.Pick(
               linux: "android-sdk-linux-x86_64",
               mac: "android-sdk-darwin-x86_64",
               windows: "android-sdk-windows-x86_64"
            ));

            var jdk = new StevedoreArtifact(HostPlatform.Pick(
               linux: "open-jdk-linux-x64",
               mac: "open-jdk-mac-x64",
               windows: "open-jdk-win-x64"
            ));

            Backend.Current.Register(sdk);
            SdkPath = sdk.Path;

            Backend.Current.Register(jdk);
            JavaPath = jdk.Path;

            var gradle = new StevedoreArtifact("gradle");
            Backend.Current.Register(gradle);
            GradlePath = gradle.Path;
        }
    }

    internal class AndroidLinker : LdDynamicLinker
    {
        // workaround arm64 issue (https://issuetracker.google.com/issues/70838247)
        protected override string LdLinkerName => Toolchain.Architecture is Arm64Architecture ? "bfd" : "gold";

        public AndroidLinker(AndroidNdkToolchain toolchain) : base(toolchain, true) {}

        protected override IEnumerable<string> CommandLineFlagsFor(NPath target, CodeGen codegen, IEnumerable<NPath> inputFiles)
        {
            foreach (var flag in base.CommandLineFlagsFor(target, codegen, inputFiles))
                yield return flag;

            var ndk = (AndroidNdk)Toolchain.Sdk;
            foreach (var flag in ndk.LinkerCommandLineFlagsFor(target, codegen, inputFiles))
                yield return flag;

            if (LdLinkerName == "gold" && codegen != CodeGen.Debug)
            {
                // enable identical code folding (saves ~500k (3%) of Android mono release library as of May 2018)
                yield return "-Wl,--icf=safe";

                // redo folding multiple times (default is 2, saves 13k of Android mono release library as of May 2018)
                yield return "-Wl,--icf-iterations=5";
            }
            if (codegen != CodeGen.Debug)
            {
                // why it hasn't been added originally?
                yield return "-Wl,--strip-all";
            }
        }

        protected override BuiltNativeProgram BuiltNativeProgramFor(NPath destination, IEnumerable<PrecompiledLibrary> allLibraries)
        {
            var dynamicLibraries = allLibraries.Where(l => l.Dynamic).ToArray();
            return (BuiltNativeProgram) new DynamicLibrary(destination, dynamicLibraries);
        }
    }

    internal class AndroidMainModuleLinker : AndroidLinker
    {
        public AndroidMainModuleLinker(AndroidNdkToolchain toolchain) : base(toolchain) { }

        private NPath ChangeMainModuleName(NPath target)
        {
            // need to rename to make it start with "lib", otherwise Android have problems with loading native library
            return target.Parent.Combine("lib" + target.FileName).ChangeExtension("so");
        }

        protected override IEnumerable<string> CommandLineFlagsFor(NPath target, CodeGen codegen, IEnumerable<NPath> inputFiles)
        {
            foreach (var flag in base.CommandLineFlagsFor(ChangeMainModuleName(target), codegen, inputFiles))
                yield return flag;
        }

        protected override BuiltNativeProgram BuiltNativeProgramFor(NPath destination, IEnumerable<PrecompiledLibrary> allLibraries)
        {
            var dynamicLibraries = allLibraries.Where(l => l.Dynamic).ToArray();
            return (BuiltNativeProgram)new AndroidMainDynamicLibrary(ChangeMainModuleName(destination), Toolchain as AndroidApkToolchain, dynamicLibraries);
        }
    }

    internal sealed class AndroidApkDynamicLibraryFormat : NativeProgramFormat
    {
        public override string Extension { get; } = "so";

        internal AndroidApkDynamicLibraryFormat(AndroidNdkToolchain toolchain) : base(
            new AndroidLinker(toolchain).AsDynamicLibrary())
        {
        }
    }

    internal sealed class AndroidApkMainModuleFormat : NativeProgramFormat
    {
        public override string Extension { get; } = "apk";

        internal AndroidApkMainModuleFormat(AndroidNdkToolchain toolchain) : base(
            new AndroidMainModuleLinker(toolchain).AsDynamicLibrary())
        {
        }
    }

    internal class AndroidMainDynamicLibrary : DynamicLibrary, IPackagedAppExtension
    {
        private AndroidApkToolchain m_apkToolchain;
        private String m_gameName;
        private CodeGen m_codeGen;
        private IEnumerable<IDeployable> m_supportFiles;

        public AndroidMainDynamicLibrary(NPath path, AndroidApkToolchain toolchain, params PrecompiledLibrary[] dynamicLibraryDependencies) : base(path, dynamicLibraryDependencies)
        {
            m_apkToolchain = toolchain;
        }

        public void SetAppPackagingParameters(String gameName, CodeGen codeGen, IEnumerable<IDeployable> supportFiles)
        {
            m_gameName = gameName;
            m_codeGen = codeGen;
            m_supportFiles = supportFiles;
        }

        private NPath PackageApp(NPath buildPath, NPath mainLibPath)
        {
            var deployedPath = buildPath.Combine(m_gameName.Replace(".","-") + ".apk");
            if (m_apkToolchain == null)
            {
                Console.WriteLine($"Error: not Android APK toolchain");
                return deployedPath;
            }

            var gradleProjectPath = buildPath.Combine("gradle/");
            var pathToRoot = new NPath(string.Concat(Enumerable.Repeat("../", gradleProjectPath.Depth)));
            var apkSrcPath = BuildProgramConfigFile.AsmDefDescriptionFor("Unity.Platforms.Android").Path.Parent.Combine("AndroidProjectTemplate~/");

            var javaLaunchPath = pathToRoot.Combine(m_apkToolchain.JavaPath).Combine("bin").Combine("java");
            var gradleLaunchPath = pathToRoot.Combine(m_apkToolchain.GradlePath).Combine("lib").Combine("gradle-launcher-4.6.jar");
            var releaseApk = m_codeGen == CodeGen.Release;
            var gradleCommand = releaseApk ? "assembleRelease" : "assembleDebug";
            var deleteCommand = HostPlatform.IsWindows ? $"del /f /q {deployedPath.InQuotes(SlashMode.Native)} 2> nul" : $"rm -f {deployedPath.InQuotes(SlashMode.Native)}";
            var gradleExecutableString = $"{deleteCommand} && cd {gradleProjectPath.InQuotes()} && {javaLaunchPath.InQuotes()} -classpath {gradleLaunchPath.InQuotes()} org.gradle.launcher.GradleMain {gradleCommand} && cd {pathToRoot.InQuotes()}";

            var apkPath = gradleProjectPath.Combine("build/outputs/apk").Combine(releaseApk ? "release/gradle-release.apk" : "debug/gradle-debug.apk");

            Backend.Current.AddAction(
                actionName: "Build Gradle project",
                targetFiles: new[] { apkPath },
                inputs: new[] { mainLibPath, m_apkToolchain.SdkPath, m_apkToolchain.JavaPath, m_apkToolchain.GradlePath },
                executableStringFor: gradleExecutableString,
                commandLineArguments: Array.Empty<string>(),
                allowUnexpectedOutput: false,
                allowedOutputSubstrings: new[] { ":*", "BUILD SUCCESSFUL in *" }
            );

            var templateStrings = new Dictionary<string, string>
            {
                { "**TINYNAME**", m_gameName.ToLower() },
                { "**GAMENAME**", m_gameName },
            };

            // copy and patch project files
            foreach (var r in apkSrcPath.Files(true))
            {
                var destPath = gradleProjectPath.Combine(r.RelativeTo(apkSrcPath));
                if (r.Extension == "template")
                {
                    destPath = destPath.ChangeExtension("");
                    var code = r.ReadAllText();
                    foreach (var t in templateStrings)
                    {
                        if (code.IndexOf(t.Key) != -1)
                        {
                            code = code.Replace(t.Key, t.Value);
                        }
                    }
                    Backend.Current.AddWriteTextAction(destPath, code);
                }
                else
                {
                    destPath = CopyTool.Instance().Setup(destPath, r);
                }
                Backend.Current.AddDependency(apkPath, destPath);
            }

            var localProperties = new StringBuilder();
            localProperties.AppendLine($"sdk.dir={m_apkToolchain.SdkPath.MakeAbsolute()}");
            localProperties.AppendLine($"ndk.dir={m_apkToolchain.Sdk.Path.MakeAbsolute()}");
            var localPropertiesPath = gradleProjectPath.Combine("local.properties");
            Backend.Current.AddWriteTextAction(localPropertiesPath, localProperties.ToString());
            Backend.Current.AddDependency(apkPath, localPropertiesPath);

            // copy additional resources and Data files
            // TODO: better to use move from main lib directory
            foreach (var r in m_supportFiles)
            {
                var targetAssetPath = gradleProjectPath.Combine("src/main/assets");
                if (r is DeployableFile && (r as DeployableFile).RelativeDeployPath != null)
                {
                    targetAssetPath = targetAssetPath.Combine((r as DeployableFile).RelativeDeployPath);
                }
                else
                {
                    targetAssetPath = targetAssetPath.Combine(r.Path.FileName);
                }
                Backend.Current.AddDependency(apkPath, CopyTool.Instance().Setup(targetAssetPath, r.Path));
            }

            return CopyTool.Instance().Setup(deployedPath, apkPath);
        }

        public override BuiltNativeProgram DeployTo(NPath targetDirectory, Dictionary<IDeployable, IDeployable> alreadyDeployed = null)
        {
            // TODO: path should depend on toolchain (armv7/arm64)
            var libDirectory = targetDirectory.Combine("gradle/src/main/jniLibs/armeabi-v7a");
            var result = base.DeployTo(libDirectory, alreadyDeployed);

            return new Executable(PackageApp(targetDirectory, result.Path));
        }
    }

}

