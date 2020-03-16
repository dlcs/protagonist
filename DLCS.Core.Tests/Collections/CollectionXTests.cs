using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Collections
{
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
            var coll = new [] {2};

            coll.IsNullOrEmpty().Should().BeFalse();
        }
    }
}