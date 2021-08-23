using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DLCS.Mediatr.Behaviours
{
    /// <summary>
    /// Mediatr Pipeline Behaviour that logs requests with timings.
    /// Will use ToString() property to log details
    /// </summary>
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : ITimedRequest
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            this.logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken,
            RequestHandlerDelegate<TResponse> next)
        {
            var logLevel = request.LoggingLevel ?? LogLevel.Debug;

            // This could be cleverer, currently will just log ToString()
            logger.Log(logLevel, "Handling '{RequestType}' request. {Request}", typeof(TRequest).Name, request);

            Stopwatch sw = Stopwatch.StartNew();
            var response = await next();
            sw.Stop();

            logger.Log(logLevel, "Handled '{RequestType}' in {Elapsed}ms. {Request}", typeof(TRequest).Name,
                sw.ElapsedMilliseconds, request);

            return response;
        }
    }

    /// <summary>
    /// Marker interface for requests that should be timed.
    /// </summary>
    public interface ITimedRequest : IRequest
    {
        LogLevel? LoggingLevel { get; }
    }
}