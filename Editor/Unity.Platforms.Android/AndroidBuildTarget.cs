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

        private static string AdbPath(string buildDir)
        {
            try
            {
                var localProps = File.ReadAllLines(Path.Combine(buildDir, "local.properties"));
                foreach (var line in localProps)
                {
                    if (line.StartsWith("sdk.dir="))
                    {
                        return Path.Combine(line.Substring(8), "platform-tools", AdbName);
                    }
                }
            }
            catch (Exception)
            {
                // file not found or other file system related problems
            }
            return null;
        }

        private ShellProcessOutput InstallApp(string adbPath, string apkName, string buildDir)
        {
            return Shell.Run(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = adbPath,
                Arguments = new string[] { "install", "-r", apkName },
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
                        "-n", $"com.unity3d.{name}/com.unity3d.tinyplayer.UnityTinyActivity"
                },
                WorkingDirectory = new DirectoryInfo(buildDir)
            });
        }

        public override bool Run(FileInfo buildTarget)
        {
            var buildDir = buildTarget.Directory.FullName;
            var adbPath = AdbPath(buildDir);
            if (adbPath != null)
            {
                var result = InstallApp(adbPath, buildTarget.FullName, buildDir);
                if (result.FullOutput.Contains("Success"))
                {
                    var name = Path.GetFileNameWithoutExtension(buildTarget.Name).ToLower();
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
                else
                {
                    throw new Exception($"Cannot install APK : {result.FullOutput}");
                }
            }
            else
            {
                throw new Exception("Cannot find Android SDK path in local.properties");
            }
        }

        public override ShellProcessOutput RunTestMode(string exeName, string workingDirPath, int timeout)
        {
            ShellProcessOutput output;
            var adbPath = AdbPath(workingDirPath);
            var executable = $"{workingDirPath}/{exeName}{GetExecutableExtension()}";
            if (adbPath != null)
            {
                output = InstallApp(adbPath, executable, workingDirPath);
                if (output.FullOutput.Contains("Success"))
                {
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

                    var name = exeName.ToLower();
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
                                $"com.unity3d.{name}"
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
                }
            }
            else
            {
                output = new ShellProcessOutput
                {
                    Succeeded = false,
                    ExitCode = 1,
                    FullOutput = "Cannot find Android SDK path in local.properties"
                };
            }
            return output;
        }
    }
}
