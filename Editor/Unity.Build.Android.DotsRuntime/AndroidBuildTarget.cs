using System;
using System.IO;
using Unity.Build.DotsRuntime;
using BuildTarget = Unity.Build.DotsRuntime.BuildTarget;

namespace Unity.Build.Android.DotsRuntime
{
    public class AndroidBuildTarget32 : AndroidBuildTarget
    {
        public override string DisplayName => "Android32";
        public override string BeeTargetName => "android_armv7";
    }

    public class AndroidBuildTarget64 : AndroidBuildTarget
    {
        public override string DisplayName => "Android64";
        public override string BeeTargetName => "android_arm64";
    }

    public class AndroidBuildTargetFat : AndroidBuildTarget
    {
        public override string DisplayName => "AndroidFat";
        public override string BeeTargetName => "android_fat";
    }

    public abstract class AndroidBuildTarget : BuildTarget
    {
        public override bool CanBuild => true;
        public override string ExecutableExtension => ".apk";
        public override string UnityPlatformName => nameof(UnityEditor.BuildTarget.Android);
        public override bool UsesIL2CPP => true;

        public override Type[] UsedComponents { get; } =
        {
            typeof(AndroidExternalTools),
            typeof(AndroidAPILevels)
        };

        private string GetPackageName(string name)
        {
            return $"com.unity3d.{name}";
        }

        private ShellProcessOutput InstallApp(string adbPath, string name, string apkName, string buildDir)
        {
            // checking that app is already installed
            var result = Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = adbPath,
                Arguments = new string[] { "shell", "pm", "list", "packages", GetPackageName(name) },
                WorkingDirectory = new DirectoryInfo(buildDir)
            });
            if (result.FullOutput.Contains(GetPackageName(name)))
            {
                // uninstall previous version, it may be signed with different key, so re-installing is not possible
                result = Shell.Run(new ShellProcessArgs()
                {
                    ThrowOnError = false,
                    Executable = adbPath,
                    Arguments = new string[] { "uninstall", GetPackageName(name) },
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
                        "-n", $"{GetPackageName(name)}/com.unity3d.tinyplayer.UnityTinyActivity"
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
                        GetPackageName(name)
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
    }
}
