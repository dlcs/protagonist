using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace DLCS.Web.Views;

/// <summary>
/// <see cref="IControllerModelConvention"/> that sets "feature" property in ControllerModel
/// </summary>
public class FeatureControllerModelConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        controller.Properties.Add("feature", DeriveFeatureFolderName(controller));
    }
    
    private string DeriveFeatureFolderName(ControllerModel model)
    {
        var controllerNamespace = model.ControllerType.Namespace;
        var result = controllerNamespace.Split('.')
            .SkipWhile(s => s != "Features")
            .Aggregate(string.Empty, Path.Combine);

        return result;
    }
}