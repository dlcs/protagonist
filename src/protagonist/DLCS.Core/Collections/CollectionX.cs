using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DLCS.Core.Collections;

public static class CollectionX
{
    /// <summary>
    /// Check if IEnumerable is null or empty
    /// </summary>
    /// <returns>true if null or empty, else false</returns>
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? collection) 
        => collection == null || !collection.Any();
    
    /// <summary>
    /// Check if IList is null or empty
    /// </summary>
    /// <returns>true if null or empty, else false</returns>
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IList<T>? collection)
        => collection == null || collection.Count == 0;

    /// <summary>
    /// Return a List{T} containing single item.
    /// </summary>
    /// <param name="item">Item to add to list</param>
    /// <typeparam name="T">Type of item</typeparam>
    /// <returns>List of one item</returns>
    public static List<T> AsList<T>(this T item)
        => new() { item };

    /// <summary>
    /// Return a List{TList} containing single item of derived type
    /// </summary>
    /// <param name="item"></param>
    /// <typeparam name="TList">Type of returned list</typeparam>
    /// <returns>List containing single item</returns>
    public static List<TList> AsListOf<TList>(this object item) 
        where TList : class
    {
        if (item is not TList list)
        {
            throw new InvalidCastException($"Cannot cast {item.GetType().Name} to {typeof(TList).Name}");
        }

        return new() { list };
    }
    
    /// <summary>
    /// Pick a single random item from list
    /// </summary>
    public static T PickRandom<T>(this IEnumerable<T> source) 
        => source.PickRandom(1).Single();

    /// <summary>
    /// Pick a random number of items from list
    /// </summary>
    public static IEnumerable<T> PickRandom<T>(this IEnumerable<T> source, int count) 
        => source.Shuffle().Take(count);

    /// <summary>
    /// Randomly reorder given list
    /// </summary>
    public static IOrderedEnumerable<T> Shuffle<T>(this IEnumerable<T> source) 
        => source.OrderBy(_ => Guid.NewGuid());

    /// <summary>
    /// Get a list of all duplicates in specified list
    /// </summary>
    /// <remarks>This will use the default equality comparer for type</remarks>
    public static IEnumerable<T> GetDuplicates<T>(this IEnumerable<T> source)
        => source
            .GroupBy(m => m)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
}