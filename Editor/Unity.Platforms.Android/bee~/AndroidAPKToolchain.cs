using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Bee.NativeProgramSupport.Building;
using Bee.Core;
using Bee.DotNet;
using Bee.Stevedore;
using Bee.Toolchain.GNU;
using Bee.Toolchain.LLVM;
using Bee.Toolchain.Extension;
using Bee.BuildTools;
using Bee.NativeProgramSupport;
using Newtonsoft.Json.Linq;
using NiceIO;
using Unity.BuildSystem.NativeProgramSupport;

namespace Bee.Toolchain.Android
{
    internal class AndroidApkToolchain : AndroidNdkToolchain
    {
        private static AndroidApkToolchain ToolChain_AndroidArmv7 { get; set; } = null;

        public override CLikeCompiler CppCompiler { get; }
        public override NativeProgramFormat DynamicLibraryFormat { get; }
        public override NativeProgramFormat ExecutableFormat { get; }

        public List<NPath> RequiredArtifacts = new List<NPath>();
        public NPath SdkPath { get; private set; }
        public NPath JavaPath { get; private set; }
        public NPath GradlePath { get; private set; }

        private struct AndroidConfig
        {
            public string JavaPath;
            public string SdkPath;
            public string NdkPath;
            public string GradlePath;
        }

        public static AndroidApkToolchain GetToolChain(bool useStatic)
        {
            if (ToolChain_AndroidArmv7 == null)
            {
                var androidConfig = ReadConfigFromFile();
                var androidNdk = string.IsNullOrEmpty(androidConfig.NdkPath) ?
                    AndroidNdk.LocatorArmv7.UserDefaultOrDummy:
                    AndroidNdk.LocatorArmv7.UseSpecific(androidConfig.NdkPath);
                ToolChain_AndroidArmv7 = new AndroidApkToolchain(androidNdk, androidConfig.SdkPath, androidConfig.JavaPath, androidConfig.GradlePath, useStatic);
            }
            return ToolChain_AndroidArmv7;
        }

        private static AndroidConfig ReadConfigFromFile()
        {
            var file = NPath.CurrentDirectory.Combine("androidsettings.json");
            if (!file.FileExists())
                return new AndroidConfig();

            var json = file.ReadAllText();
            var jobject = JObject.Parse(json);
            return new AndroidConfig()
            {
                JavaPath = jobject["JavaPath"].Value<string>(),
                SdkPath = jobject["SdkPath"].Value<string>(),
                NdkPath = jobject["NdkPath"].Value<string>(),
                GradlePath = jobject["GradlePath"].Value<string>()
            };
        }

        public AndroidApkToolchain(AndroidNdk ndk, string sdkPath, string javaPath, string gradlePath, bool useStatic) : base(ndk)
        {
            DynamicLibraryFormat = useStatic ? new AndroidApkStaticLibraryFormat(this) as NativeProgramFormat :
                                               new AndroidApkDynamicLibraryFormat(this) as NativeProgramFormat;
            ExecutableFormat = new AndroidApkMainModuleFormat(this);
            CppCompiler = new AndroidNdkCompilerNoThumb(ActionName, Architecture, Platform, Sdk, ndk.ApiLevel, useStatic);
            SdkPath = sdkPath;
            JavaPath = javaPath;
            GradlePath = gradlePath;
        }

        public NPath GetGradleLaunchJarPath()
        {
            var launcherFiles = GradlePath.Combine("lib").Files("gradle-launcher-*.jar");
            if (launcherFiles.Length == 1)
                return launcherFiles[0];
            return null;
        }
    }

    internal class AndroidNdkCompilerNoThumb : AndroidNdkCompiler
    {
        public AndroidNdkCompilerNoThumb(string actionNameSuffix, Architecture targetArchitecture, Platform targetPlatform, Sdk sdk, int apiLevel, bool useStatic)
            : base(actionNameSuffix, targetArchitecture, targetPlatform, sdk, apiLevel)
        {
            DefaultSettings = new AndroidNdkCompilerSettingsNoThumb(this, apiLevel, useStatic)
                .WithExplicitlyRequireCPlusPlusIncludes(((AndroidNdk)sdk).GnuBinutils)
                .WithPositionIndependentCode(true);
        }
    }

    public class AndroidNdkCompilerSettingsNoThumb : AndroidNdkCompilerSettings
    {
        public AndroidNdkCompilerSettingsNoThumb(AndroidNdkCompiler gccCompiler, int apiLevel, bool useStatic) : base(gccCompiler, apiLevel)
        {
            UseStatic = useStatic;
        }

