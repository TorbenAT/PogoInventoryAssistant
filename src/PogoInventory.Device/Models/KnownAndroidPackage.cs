namespace PogoInventory.Device.Models;

public enum KnownAndroidPackage
{
    Calcy
}

public static class KnownAndroidPackageNames
{
    public const string Calcy = "tesmath.calcy";

    public static string GetPackageName(KnownAndroidPackage app) =>
        app switch
        {
            KnownAndroidPackage.Calcy => Calcy,
            _ => throw new ArgumentOutOfRangeException(nameof(app), app, "Unknown Android package.")
        };
}
