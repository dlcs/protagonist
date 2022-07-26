using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using FluentAssertions;
using Xunit;

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
}