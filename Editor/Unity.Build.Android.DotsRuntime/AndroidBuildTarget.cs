using System;
using System.IO;
using Unity.Build.Common;
using Unity.Build.DotsRuntime;
using Unity.Build.Android;
using BuildTarget = Unity.Build.DotsRuntime.BuildTarget;

namespace Unity.Build.Android.DotsRuntime
{
    public class AndroidBuildTarget : BuildTarget
    {
        public override string DisplayName => "Android";
        public override string BeeTargetName => "android_armv7";
        public override bool CanBuild => true;
        public override bool CanRun => ExportSettings?.ExportProject != true;
        public override string ExecutableExtension => ExportSettings?.ExportProject == true ? "" :
                                                      (ExportSettings?.TargetType == AndroidTargetType.AndroidAppBundle ? ".aab" : ".apk");
        public override string UnityPlatformName => nameof(UnityEditor.BuildTarget.Android);
        public override bool UsesIL2CPP => true;

        public override Type[] UsedComponents { get; } =
        {
            typeof(GeneralSettings),
            typeof(ApplicationIdentifier),
            typeof(AndroidBundleVersionCode),
            typeof(ScreenOrientations),
            typeof(AndroidAPILevels),
            typeof(AndroidArchitectures),
            typeof(AndroidExternalTools),
            typeof(AndroidExportSettings),
            typeof(AndroidInstallLocation)
        };

        string PackageName { get; set; }
        AndroidExportSettings ExportSettings { get; set; }

        string m_BundleToolJar;
        string BundleToolJar
        {
            get
            {
                if (!string.IsNullOrEmpty(m_BundleToolJar))
                {
                    return m_BundleToolJar;
                }
                //TODO revisit this later, probably use AndroidBuildContext to get bundletool path
                var androidEngine = UnityEditor.BuildPipeline.GetPlaybackEngineDirectory(UnityEditor.BuildTarget.Android, UnityEditor.BuildOptions.None);
                var androidTools = Path.Combine(androidEngine, "Tools");
                var bundleToolFiles = Directory.GetFiles(androidTools, "bundletool-all-*.jar");
                if (bundleToolFiles.Length != 1)
                    throw new Exception($"Failed to find bundletool in {androidTools}");
                m_BundleToolJar = bundleToolFiles[0];
                return m_BundleToolJar;
            }
        }

