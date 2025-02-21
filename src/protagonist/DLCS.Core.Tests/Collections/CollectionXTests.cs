using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;

namespace DLCS.Core.Tests.Collections;

public class CollectionXTests
{
    [Fact]
    public void IsNullOrEmpty_True_IfNull()
    {
        IEnumerable<int> coll = null;

        coll.IsNullOrEmpty().Should().BeTrue();
    }
    
    [Fact]
    public void IsNullOrEmpty_True_IfEmpty()
    {
        var coll = Enumerable.Empty<int>();

        coll.IsNullOrEmpty().Should().BeTrue();
    }
    
    [Fact]
    public void IsNullOrEmpty_False_IfHasValues()
    {
        IEnumerable<int> coll = new [] {2};

        coll.IsNullOrEmpty().Should().BeFalse();
    }
    
    [Fact]
    public void IsNullOrEmpty_List_True_IfNull()
    {
        List<int> coll = null;

        coll.IsNullOrEmpty().Should().BeTrue();
    }
    
    [Fact]
    public void IsNullOrEmpty_List_True_IfEmpty()
    {
        var coll = new List<int>();

        coll.IsNullOrEmpty().Should().BeTrue();
    }
    
    [Fact]
    public void IsNullOrEmpty_List_False_IfHasValues()
    {
        var coll = new List<int> {2};

        coll.IsNullOrEmpty().Should().BeFalse();
    }
    
    [Fact]
    public void IsEmpty_List_False_IfNull()
    {
        List<int> coll = null;

        coll.IsEmpty().Should().BeFalse();
    }
    
    [Fact]
    public void IsEmpty_List_True_IfEmpty()
    {
        var coll = new List<int>();

        coll.IsEmpty().Should().BeTrue();
    }
    
    [Fact]
    public void IsEmpty_List_False_IfHasValues()
    {
        var coll = new List<int> {2};

        coll.IsEmpty().Should().BeFalse();
    }

    [Fact]
    public void AsList_ReturnsExpected()
    {
        var item = DateTime.Now;

        var list = item.AsList();

        list.Should().ContainSingle(i => i == item);
    }
    
    [Fact]
    public void AsListOf_ThrowsIfCastInvalid()
    {
        var item = DateTime.Now;

        Action action = () => item.AsListOf<Exception>();

        action.Should().Throw<InvalidCastException>();
    }
    
    [Fact]
    public void AsListOf_ReturnsExpected()
    {
        var item = DateTime.Now;

        var list = item.AsListOf<object>();

        list.Should().ContainSingle(i => (DateTime)i == item);
    }
    
    [Fact]
    public void GetDuplicates_ReturnsEmptyList_IfNoDuplicates()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var duplicates = list.GetDuplicates();

        duplicates.Should().BeEmpty();
    }
    
    [Fact]
    public void GetDuplicates_ReturnsDuplicates()
    {
        var list = new List<int> { 1, 2, 3, 4, 5, 4, 3 };
        var expected = new List<int> { 3, 4 };
        var duplicates = list.GetDuplicates();

        duplicates.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void ContainsOnly_False_IfNull()
    {
        int[] coll = null;

        coll.ContainsOnly(123).Should().BeFalse();
    }
    
    [Fact]
    public void ContainsOnly_False_IfEmpty()
    {
        int[] coll = Array.Empty<int>();

        coll.ContainsOnly(123).Should().BeFalse();
    }
    
    [Fact]
    public void ContainsOnly_False_IfDoesNotContain()
    {
        int[] coll = { 757 };

        coll.ContainsOnly(123).Should().BeFalse();
    }
    
    [Fact]
    public void ContainsOnly_False_IfContainsMultiple()
    {
        int[] coll = { 123, 123 };

        coll.ContainsOnly(123).Should().BeFalse();
    }
    
    [Fact]
    public void ContainsOnly_True_IfOnlyContainsSpecified()
    {
        int[] coll = { 123 };

        coll.ContainsOnly(123).Should().BeTrue();
    }
    
    [Fact]
    public void AsArray_ReturnsExpected()
    {
        var item = DateTime.Now;

        var list = item.AsArray();

        list.Should().ContainSingle(i => i == item);
    }
}