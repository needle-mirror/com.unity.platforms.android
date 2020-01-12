using System.IO;
using Unity.Platforms.Build;

namespace Unity.Platforms.Android.Build
{
    public sealed class BuildArtifactAndroid : IBuildArtifact
    {
        public FileInfo OutputTargetFile;
    }
}
