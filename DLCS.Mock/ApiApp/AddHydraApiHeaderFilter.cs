using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DLCS.Mock.ApiApp
{
    public class AddHydraApiHeaderFilter : ActionFilterAttribute
    {
        private MockModel model;

        public AddHydraApiHeaderFilter(MockModel model)
        {
            this.model = model;
        }

        // public override void OnResultExecuted(ResultExecutedContext resultExecutedContext)
        // {
        //     var headers = resultExecutedContext.HttpContext.Response.Headers;
        //     AddIfMissing(headers, "Link", "<" + settings.BaseUrl + "/vocab#>; rel=\"http://www.w3.org/ns/hydra/core#apiDocumentation\"");
        //     AddIfMissing(headers, "Access-Control-Allow-Origin", "*");
        //     AddIfMissing(headers, "Access-Control-Expose-Headers", "Link");
        //     if (resultExecutedContext.HttpContext.Response.ContentType.StartsWith("application/json"))
        //     {
        //         resultExecutedContext.HttpContext.Response.ContentType = "application/ld+json";
        //     }
        // }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (model.BaseUrl == null)
            {
                // Initialise the model based on the DISPLAY url of a request
                var uri = new Uri(context.HttpContext.Request.GetDisplayUrl());
                
                model.Init(uri.Scheme + "://" + uri.Authority);
            }
            base.OnActionExecuting(context);
        }

        public override void OnActionExecuted(ActionExecutedContext actionExecutedContext)
        {
            var headers = actionExecutedContext.HttpContext.Response.Headers;
            AddIfMissing(headers, "Link", "<" + model.BaseUrl + "/vocab#>; rel=\"http://www.w3.org/ns/hydra/core#apiDocumentation\"");
            AddIfMissing(headers, "Access-Control-Allow-Origin", "*");
            AddIfMissing(headers, "Access-Control-Expose-Headers", "Link");
            if (actionExecutedContext.HttpContext.Response.ContentType == null) // why is this always true?
            {
                return;
            }
            if (actionExecutedContext.HttpContext.Response.ContentType.StartsWith("application/json"))
            {
                actionExecutedContext.HttpContext.Response.ContentType = "application/ld+json";
            }
        }

        private void AddIfMissing(IHeaderDictionary headers, string header, string value)
        {
            if (!headers.ContainsKey(header))
            {
                headers.Add(header, value);
            }
        }
    }
}