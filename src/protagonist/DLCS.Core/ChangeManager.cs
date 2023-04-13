using System;
using System.Reflection;

namespace DLCS.Core;

/// <summary>
/// Contains methods for applying changes to existing objects
/// </summary>
public static class ChangeManager
{
    /// <summary>
    /// Replace nulls in toUpdate object with values from default object 
    /// </summary>
    /// <param name="toUpdate">Object to update</param>
    /// <param name="defaultObject">Object containing default values.</param>
    /// <typeparam name="T">Type of object</typeparam>
    public static void DefaultNullProperties<T>(this T toUpdate, T defaultObject)
        where T : class
    {
        var properties = toUpdate.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach(var prop in properties)
        {
            if (prop.GetValue(toUpdate) == null)
            {
                prop.SetValue(toUpdate, prop.GetValue(defaultObject));
            }
        }
    }
    
    /// <summary>
    /// Update toUpdate with any non-null values from candidateChanges that differ to existing values.
    /// </summary>
    /// <param name="toUpdate">Object to update</param>
    /// <param name="candidateChanges">Object containing candidate changes, any not set are null</param>
    /// <typeparam name="T">Type of object</typeparam>
    /// <returns>Number of properties update</returns>
    /// <remarks>
    /// Example use case for this is when toUpdate has been loaded from DB and candidateChanges has been passed in via
    /// an HTTP request. Any non-null values in candidateChanges will have been set in request and should override those
    /// already set on object
    /// </remarks>
    public static int ApplyChanges<T>(this T toUpdate, T candidateChanges)
        where T : class
    {
        var properties = toUpdate.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var changesApplied = 0;
        foreach (var prop in properties)
        {
            if (!prop.CanWrite) continue;
            
            // Null candidate value can be ignored
            // NOTE - this means we can't default back to NULL
            var candidateValue = prop.GetValue(candidateChanges);
            if (candidateValue == null) continue;
            
            var existingValue = prop.GetValue(toUpdate);

            var valuesAreEqual = prop.PropertyType.IsValueType
                ? existingValue?.Equals(candidateValue) ?? false
                : existingValue == candidateValue;
            
            if (!valuesAreEqual)
            {
                changesApplied++;
                prop.SetValue(toUpdate, candidateValue);
            }
        }

        return changesApplied;
    }
}