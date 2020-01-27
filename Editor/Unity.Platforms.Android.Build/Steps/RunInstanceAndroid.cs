using Unity.Build;

namespace Unity.Platforms.Android.Build
{
    sealed class RunInstanceAndroid : IRunInstance
    {
        public bool IsRunning => true;

        public RunInstanceAndroid()
        {
        }

        public void Dispose()
        {
        }
    }
}
