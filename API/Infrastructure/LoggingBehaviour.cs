using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Infrastructure
{
    /// <summary>
    /// Mediatr Pipeline Behaviour that logs incoming requests
    /// </summary>
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            this.logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken,
            RequestHandlerDelegate<TResponse> next)
        {
            // This could be cleverer, currently will just log ToString()
            logger.LogDebug($"Handling '{typeof(TRequest).Name}' request. {request}");

            Stopwatch sw = Stopwatch.StartNew();
            var response = await next();

            sw.Stop();
            logger.LogDebug($"Handled '{typeof(TResponse).Name}' in {sw.ElapsedMilliseconds}ms. {response}");

            return response;
        }
    }
}