using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;

namespace DLCS.Web.Views;

public static class ServiceCollectionX
{
    /// <summary>
    /// Update view discovery to use feature folders. Will check in
    /// {Feature}\{ViewName}.cshtml
    /// {Feature}\Views\{ViewName}.cshtml
    /// \Features\{Controller}\{ViewName}.cshtml
    /// </summary>
    /// <param name="services">Current IServiceCollection object</param>
    /// <returns>Modified IServiceCollection object</returns>
    public static IServiceCollection AddFeatureFolderViews(this IServiceCollection services)
    {
        services
            .Configure<MvcOptions>(opts =>
            {
                opts.Conventions.Add(new FeatureControllerModelConvention());
            })
            .Configure<RazorViewEngineOptions>(opts =>
            {
                opts.ViewLocationFormats.Clear();
                opts.ViewLocationFormats.Add(@"{Feature}\{0}.cshtml");
                opts.ViewLocationFormats.Add(@"{Feature}\Views\{0}.cshtml");
                opts.ViewLocationFormats.Add(@"\Features\{0}\{1}.cshtml");

                opts.ViewLocationExpanders.Add(new FeatureFolderViewExpander());
            });

        return services;
    }
}