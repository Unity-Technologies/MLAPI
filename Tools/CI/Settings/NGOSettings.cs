using RecipeEngine.Api.Settings;
using RecipeEngine.Modules.Wrench.Models;
using RecipeEngine.Modules.Wrench.Settings;

namespace NGO.Cookbook.Settings;

public class NGOSettings : AnnotatedSettingsBase
{
    // Path from the root of the repository where packages are located.
    readonly string[] packagesRootPaths = {"."};

    // update this to list all packages in this repo that you want to release.
    Dictionary<string, PackageOptions> PackageOptions = new()
    {
        {
            "com.unity.netcode.gameobjects",
            new PackageOptions() { ReleaseOptions = new ReleaseOptions() { IsReleasing = true } }
        }
    };

    public NGOSettings()
    {
        Wrench = new WrenchSettings(
            packagesRootPaths,
            PackageOptions,
            false,
            false,
            @"Tools\CI\NGO.Cookbook.csproj"); // There should be fix soon and there should be no need of specifying the path

        Wrench.PvpProfilesToCheck = new HashSet<string>() { "supported" };
    }

    public WrenchSettings Wrench { get; private set; }
}
