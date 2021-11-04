using Hydra.Model;

namespace API.Converters
{
    public static class ErrorX
    {
        public static Error ToHydra(this APIException exception)
        {
            return new()
            {
                IncludeContext = true,
                Title = exception.Label,
                Description = exception.Message,
                StatusCode = exception.StatusCode
            };
        }
        
    }
}