using System.Threading.Tasks;
using DLCS.Core.Exceptions;
using DLCS.Web.Response;
using Microsoft.AspNetCore.Http;

namespace DLCS.Web.Middleware
{
    /// <summary>
    /// Middleware that uses <see cref="HttpException"/> to return formatted exception
    /// </summary>
    public class StatusCodeExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;

        public StatusCodeExceptionHandlerMiddleware(RequestDelegate next)
        {
            _next = next;
        }
 
        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (HttpException ex)
            {
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, HttpException exception)
            => new StatusCodeResponse(exception.StatusCode, exception.Message).WriteJsonResponse(context.Response);
    }
}