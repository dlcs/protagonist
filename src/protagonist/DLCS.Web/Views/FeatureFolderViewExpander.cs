using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;

namespace DLCS.Web.Views;

/// <summary>
/// <see cref="IViewLocationExpander"/> to find views in Feature folders
/// </summary>
public class FeatureFolderViewExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
        // no-op
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        var controllerDescriptor = context.ActionContext.ActionDescriptor as ControllerActionDescriptor;
        var featureName = controllerDescriptor?.Properties["feature"] as string;

        foreach (var location in viewLocations)
        {
            yield return location.Replace("{Feature}", featureName);
        }
    }
}