using System;
using DLCS.Web.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Http;

namespace DLCS.Web.Configuration;

internal class HeaderPropagationMessageHandlerBuilderFilter(IHttpContextAccessor contextAccessor)
    : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            builder.AdditionalHandlers.Add(new PropagateHeaderHandler(contextAccessor));
            next(builder);
        };
    }
}
