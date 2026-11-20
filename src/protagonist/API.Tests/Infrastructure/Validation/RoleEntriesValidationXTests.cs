using System.Linq;
using API.Infrastructure.Validation;
using FluentValidation;
using FluentValidation.TestHelper;

namespace API.Tests.Infrastructure.Validation;

public class RoleEntriesValidationXTests
{
    private class Model
    {
        public string[]? Roles { get; init; }
    }

    private class Sut : AbstractValidator<Model>
    {
        public Sut() => RuleFor(m => m.Roles).ValidRoleEntries();
    }

    private static TestValidationResult<Model> Validate(string[]? roles) => new Sut().TestValidate(new Model { Roles = roles });

    [Fact]
    public void Null_Valid() => Validate(null).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void EmptyArray_Valid() => Validate([]).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("a")]
    [InlineData("a|b")]
    [InlineData("a+b|c")]
    [InlineData("a+b|a+c")]
    [InlineData("https://dlcs.io/roles/ip:x+https://dlcs.io/roles/b")]
    public void ValidEntries(string roles) => Validate(roles.Split('|')).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("", "Role entry '' contains an empty role")]
    [InlineData("a||b", "Role entry '' contains an empty role")]
    [InlineData("a+", "Role entry 'a+' contains an empty role")]
    [InlineData("+a", "Role entry '+a' contains an empty role")]
    [InlineData("a++b", "Role entry 'a++b' contains an empty role")]
    [InlineData("a b", "Role entry 'a b' contains whitespace or ','")]
    [InlineData(" a", "Role entry ' a' contains whitespace or ','")]
    [InlineData("a\tb", "Role entry 'a\tb' contains whitespace or ','")]
    [InlineData("a,b", "Role entry 'a,b' contains whitespace or ','")]
    [InlineData("a|a", "Role entry 'a' is redundant as it contains all roles of 'a'")]
    [InlineData("a|a+b", "Role entry 'a+b' is redundant as it contains all roles of 'a'")]
    [InlineData("a+b|a", "Role entry 'a+b' is redundant as it contains all roles of 'a'")]
    [InlineData("b+a|a+b+c", "Role entry 'a+b+c' is redundant as it contains all roles of 'b+a'")]
    [InlineData("a+b|b+a", "Role entry 'b+a' is redundant as it contains all roles of 'a+b'")]
    public void InvalidEntries_SingleError(string roles, string message)
        => Validate(roles.Split('|')).ShouldHaveValidationErrorFor(m => m.Roles)
            .WithErrorMessage(message)
            .Should().ContainSingle();

    [Fact]
    public void NullEntry_IsEmptyRole()
        => Validate(["a", null!]).ShouldHaveValidationErrorFor(m => m.Roles)
            .WithErrorMessage("Role entry '' contains an empty role");

    [Fact]
    public void InvalidEntry_NotUsedForRedundancyCheck()
        => Validate(["a b", "a b+c"]).Errors.Should().HaveCount(2)
            .And.OnlyContain(e => e.ErrorMessage.Contains("whitespace"));

    [Fact]
    public void ReportsEachRedundantEntry()
        => Validate(["a", "a+b", "a+c"]).Errors.Select(e => e.ErrorMessage).Should().BeEquivalentTo(
            "Role entry 'a+b' is redundant as it contains all roles of 'a'",
            "Role entry 'a+c' is redundant as it contains all roles of 'a'");
}
