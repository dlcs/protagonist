using API.Features.Assets.Query;

namespace API.Tests.Features.Assets.Query;

public class AssetIncludeTests
{
    [Fact]
    public void Ctor_IncludesNull_IfInputNull()
    {
        var sut = new AssetInclude(null);
        sut.Include.Should().BeNull();
    }
    
    [Fact]
    public void Ctor_IncludesNull_IfInputEmpty()
    {
        var sut = new AssetInclude([]);
        sut.Include.Should().BeNull();
    }

    [Fact]
    public void Ctor_AllValidIncludes_SetsAll()
    {
        var sut = new AssetInclude([IncludeFields.Adjuncts]);
        sut.Include.Should().BeEquivalentTo(IncludeFields.Adjuncts);
    }
    
    [Fact]
    public void Ctor_AllValidIncludes_SetsAll_Deduplicates()
    {
        var sut = new AssetInclude([IncludeFields.Adjuncts, IncludeFields.Adjuncts]);
        sut.Include.Should().BeEquivalentTo(IncludeFields.Adjuncts);
    }

    [Fact]
    public void Ctor_IncludesNull_IfAllInvalid()
    {
        var sut = new AssetInclude(["foo", "bar"]);
        sut.Include.Should().BeNull();
    }

    [Fact]
    public void Ctor_MixedIncludes_OnlyKeepsAllowed()
    {
        var sut = new AssetInclude([IncludeFields.Adjuncts, "foo"]);
        sut.Include.Should().BeEquivalentTo(IncludeFields.Adjuncts);
    }

    [Fact]
    public void Ctor_CaseInsensitive_AllowedFieldKept()
    {
        var sut = new AssetInclude(["ADJUNCTS"]);
        sut.Include.Should().ContainSingle();
    }

    [Fact]
    public void IncludesField_ReturnsFalse_WhenFieldNotIncluded()
    {
        var sut = new AssetInclude([]);
        sut.IncludesField(IncludeFields.Adjuncts).Should().BeFalse();
    }

    [Fact]
    public void IncludesField_ReturnsTrue_WhenFieldIncluded()
    {
        var sut = new AssetInclude([IncludeFields.Adjuncts]);
        sut.IncludesField(IncludeFields.Adjuncts).Should().BeTrue();
    }
}
