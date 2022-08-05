using DLCS.Core.Types;
using DLCS.Model.Templates;
using Orchestrator.Assets;

namespace Orchestrator.Settings;

/// <summary>
/// A collection of extension methods for OrchestratorSettings
/// </summary>
public static class OrchestratorSettingsX
{
    /// <summary>
    /// Get the local folder path for Asset. This is where it will be orchestrated to, or found on fast disk after
    /// orchestration.
    /// </summary>
    public static string GetImageLocalPath(this OrchestratorSettings settings, AssetId assetId)
        => TemplatedFolders.GenerateFolderTemplate(settings.ImageFolderTemplateOrchestrator, assetId);

    /// <summary>
    /// Get the full redirect path for ImageServer. Includes path prefix and parsed location where image-server can
    /// access Asset file.
    /// This will return the endpoint for highest supported ImageApiVersion 
    /// </summary>
    public static string GetImageServerPath(this OrchestratorSettings settings, AssetId assetId)
        => GetImageServerFilePathInternal(assetId, settings.ImageServerConfig,
            settings.ImageServerConfig.DefaultVersionPathTemplate);

    /// <summary>
    /// Get the full redirect path for ImageServer for specified ImageApi version. Includes path prefix and parsed
    /// location where image-server can access Asset file.
    /// </summary>
    /// <returns>Path for image-server if image-server can handle requested version, else null</returns>
    public static string? GetImageServerPath(this OrchestratorSettings settings, AssetId assetId,
        IIIF.ImageApi.Version targetVersion)
        => settings.ImageServerConfig.VersionPathTemplates.TryGetValue(targetVersion, out var pathTemplate)
            ? GetImageServerFilePathInternal(assetId, settings.ImageServerConfig, pathTemplate)
            : null;

    /// <summary>
    /// Get the full redirect path for SpecialServer for specified ImageApi version. Includes parsed S3 location where
    /// image-server can access asset file
    /// </summary>
    /// <returns>Path for image-server if image-server can handle requested version, else null</returns>
    public static string? GetSpecialServerPath(this OrchestratorSettings settings, string s3Location,
        IIIF.ImageApi.Version targetVersion)
    {
        if (!settings.ImageServerPathConfig.TryGetValue(ImageServer.Cantaloupe, out var imageServerConfig))
        {
            return null;
        }

        if (!imageServerConfig.VersionPathTemplates.TryGetValue(targetVersion, out var pathTemplate))
        {
            return null;
        }

        return $"{pathTemplate}{s3Location.Replace("/", imageServerConfig.Separator)}";
    }

    private static string GetImageServerFilePathInternal(AssetId assetId, ImageServerConfig imageServerConfig,
        string versionTemplate)
    {
        var imageServerFilePath = TemplatedFolders.GenerateTemplate(imageServerConfig.PathTemplate, assetId,
            imageServerConfig.Separator);

        return $"{versionTemplate}{imageServerFilePath}";
    }
}