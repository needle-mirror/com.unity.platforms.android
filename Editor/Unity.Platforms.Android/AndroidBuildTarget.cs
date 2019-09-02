using System;
using System.IO;

namespace Unity.Platforms.Android
{
    public class AndroidBuildTarget : BuildTarget
    {
        public override string GetDisplayName()
        {
            return "Android";
        }

        public override string GetBeeTargetName()
        {
            return "android_armv7";
        }

        public override string GetExecutableExtension()
        {
            return ".apk";
        }

        public override string GetUnityPlatformName()
        {
            return nameof(UnityEditor.BuildTarget.Android);
        }

        private static string AdbName
        {
            get
            {
#if UNITY_EDITOR_WIN
                return "adb.exe";
#elif UNITY_EDITOR_OSX
                return "adb";
#else
                return "adb";
#endif
            }
        }

        public override bool Run(FileInfo buildTarget)
        {
            var buildDir = buildTarget.Directory.FullName;
            var propsFileName = Path.Combine(buildDir, "gradle", "local.properties");
            var localProps = File.ReadAllLines(propsFileName);
            string sdkPath = null;
            foreach (var line in localProps)
            {
                if (line.StartsWith("sdk.dir="))
                {
                    sdkPath = line.Substring(8);
                    break;
                }
            }
            if (sdkPath != null)
            {
                var adbPath = Path.Combine(sdkPath, "platform-tools", AdbName);
                // install build
                var result = Shell.Run(new ShellProcessArgs()
                {
                    ThrowOnError = false,
                    Executable = adbPath,
                    Arguments = new string[] { "install", "-r", buildTarget.FullName },
                    WorkingDirectory = new DirectoryInfo(buildDir)
                });
                if (result.FullOutput.IndexOf("Success") >= 0)
                {
                    // run build
                    var name = Path.GetFileNameWithoutExtension(buildTarget.Name).ToLower();
                    result = Shell.Run(new ShellProcessArgs()
                    {
                        ThrowOnError = false,
                        Executable = adbPath,
                        Arguments = new string[] {
                                "shell", "am", "start",
                                "-a", "android.intent.action.MAIN",
                                "-c", "android.intent.category.LAUNCHER",
                                "-f", "0x10200000",
                                "-S",
                                "-n", $"com.unity3d.{name}/com.unity3d.tinyplayer.UnityTinyActivity"
                        },
                        WorkingDirectory = new DirectoryInfo(buildDir)
                    });
                    if (result.Succeeded)
                    {
                        return true;
                    }
                    else
                    {
                        throw new Exception($"Cannot launch APK : {result.FullOutput}");
                    }
                }
                else
                {
                    throw new Exception($"Cannot install APK : {result.FullOutput}");
                }
            }
            else
            {
                throw new Exception($"Cannot find Android SDK path in {propsFileName}");
            }
        }
    }
}
