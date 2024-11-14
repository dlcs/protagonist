using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Orchestrator.Infrastructure;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ResponseNoCacheOnSuccessAttribute : ActionFilterAttribute
{
    private readonly List<int> disallowedStatusCodes = new ()
    {
        400
    };
    
    public override void OnResultExecuting(ResultExecutingContext filterContext)
    {
        var result = filterContext.Result as ObjectResult;
        
        if ((result != null && IsAllowedStatusCode(result.StatusCode!.Value)) || 
            (result == null && IsAllowedStatusCode(filterContext.HttpContext.Response.StatusCode))) // avoids issues with ContentResult types
        {
            filterContext.HttpContext.Response.Headers.CacheControl = "no-cache,no-store";
        }

        base.OnResultExecuting(filterContext);
    }

    private bool IsAllowedStatusCode(int statusCode)
    {
        return disallowedStatusCodes.All(s => s != statusCode);
    }
}