        public override IEnumerable<string> CommandLineFlagsFor(NPath target)
        {
            foreach (var flag in base.CommandLineFlagsFor(target))
            {
                // disabling thumb for Debug configuration to solve problem with Android Studio debugging
                if (flag == "-mthumb" && CodeGen == CodeGen.Debug)
                    yield return "-marm";
                else
                    yield return flag;
            }
            if (UseStatic)
            {
                yield return "-DSTATIC_LINKING";
            }
        }

        private bool UseStatic { get; set; }
    }

    internal class AndroidStaticLinker : LLVMArStaticLinkerForAndroid
    {
        public AndroidStaticLinker(ToolChain toolchain) : base(toolchain)
        {
        }

        protected override BuiltNativeProgram BuiltNativeProgramFor(NPath destination, IEnumerable<PrecompiledLibrary> allLibraries)
        {
            //var staticLibraries = allLibraries.Where(l => l.Static).ToArray();
            return (BuiltNativeProgram) new AndroidApkStaticLibrary(destination, allLibraries.ToArray());
        }
    }

    internal class AndroidApkStaticLibrary : StaticLibrary
    {
        public AndroidApkStaticLibrary(NPath path, PrecompiledLibrary[] libraryDependencies = null) : base(path, libraryDependencies)
        {
            SystemLibraries = libraryDependencies.Where(l => l.System).ToArray();
        }

        public PrecompiledLibrary[] SystemLibraries { get; private set; }
    }

    internal class AndroidMainModuleLinker : AndroidDynamicLinker
    {
        public AndroidMainModuleLinker(AndroidNdkToolchain toolchain) : base(toolchain) { }

        private NPath ChangeMainModuleName(NPath target)
        {
            // need to rename to make it start with "lib", otherwise Android have problems with loading native library
            return target.Parent.Combine("lib" + target.FileName).ChangeExtension("so");
        }

        public override BuiltNativeProgram CombineObjectFiles(NPath destination, CodeGen codegen, IEnumerable<NPath> objectFiles, IEnumerable<PrecompiledLibrary> allLibraries)
        {
            var requiredLibraries = allLibraries.ToList();
            foreach (var l in allLibraries.OfType<AndroidApkStaticLibrary>())
            {
                foreach (var sl in l.SystemLibraries)
                {
                    if (!requiredLibraries.Contains(sl)) requiredLibraries.Add(sl);
                }
            }
            return base.CombineObjectFiles(destination, codegen, objectFiles, requiredLibraries);
        }

        protected override IEnumerable<string> CommandLineFlagsForLibrary(PrecompiledLibrary library, CodeGen codegen)
        {
            // if lib which contains all JNI code is linked statically, then all methods from this lib should be exposed
            var entryPoint = library.ToString().Contains("lib_unity_tiny_android.a");
            if (entryPoint)
            {
                yield return "-Wl,--whole-archive";
            }
            foreach (var flag in base.CommandLineFlagsForLibrary(library, codegen))
            {
                yield return flag;
            }
            if (entryPoint)
            {
                yield return "-Wl,--no-whole-archive";
            }
        }

        protected override IEnumerable<string> CommandLineFlagsFor(NPath target, CodeGen codegen, IEnumerable<NPath> inputFiles)
        {
            foreach (var flag in base.CommandLineFlagsFor(ChangeMainModuleName(target), codegen, inputFiles))
            {
                yield return flag;
            }
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
            new AndroidDynamicLinker(toolchain).AsDynamicLibrary().WithStaticCppRuntime(toolchain.Sdk.Version.Major >= 19))
        {
        }
    }

    internal sealed class AndroidApkStaticLibraryFormat : NativeProgramFormat
    {
        public override string Extension { get; } = "a";

        internal AndroidApkStaticLibraryFormat(AndroidNdkToolchain toolchain) : base(
            new AndroidStaticLinker(toolchain))
        {
        }
    }

    internal sealed class AndroidApkMainModuleFormat : NativeProgramFormat
    {
        public override string Extension { get; } = "apk";

        internal AndroidApkMainModuleFormat(AndroidNdkToolchain toolchain) : base(
            new AndroidMainModuleLinker(toolchain).AsDynamicLibrary().WithStaticCppRuntime(toolchain.Sdk.Version.Major >= 19))
        {
        }
    }

