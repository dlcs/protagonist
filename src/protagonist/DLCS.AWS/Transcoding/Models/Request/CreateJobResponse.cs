using System.Net;

namespace DLCS.AWS.Transcoding.Models.Request;

/// <summary>
/// Object representing status of creating a transcode job 
/// </summary>
/// <param name="JobId">Unique identifier for job</param>
/// <param name="HttpStatusCode">HttpStatus code of create job request</param>
public record CreateJobResponse(string JobId, HttpStatusCode HttpStatusCode)
{
    /// <summary>
    /// True if job was successfully created, else false
    /// </summary>
    public bool Success => (int)HttpStatusCode is >= 200 and < 300;
}
