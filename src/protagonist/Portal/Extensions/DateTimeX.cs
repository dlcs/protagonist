using System;

namespace Portal.Extensions;

public static class DateTimeX
{
    private const string DefaultTimeFormat = "yyyy-MM-dd hh:mm:ss";
    
    /// <summary>
    /// Returns a DateTime object as a string in a preset format
    /// </summary>
    public static string GetDefaultTime(this DateTime dateTime)
    {
        return dateTime.ToString(DefaultTimeFormat);
    }
}