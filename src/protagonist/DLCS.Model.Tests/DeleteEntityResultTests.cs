using System;
using DLCS.Core;
using DLCS.Model.Assets;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests;

public class DeleteEntityResultTests
{
    [Fact]
    public void Ctor_Throws_IfDeletedStatus_WithNullEntity()
    {
        // Act
        Action action = () => new DeleteEntityResult<Asset>(DeleteResult.Deleted);

        // Assert
        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Deleted Asset entity must be provided if status is 'Deleted'");
    }
    
    [Theory]
    [InlineData(DeleteResult.Conflict)]
    [InlineData(DeleteResult.Error)]
    [InlineData(DeleteResult.NotFound)]
    public void Ctor_AllowsNullEntity_ForAllOtherStatus(DeleteResult result)
    {
        // Act
        var deleteResult = new DeleteEntityResult<Asset>(result);

        // Assert
        deleteResult.Result.Should().Be(result);
        deleteResult.DeletedEntity.Should().BeNull();
    }
}