using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using DLCS.Core.Guard;

namespace DLCS.Core.Enum;

/// <summary>
/// A collection of extension methods for dealing with enums.
/// </summary>
public static class EnumX
{
    /// <summary>
    /// Find enum value for specified string.
    /// Matches in order of precedence: Exact value -> DescriptionAttribute. 
    /// </summary>
    /// <param name="description">String to find enum for.</param>
    /// <param name="defaultIfNotFound">
    /// Whether to return default(T) if value not found.
    /// If false throws exception of not found.
    /// </param>
    /// <typeparam name="T">Type of enum.</typeparam>
    /// <returns>Matching enum value, if found.</returns>
    public static T? GetEnumFromString<T>(this string description, bool defaultIfNotFound = true)
        where T : System.Enum
    {
        description.ThrowIfNullOrWhiteSpace(nameof(description));
        var memberInfos = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in memberInfos)
        {
            if (field.Name == description)
            {
                return (T?)field.GetRawConstantValue();
            }

            if (field.GetCustomAttribute<DescriptionAttribute>()?.Description == description)
            {
                return (T?)field.GetRawConstantValue();
            }
        }

        return defaultIfNotFound
            ? default(T)
            : throw new ArgumentException($"Matching enum for '{description}' not found.", nameof(description));
    }

    /// <summary>
    /// Get Description value for enum. Will use <see cref="DescriptionAttribute"/> if found, or fall back to value.ToString().
    /// </summary>
    /// <param name="enumValue">Value to get description for.</param>
    /// <typeparam name="T">Type of enum.</typeparam>
    /// <returns>String description for enum value.</returns>
    public static string GetDescription<T>(this T enumValue)
        where T : System.Enum
    {
        var memberInfo = typeof(T).GetMember(enumValue.ToString()).Single();
        var desc = memberInfo.GetCustomAttribute<DescriptionAttribute>();
        return desc == null ? enumValue.ToString() : desc.Description;
    }

    /// <summary>
    /// Returns true if the input string can be matched to any of a specified enum's values.
    /// </summary>
    /// <param name="description">String to try and match to an enum.</param>
    /// <typeparam name="T">Type of enum.</typeparam>
    /// <returns>Boolean indicating if a match was found.</returns>
    public static bool IsValidEnumValue<T>(this string description)
        where T : System.Enum
    {
        if (string.IsNullOrWhiteSpace(description)) return false;
        
        var memberInfos = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static);
        var isValid = memberInfos.Any(
            field => field.Name == description ||
            field.GetCustomAttribute<DescriptionAttribute>()?.Description == description);
        
        return isValid;
    }
    
    /// <summary>
    /// Converts a list of strings into an enum with flags representation
    /// </summary>
    /// <param name="enumFlagsAsList">The list to convert</param>
    /// <typeparam name="T">The type of the enum to convert</typeparam>
    /// <returns>An enum</returns>
    public static T ToEnumFlags<T>(this List<string> enumFlagsAsList) where T : struct, System.Enum
    {
        T flags;
        var commaSeparatedFlags = string.Join(",", enumFlagsAsList.Select(s => s.GetEnumFromString<T>()));
        System.Enum.TryParse(commaSeparatedFlags, out flags);
        return flags;
    }
}