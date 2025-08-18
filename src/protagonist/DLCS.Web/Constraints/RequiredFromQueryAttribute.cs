using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace DLCS.Web.Constraints;

/// <summary>
/// Binding attribute to enforce required query string parameter
/// </summary>
/// <see cref="https://www.strathweb.com/2016/09/required-query-string-parameters-in-asp-net-core-mvc/"/>
public class RequiredFromQueryAttribute : FromQueryAttribute, IParameterModelConvention
{
    public void Apply(ParameterModel parameter)
    {
        if (parameter.Action.Selectors != null && parameter.Action.Selectors.Any())
        {
            parameter.Action.Selectors.Last().ActionConstraints.Add(
                new RequiredFromQueryActionConstraint(parameter.BindingInfo?.BinderModelName ??
                                                      parameter.ParameterName));
        }
    }
}

/// <summary>
/// <see cref="IActionConstraint"/> that rejects if query string parameters don't include specified parameter
/// </summary>
public class RequiredFromQueryActionConstraint : IActionConstraint
{
    private readonly string parameter;

    public RequiredFromQueryActionConstraint(string parameter)
    {
        this.parameter = parameter;
    }

    public int Order => 999;

    public bool Accept(ActionConstraintContext context) 
        => context.RouteContext.HttpContext.Request.Query.ContainsKey(parameter);
}