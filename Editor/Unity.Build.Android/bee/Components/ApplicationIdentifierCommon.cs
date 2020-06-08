using Unity.Properties;

namespace Unity.Build.Android
{
    internal sealed partial class ApplicationIdentifier
    {
        string m_PackageName;

        [CreateProperty]
        public string PackageName
        {
            get => !string.IsNullOrEmpty(m_PackageName) ? m_PackageName : "com.unity.DefaultPackage";
            set => m_PackageName = value;
        }

    }
}
