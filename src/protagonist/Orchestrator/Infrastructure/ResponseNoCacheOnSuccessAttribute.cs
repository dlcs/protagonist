using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Orchestrator.Infrastructure;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ResponseNoCacheOnSuccessAttribute : ActionFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext filterContext)
    {
        var result = filterContext.Result as ObjectResult;
        
        if ((result != null && IsSuccessStatusCode(result.StatusCode!.Value)) || 
            (result == null && IsSuccessStatusCode(filterContext.HttpContext.Response.StatusCode))) // avoids issues with ContentResult types
        {
            filterContext.HttpContext.Response.Headers.CacheControl = "no-cache,no-store";
        }

        base.OnResultExecuting(filterContext);
    }

    private bool IsSuccessStatusCode(int statusCode)
    {
        return statusCode is >= 200 and <= 299;
    }
}