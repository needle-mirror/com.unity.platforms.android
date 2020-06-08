using System;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        public enum TargetType
        {
            Single,
            Main,
            Complementary
        }

        private static Dictionary<Tuple<Architecture, TargetType>, AndroidApkToolchain> ToolChains = new Dictionary<Tuple<Architecture, TargetType>, AndroidApkToolchain>();

        public TargetType Type { get; private set; }

        public override CLikeCompiler CppCompiler { get; }
        public override NativeProgramFormat DynamicLibraryFormat { get; }
        public override NativeProgramFormat ExecutableFormat { get; }

        public List<NPath> RequiredArtifacts = new List<NPath>();

        // Build configuration
        public static NPath SdkPath { get; private set; }
        public static NPath NdkPath { get; private set; }
        public static NPath JavaPath { get; private set; }
        public static NPath GradlePath { get; private set; }
        public static int MinAPILevel { get; private set; }
        public static int TargetAPILevel { get; private set; }

        static AndroidApkToolchain()
        {
            // reading configuration
            var file = NPath.CurrentDirectory.Combine("buildconfiguration.json");

            if (file.FileExists())
            {
                var json = file.ReadAllText();
                var jarray = JArray.Parse(json);
                foreach (var jobject in jarray)
                {
                    var type = jobject["$type"].Value<string>();
                    switch (type)
                    {
                        case "Unity.Build.Android.AndroidExternalTools, Unity.Build.Android":
                            JavaPath = new NPath(jobject["JavaPath"].Value<string>());
                            SdkPath = new NPath(jobject["SdkPath"].Value<string>());
                            NdkPath = new NPath(jobject["NdkPath"].Value<string>());
                            GradlePath = new NPath(jobject["GradlePath"].Value<string>());
                            break;
                        case "Unity.Build.Android.AndroidAPILevels, Unity.Build.Android":
                            MinAPILevel = jobject["MinAPILevel"].Value<int>();
                            TargetAPILevel = jobject["TargetAPILevel"].Value<int>();
                            break;
                    }
                }
            }
        }

        public static AndroidApkToolchain GetToolChain(bool useStatic, Architecture architecture, TargetType type)
        {
            var key = new Tuple<Architecture, TargetType>(architecture, type);
            if (!ToolChains.ContainsKey(key))
            {
                var locator = new AndroidNdkLocator(architecture);
                var androidNdk = NdkPath == null || !NdkPath.DirectoryExists() ||
                                 SdkPath == null || !SdkPath.DirectoryExists() ||
                                 JavaPath == null || !JavaPath.DirectoryExists() ||
                                 GradlePath == null || !GradlePath.DirectoryExists() ?
                    locator.UserDefaultOrDummy : locator.UseSpecific(NdkPath);
                var toolchain = new AndroidApkToolchain(androidNdk, useStatic, type);
                ToolChains.Add(key, toolchain);
            }
            return ToolChains[key];
        }

        public AndroidApkToolchain(AndroidNdk ndk, bool useStatic, TargetType type) : base(ndk)
        {
            Type = type;
            DynamicLibraryFormat = useStatic ? new AndroidApkStaticLibraryFormat(this) as NativeProgramFormat :
                                               new AndroidApkDynamicLibraryFormat(this) as NativeProgramFormat;
            ExecutableFormat = (type == TargetType.Complementary) ?
                                new AndroidApkMainModuleFormat(this) as NativeProgramFormat :
                                new AndroidApkFormat(this) as NativeProgramFormat;
            CppCompiler = new AndroidNdkCompilerNoThumb(ActionName, Architecture, Platform, Sdk, ndk.ApiLevel, useStatic);
        }

        public static NPath GetGradleLaunchJarPath()
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

    // AndroidApkStaticLibraryFormat / AndroidStaticLinker / AndroidApkStaticLibrary are being used instead of
    // AndroidNdkStaticLibraryFormat / LLVMArStaticLinkerForAndroid / StaticLibrary because it is required to keep
    // system libs on which this lib depends on, but which are not embedded. Information about these system libs
    // is required when this static lib is being used as an input file when linking some other library.
    // This has been discussed here https://unity.slack.com/archives/C1RM0NBLY/p1589960319112500
    // Jira ticket has been created https://jira.unity3d.com/browse/DS-243
    // TODO: get rid of these extra classes once required functionality is added to StaticLibrary.
    internal sealed class AndroidApkStaticLibraryFormat : NativeProgramFormat
    {
        public override string Extension { get; } = "a";

        internal AndroidApkStaticLibraryFormat(AndroidNdkToolchain toolchain) : base(
            new AndroidStaticLinker(toolchain))
        {
        }
    }

    internal class AndroidStaticLinker : LLVMArStaticLinkerForAndroid
    {
        public AndroidStaticLinker(ToolChain toolchain) : base(toolchain)
        {
        }

        protected override BuiltNativeProgram BuiltNativeProgramFor(NPath destination, IEnumerable<PrecompiledLibrary> allLibraries)
        {
            return (BuiltNativeProgram)new AndroidApkStaticLibrary(destination, allLibraries.ToArray());
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

    internal sealed class AndroidApkDynamicLibraryFormat : NativeProgramFormat
    {
        public override string Extension { get; } = "so";

        internal AndroidApkDynamicLibraryFormat(AndroidNdkToolchain toolchain) : base(
            new AndroidDynamicLinker(toolchain).AsDynamicLibrary().WithStaticCppRuntime(toolchain.Sdk.Version.Major >= 19))
        {
        }
    }

    internal sealed class AndroidApkMainModuleFormat : NativeProgramFormat
    {
        public override string Extension { get; } = "so";

        internal AndroidApkMainModuleFormat(AndroidNdkToolchain toolchain) : base(
            new AndroidApkMainModuleLinker(toolchain).AsDynamicLibrary().WithStaticCppRuntime(toolchain.Sdk.Version.Major >= 19))
        {
        }
    }

    internal class AndroidApkMainModuleLinker : AndroidDynamicLinker
    {
        public AndroidApkMainModuleLinker(AndroidNdkToolchain toolchain) : base(toolchain) { }

        protected NPath ChangeMainModuleName(NPath target)
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
            return (BuiltNativeProgram)new AndroidApkMainModule(ChangeMainModuleName(destination), Toolchain as AndroidApkToolchain, dynamicLibraries);
        }
    }

    internal sealed class AndroidApkMainModule : DynamicLibrary
    {
        private String m_libPath;

        public AndroidApkMainModule(NPath path, AndroidApkToolchain toolchain, params PrecompiledLibrary[] dynamicLibraryDependencies) : base(path, dynamicLibraryDependencies)
        {
            m_libPath = toolchain.Architecture is Arm64Architecture ? "arm64-v8a" : "armeabi-v7a";
        }

        public override BuiltNativeProgram DeployTo(NPath targetDirectory, Dictionary<IDeployable, IDeployable> alreadyDeployed = null)
        {
            // This is complementary target, library should be deployed to the corresponding folder of the main target
            // see comment in https://github.com/Unity-Technologies/dots/blob/master/TinySamples/Packages/com.unity.dots.runtime/bee%7E/BuildProgramSources/DotsConfigs.cs
            // DotsConfigs.MakeConfigs() method for details.
            var gradleProjectPath = Path.Parent.Parent.Combine("gradle");
            var libDirectory = gradleProjectPath.Combine("src/main/jniLibs").Combine(m_libPath);
            var result = base.DeployTo(libDirectory, alreadyDeployed);
            // Required to make sure that main target Gradle project depends on this lib and this lib is deployed before packaging step
            Backend.Current.AddDependency(gradleProjectPath.Combine("build.gradle"), result.Path);
            return result;
        }
    }

    internal sealed class AndroidApkFormat : NativeProgramFormat
    {
        public override string Extension { get; } = "apk";

        internal AndroidApkFormat(AndroidNdkToolchain toolchain) : base(
            new AndroidApkLinker(toolchain).AsDynamicLibrary().WithStaticCppRuntime(toolchain.Sdk.Version.Major >= 19))
        {
        }
    }

    internal class AndroidApkLinker : AndroidApkMainModuleLinker
    {
        public AndroidApkLinker(AndroidNdkToolchain toolchain) : base(toolchain) { }

        protected override BuiltNativeProgram BuiltNativeProgramFor(NPath destination, IEnumerable<PrecompiledLibrary> allLibraries)
        {
            var dynamicLibraries = allLibraries.Where(l => l.Dynamic).ToArray();
            return (BuiltNativeProgram)new AndroidApk(ChangeMainModuleName(destination), Toolchain as AndroidApkToolchain, dynamicLibraries);
        }
    }

    internal class AndroidApk : DynamicLibrary, IPackagedAppExtension
    {
        private AndroidApkToolchain m_apkToolchain;
        private String m_gameName;
        private DotsConfiguration m_config;
        private IEnumerable<IDeployable> m_supportFiles;

        public AndroidApk(NPath path, AndroidApkToolchain toolchain, params PrecompiledLibrary[] dynamicLibraryDependencies) : base(path, dynamicLibraryDependencies)
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
            var apkSrcPath = AsmDefConfigFile.AsmDefDescriptionFor("Unity.Build.Android.DotsRuntime").Path.Parent.Combine("AndroidProjectTemplate~/");

            var javaLaunchPath = AndroidApkToolchain.JavaPath.Combine("bin").Combine("java");
            var gradleLaunchPath = AndroidApkToolchain.GetGradleLaunchJarPath();
            var releaseApk = m_config == DotsConfiguration.Release;
            var gradleCommand = releaseApk ? "assembleRelease" : "assembleDebug";
            var gradleExecutableString = $"cd {gradleProjectPath.InQuotes()} && {javaLaunchPath.InQuotes()} -classpath {gradleLaunchPath.InQuotes()} org.gradle.launcher.GradleMain {gradleCommand} && cd {pathToRoot.InQuotes()}";

            var apkPath = gradleProjectPath.Combine("build/outputs/apk").Combine(releaseApk ? "release/gradle-release.apk" : "debug/gradle-debug.apk");

            Backend.Current.AddAction(
                actionName: "Build Gradle project",
                targetFiles: new[] { apkPath },
                inputs: m_apkToolchain.RequiredArtifacts.Append(mainLibPath).Concat(m_supportFiles.Select(d => d.Path)).ToArray(),
                executableStringFor: gradleExecutableString,
                commandLineArguments: Array.Empty<string>(),
                allowUnexpectedOutput: false,
                allowedOutputSubstrings: new[] { ":*", "BUILD SUCCESSFUL in *" }
            );

            var localProperties = new StringBuilder();
            localProperties.AppendLine($"sdk.dir={AndroidApkToolchain.SdkPath}");
            localProperties.AppendLine($"ndk.dir={AndroidApkToolchain.NdkPath}");
            var localPropertiesPath = gradleProjectPath.Combine("local.properties");
            Backend.Current.AddWriteTextAction(localPropertiesPath, localProperties.ToString());
            Backend.Current.AddDependency(apkPath, localPropertiesPath);

            var hasGradleDependencies = false;
            var gradleDependencies = new StringBuilder();
            gradleDependencies.AppendLine("    dependencies {");
            var hasKotlin = false;
            foreach (var d in Deployables.Where(d => (d is DeployableFile)))
            {
                var f = d as DeployableFile;
                if (f.Path.Extension == "aar" || f.Path.Extension == "jar")
                {
                    gradleDependencies.AppendLine($"        compile(name:'{f.Path.FileNameWithoutExtension}', ext:'{f.Path.Extension}')");
                    hasGradleDependencies = true;
                }
                else if (f.Path.Extension == "kt")
                {
                    hasKotlin = true;
                }
            }
            if (hasGradleDependencies)
            {
                gradleDependencies.AppendLine("    }");
            }
            else
            {
                gradleDependencies.Clear();
            }

            var kotlinClassPath = hasKotlin ? "        classpath 'org.jetbrains.kotlin:kotlin-gradle-plugin:1.3.11'" : "";
            var kotlinPlugin = hasKotlin ? "apply plugin: 'kotlin-android'" : "";

            var loadLibraries = new StringBuilder();
            bool useStaticLib = Deployables.FirstOrDefault(l => l.ToString().Contains("lib_unity_tiny_android.so")) == default(IDeployable);
            if (useStaticLib)
            {
                loadLibraries.AppendLine($"        System.loadLibrary(\"{m_gameName}\");");
            }
            else
            {
                var rx = new Regex(@".*lib([\w\d_]+)\.so", RegexOptions.Compiled);
                foreach (var l in Deployables)
                {
                    var match = rx.Match(l.ToString());
                    if (match.Success)
                    {
                        loadLibraries.AppendLine($"        System.loadLibrary(\"{match.Groups[1].Value}\");");
                    }
                }
            }

            String abiFilters = "";
            if (m_apkToolchain.Type == AndroidApkToolchain.TargetType.Single && m_apkToolchain.Architecture is Arm64Architecture)
            {
                abiFilters = "'arm64-v8a'";
            }
            else if (m_apkToolchain.Type == AndroidApkToolchain.TargetType.Single && m_apkToolchain.Architecture is ARMv7Architecture)
            {
                abiFilters = "'armeabi-v7a'";
            }
            else if (m_apkToolchain.Type == AndroidApkToolchain.TargetType.Main && m_apkToolchain.Architecture is ARMv7Architecture)
            {
                abiFilters = "'armeabi-v7a', 'arm64-v8a'";
            }
            else // shouldn't happen
            {
                Console.WriteLine($"Main android toolchain with {m_apkToolchain.Architecture.ToString()}");
            }

            var templateStrings = new Dictionary<string, string>
            {
                { "**LOADLIBRARIES**", loadLibraries.ToString() },
                { "**TINYNAME**", m_gameName.Replace("-","").ToLower() },
                { "**GAMENAME**", m_gameName },
                { "**ABIFILTERS**", abiFilters },
                { "**DEPENDENCIES**", gradleDependencies.ToString() },
                { "**KOTLINCLASSPATH**", kotlinClassPath },
                { "**KOTLINPLUGIN**", kotlinPlugin },
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

            return CopyTool.Instance().Setup(deployedPath, apkPath);
        }

        public override BuiltNativeProgram DeployTo(NPath targetDirectory, Dictionary<IDeployable, IDeployable> alreadyDeployed = null)
        {
            var gradleProjectPath = Path.Parent.Combine("gradle");
            var libDirectory = gradleProjectPath.Combine("src/main/jniLibs");
            libDirectory = libDirectory.Combine(m_apkToolchain.Architecture is Arm64Architecture ? "arm64-v8a" : "armeabi-v7a");

            for (int i = 0; i < Deployables.Length; ++i)
            {
                if (Deployables[i] is DeployableFile)
                {
                    var f = Deployables[i] as DeployableFile;
                    var targetPath = gradleProjectPath.Combine("src/main/assets");
                    if (f.Path.Extension == "java")
                    {
                        targetPath = gradleProjectPath.Combine("src/main/java");
                    }
                    if (f.Path.Extension == "kt")
                    {
                        targetPath = gradleProjectPath.Combine("src/main/kotlin");
                    }
                    else if (f.Path.Extension == "aar" || f.Path.Extension == "jar")
                    {
                        targetPath = gradleProjectPath.Combine("libs");
                    }
                    else if (f.Path.FileName == "testconfig.json")
                    {
                        targetPath = targetDirectory;
                    }
                    targetPath = targetPath.Combine(f.RelativeDeployPath ?? f.Path.FileName);

                    Deployables[i] = new DeployableFile(f.Path, targetPath.RelativeTo(libDirectory));
                }
            }

            var result = base.DeployTo(libDirectory, alreadyDeployed);

            return new Executable(PackageApp(targetDirectory, result.Path));
        }
    }

}

