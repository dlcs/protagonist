using System;
using DLCS.Web.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Http;

namespace DLCS.Web.Configuration;

internal class HeaderPropagationMessageHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
{
    private readonly IHttpContextAccessor contextAccessor;

    public HeaderPropagationMessageHandlerBuilderFilter(IHttpContextAccessor contextAccessor)
    {
        this.contextAccessor = contextAccessor;
    }

    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            builder.AdditionalHandlers.Add(new PropagateHeaderHandler(contextAccessor));
            next(builder);
        };
    }
}