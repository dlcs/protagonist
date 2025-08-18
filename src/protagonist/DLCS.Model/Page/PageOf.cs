using System.Collections.Generic;

namespace DLCS.Model.Page;

/// <summary>
/// Represents a page of entities, including paging details
/// </summary>
/// <typeparam name="T"></typeparam>
public class PageOf<T>
    where T : class
{
    public IReadOnlyCollection<T> Entities { get; set; }
    public int Page { get; set; }
    public int Total { get; set; }
    public int PageSize { get; set; }
}