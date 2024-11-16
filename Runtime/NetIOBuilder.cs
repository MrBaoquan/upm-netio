using UNIHper;
using UnityEditor;

public static class NetIOBuilder
{
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void AddToUNIHper()
    {
        UNIHperSettings.AddAssemblyToSettingsIfNotExists("NetIO.Runtime");
    }
#endif

    public static void AutoBuild()
    {
        Managements.Resource.AddConfig("NetIO_resources");
        Managements.UI.AddConfig("NetIO_uis");
    }
}
