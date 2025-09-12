using System;
using Serilog.Context;

namespace DLCS.Web.Logging;

public static class LogContextHelpers
{
    /// <summary>
    /// Manually add a "CorrelationId" property to log context to track requests.
    /// Note that this automatically happens for HTTP requests via the
    /// <see cref="DLCS.Web.Logging.CorrelationIdHeaderEnricher"/> but non-http request won't have this.
    /// </summary>
    /// <param name="correlationId">Unique identifier for request</param>
    public static IDisposable SetCorrelationId(string correlationId) =>
        LogContext.PushProperty("CorrelationId", correlationId, false);
}