    internal class AndroidMainDynamicLibrary : DynamicLibrary, IPackagedAppExtension
    {
        private AndroidApkToolchain m_apkToolchain;
        private String m_gameName;
        private DotsConfiguration m_config;
        private IEnumerable<IDeployable> m_supportFiles;

        public AndroidMainDynamicLibrary(NPath path, AndroidApkToolchain toolchain, params PrecompiledLibrary[] dynamicLibraryDependencies) : base(path, dynamicLibraryDependencies)
        {
            m_apkToolchain = toolchain;
        }

        public void SetAppPackagingParameters(String gameName, DotsConfiguration config, IEnumerable<IDeployable> supportFiles)
        {
            m_gameName = gameName;
            m_config = config;
            m_supportFiles = supportFiles;
        }

        private NPath PackageApp(NPath buildPath, NPath mainLibPath)
        {
            var deployedPath = buildPath.Combine(m_gameName + ".apk");
            if (m_apkToolchain == null)
            {
                Console.WriteLine($"Error: not Android APK toolchain");
                return deployedPath;
            }

            var gradleProjectPath = mainLibPath.Parent.Parent.Parent.Parent.Parent;
            var pathToRoot = new NPath(string.Concat(Enumerable.Repeat("../", gradleProjectPath.Depth)));
            var apkSrcPath = AsmDefConfigFile.AsmDefDescriptionFor("Unity.Platforms.Android").Path.Parent.Combine("AndroidProjectTemplate~/");

            var javaLaunchPath = m_apkToolchain.JavaPath.Combine("bin").Combine("java");
            var gradleLaunchPath = m_apkToolchain.GetGradleLaunchJarPath();
            var releaseApk = m_config == DotsConfiguration.Release;
            var gradleCommand = releaseApk ? "assembleRelease" : "assembleDebug";
            var deleteCommand = Unity.BuildTools.HostPlatform.IsWindows ? $"del /f /q {deployedPath.InQuotes(SlashMode.Native)} 2> nul" : $"rm -f {deployedPath.InQuotes(SlashMode.Native)}";
            var gradleExecutableString = $"{deleteCommand} && cd {gradleProjectPath.InQuotes()} && {javaLaunchPath.InQuotes()} -classpath {gradleLaunchPath.InQuotes()} org.gradle.launcher.GradleMain {gradleCommand} && cd {pathToRoot.InQuotes()}";

            var apkPath = gradleProjectPath.Combine("build/outputs/apk").Combine(releaseApk ? "release/gradle-release.apk" : "debug/gradle-debug.apk");

            Backend.Current.AddAction(
                actionName: "Build Gradle project",
                targetFiles: new[] { apkPath },
                inputs: m_apkToolchain.RequiredArtifacts.Append(mainLibPath).Concat(m_supportFiles.Select(d=>d.Path)).ToArray(),
                executableStringFor: gradleExecutableString,
                commandLineArguments: Array.Empty<string>(),
                allowUnexpectedOutput: false,
                allowedOutputSubstrings: new[] { ":*", "BUILD SUCCESSFUL in *" }
            );

            bool useStaticLib = Deployables.FirstOrDefault(l => l.ToString().Contains("lib_unity_tiny_android.so")) == default(IDeployable);
            var templateStrings = new Dictionary<string, string>
            {
                { "**TINYLIBNAME**", useStaticLib ? m_gameName : "_unity_tiny_android" },
                { "**TINYNAME**", m_gameName.Replace("-","").ToLower() },
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
            localProperties.AppendLine($"sdk.dir={m_apkToolchain.SdkPath}");
            localProperties.AppendLine($"ndk.dir={m_apkToolchain.Sdk.Path.MakeAbsolute()}");
            var localPropertiesPath = gradleProjectPath.Combine("local.properties");
            Backend.Current.AddWriteTextAction(localPropertiesPath, localProperties.ToString());
            Backend.Current.AddDependency(apkPath, localPropertiesPath);

            // copy additional resources and Data files
            // TODO: better to use move from main lib directory
            foreach (var r in m_supportFiles)
            {
                var targetAssetPath = gradleProjectPath.Combine("src/main/assets");
                if (r.Path.FileName == "testconfig.json")
                {
                    targetAssetPath = buildPath.Combine(r.Path.FileName);
                }
                else if (r is DeployableFile && (r as DeployableFile).RelativeDeployPath != null)
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
            var libDirectory = Path.Parent.Combine("gradle/src/main/jniLibs/armeabi-v7a");
            var result = base.DeployTo(libDirectory, alreadyDeployed);

            return new Executable(PackageApp(targetDirectory, result.Path));
        }
    }

}

