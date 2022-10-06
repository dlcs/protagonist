
using System;

namespace DLCS.Model.PathElements;

/// <summary>
/// A customer in a path can be an integer id or the customer name, e.g.,
/// /thumbs/wellcome/2/
/// /thumbs/5/2
/// ...are equivalent if Customer 5 is called "wellcome"
/// </summary>
public class CustomerPathElement
{
    public CustomerPathElement(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }
    public string Name { get; }
}

/// <summary>
/// A customer path element object where only integer id is important.
/// </summary>
public class IdOnlyPathElement : CustomerPathElement
{
    public IdOnlyPathElement(int id) : base(id, "")
    {
    }
    
    public IdOnlyPathElement(int id, string name) : base(id, name)
    {
        throw new InvalidOperationException("Don't use this ctor if Name is required");
    }
}