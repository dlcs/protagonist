using System;

namespace Portal.Extensions;

public static class DateTimeX
{
    private const string DefaultTimeFormat = "yyyy-MM-dd hh:mm:ss";
    private const string DateNotFoundMessage = "Date not found";
    
    /// <summary>
    /// Returns a DateTime object as a string in a preset format
    /// </summary>
    public static string GetDefaultTime(this DateTime dateTime)
    {
        return dateTime.ToString(DefaultTimeFormat);
    }
    
    /// <inheritdoc cref="GetDefaultTime(DateTime)"/>
    public static string GetDefaultTime(this DateTime? dateTime)
    {
        return dateTime.HasValue ? dateTime.Value.ToString(DefaultTimeFormat) : DateNotFoundMessage;
    }
}