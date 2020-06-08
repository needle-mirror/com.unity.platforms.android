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
        public override string ExecutableExtension => ".apk";
        public override string UnityPlatformName => nameof(UnityEditor.BuildTarget.Android);
        public override bool UsesIL2CPP => true;

        public override Type[] UsedComponents { get; } =
        {
            typeof(GeneralSettings),
            typeof(ApplicationIdentifier),
            typeof(AndroidAPILevels),
            typeof(AndroidArchitectures),
            typeof(AndroidExternalTools),
        };

        string PackageName { get; set; }

        private ShellProcessOutput InstallApp(string adbPath, string name, string apkName, string buildDir)
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

            return Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = adbPath,
                Arguments = new string[] { "install", "\"" + apkName + "\"" },
                WorkingDirectory = new DirectoryInfo(buildDir)
            });
        }

        private ShellProcessOutput LaunchApp(string adbPath, string name, string buildDir)
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
            var name = Path.GetFileNameWithoutExtension(buildTarget.Name).ToLower();
            var result = InstallApp(adbPath, name, buildTarget.FullName, buildDir);
            if (!result.FullOutput.Contains("Success"))
            {
                throw new Exception($"Cannot install APK : {result.FullOutput}");
            }
            result = LaunchApp(adbPath, name, buildDir);
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

            var name = exeName.ToLower();
            var executable = $"{workingDirPath}/{exeName}{ExecutableExtension}";
            output = InstallApp(adbPath, name, executable, workingDirPath);
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

            output = LaunchApp(adbPath, name, workingDirPath);

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
        }
    }
}
