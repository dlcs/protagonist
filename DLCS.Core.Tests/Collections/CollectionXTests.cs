using System;
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

        [Fact]
        public void EnsureString_Adds_NewString()
        {
            var before = new string[] { };
            
            var ensured = StringArrays.EnsureString(before, "test");

            ensured.Should().Equal("test");
        }
        
        
        [Fact]
        public void EnsureString_Adds_AdditionalString()
        {
            var before = new[] { "already" };
            
            var ensured = StringArrays.EnsureString(before, "test");

            ensured.Should().Equal("already", "test");
        }
        
        
        [Fact]
        public void EnsureString_DoesNotAdd_AdditionalString()
        {
            var before = new[] { "already" };
            
            var ensured = StringArrays.EnsureString(before, "already");

            ensured.Should().Equal("already");
        }
        
        [Fact]
        public void RemoveString_RemovesString()
        {
            var before = new[] { "a", "b" };
            
            var ensured = StringArrays.RemoveString(before, "b");

            ensured.Should().Equal("a");
        }
        
        
        [Fact]
        public void RemoveString_RemovesString_InRightOrder()
        {
            var before = new[] { "a", "b", "c" };
            
            var ensured = StringArrays.RemoveString(before, "b");

            ensured.Should().Equal("a", "c");
        }
        
        [Fact]
        public void RemoveString_RemovesString_LeavesEmpty()
        {
            var before = new[] { "before" };
            
            var ensured = StringArrays.RemoveString(before, "before");

            ensured.Should().BeEmpty();
        }
        
        
        [Fact]
        public void RemoveString_DoesntRemove()
        {
            var before = new[] { "a", "b" };
            
            var ensured = StringArrays.RemoveString(before, "c");

            ensured.Should().Equal("a", "b");
        }
    }
}