        private ShellProcessOutput UninstallApp(string adbPath, string apkName, string buildDir)
        {
            // checking that app is already installed
            var result = Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = adbPath,
                Arguments = new string[] { "shell", "pm", "list", "packages", PackageName },
                WorkingDirectory = new DirectoryInfo(buildDir)
            });
            if (result.FullOutput.Contains(PackageName))
            {
                // uninstall previous version, it may be signed with different key, so re-installing is not possible
                result = Shell.Run(new ShellProcessArgs()
                {
                    ThrowOnError = false,
                    Executable = adbPath,
                    Arguments = new string[] { "uninstall", PackageName },
                    WorkingDirectory = new DirectoryInfo(buildDir)
                });
            }
            return result;
        }

        private ShellProcessOutput InstallApk(string adbPath, string apkName, string buildDir)
        {
            return Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = adbPath,
                Arguments = new string[] { "install", "\"" + apkName + "\"" },
                WorkingDirectory = new DirectoryInfo(buildDir)
            });
        }

        static string GetApksName(string buildDir)
        {
            return Path.Combine(buildDir, "bundle.apks");
        }

        private ShellProcessOutput BuildApks(string aabName, string buildDir)
        {
            var apksName = GetApksName(buildDir);
            //TODO check for mutliple device installing
            return Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = AndroidTools.JavaPath,
                //TODO add signing params in case of custom keystore
                Arguments = new string[] {
                    "-jar",
                    $"\"{BundleToolJar}\"",
                    "build-apks",
                    $"--bundle=\"{aabName}\"",
                    $"--output=\"{apksName}\"",
                    "--overwrite"
                },
                WorkingDirectory = new DirectoryInfo(buildDir)
            });
        }

        private ShellProcessOutput InstallApks(string adbPath, string buildDir)
        {
            var apksName = GetApksName(buildDir);
            return Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = AndroidTools.JavaPath,
                Arguments = new string[] {
                    "-jar",
                    $"\"{BundleToolJar}\"",
                    "install-apks",
                    $"--apks=\"{apksName}\"",
                    $"--adb=\"{adbPath}\""
                },
                WorkingDirectory = new DirectoryInfo(buildDir)
            });
        }

        private ShellProcessOutput LaunchApp(string adbPath, string buildDir)
        {
            return Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = adbPath,
                Arguments = new string[] {
                        "shell", "am", "start",
                        "-a", "android.intent.action.MAIN",
                        "-c", "android.intent.category.LAUNCHER",
                        "-f", "0x10200000",
                        "-S",
                        "-n", $"{PackageName}/com.unity3d.tinyplayer.UnityTinyActivity"
                },
                WorkingDirectory = new DirectoryInfo(buildDir)
            });
        }

        public override bool Run(FileInfo buildTarget)
        {
            var buildDir = buildTarget.Directory.FullName;
            var adbPath = AndroidTools.AdbPath;

            var result = UninstallApp(adbPath, buildTarget.FullName, buildDir);
            if (ExportSettings?.TargetType == AndroidTargetType.AndroidAppBundle)
            {
                result = BuildApks(buildTarget.FullName, buildDir);
                // bundletool might write to stderr even if there are no errors
                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot build APKS : {result.FullOutput}");
                }
                result = InstallApks(adbPath, buildDir);
                if (result.ExitCode != 0)
                {
                    throw new Exception($"Cannot install APKS : {result.FullOutput}");
                }
            }
            else
            {
                result = InstallApk(adbPath, buildTarget.FullName, buildDir);
                if (!result.FullOutput.Contains("Success"))
                {
                    throw new Exception($"Cannot install APK : {result.FullOutput}");
                }
            }
            result = LaunchApp(adbPath, buildDir);
            if (result.Succeeded)
            {
                return true;
            }
            else
            {
                throw new Exception($"Cannot launch APK : {result.FullOutput}");
            }
        }

        public override ShellProcessOutput RunTestMode(string exeName, string workingDirPath, int timeout)
        {
            ShellProcessOutput output;
            var adbPath = AndroidTools.AdbPath;

            var executable = $"{workingDirPath}/{exeName}{ExecutableExtension}";
            output = InstallApk(adbPath, executable, workingDirPath);
            if (!output.FullOutput.Contains("Success"))
            {
                return output;
            }

            // clear logcat
            Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = adbPath,
                Arguments = new string[] {
                        "logcat", "-c"
                },
                WorkingDirectory = new DirectoryInfo(workingDirPath)
            });

            output = LaunchApp(adbPath, workingDirPath);

            System.Threading.Thread.Sleep(timeout == 0 ? 2000 : timeout); // to kill process anyway,
                                                                          // should be rewritten to support tests which quits after execution

            // killing on timeout
            Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = adbPath,
                Arguments = new string[] {
                        "shell", "am", "force-stop",
                        PackageName
                },
                WorkingDirectory = new DirectoryInfo(workingDirPath)
            });

            // get logcat
            output = Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = adbPath,
                Arguments = new string[] {
                        "logcat", "-d"
                },
                WorkingDirectory = new DirectoryInfo(workingDirPath)
            });
            if (timeout == 0) // non-sample test, TODO invent something better
            {
                output.Succeeded = output.FullOutput.Contains("Test suite: SUCCESS");
            }
            return output;
        }

        public override void WriteBuildConfiguration(BuildContext context, string path)
        {
            base.WriteBuildConfiguration(context, path);
            var appId = context.GetComponentOrDefault<ApplicationIdentifier>();
            PackageName = appId.PackageName;
            ExportSettings = context.GetComponentOrDefault<AndroidExportSettings>();
        }
    }
}
