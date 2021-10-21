namespace Orchestrator.Features.PDF
{
    public enum PdfStatus
    {
        /// <summary>
        /// Default status
        /// </summary>
        Unknown,
        
        /// <summary>
        /// PDF is available to view, either having been freshly created or streamed
        /// </summary>
        Available,
        
        /// <summary>
        /// PDF is in the process of being created.
        /// </summary>
        InProcess,
        
        /// <summary>
        /// PDF cannot be found.
        /// </summary>
        NotFound,
        
        /// <summary>
        /// There was an error in handling the PDF request.
        /// </summary>
        Error
    }
}