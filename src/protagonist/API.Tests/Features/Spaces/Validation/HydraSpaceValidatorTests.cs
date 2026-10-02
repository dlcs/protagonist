using API.Features.Space.Validation;
using FluentValidation.TestHelper;
using SpaceModel = DLCS.HydraModel.Space;

namespace API.Tests.Features.Spaces.Validation;

public class HydraSpaceValidatorTests
{
    private readonly HydraSpaceValidator sut = new();

    [Fact]
    public void RequiresOnly_Name_OnCreate()
    {
        var space = new SpaceModel { Name = "my-test-space" };

        var result = sut.TestValidate(space, options => options.IncludeRuleSets("default", "create"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Requires_Name_OnCreate(string name)
    {
        var space = new SpaceModel { Name = name };

        var result = sut.TestValidate(space, options => options.IncludeRuleSets("default", "create"));

        result.ShouldHaveValidationErrorFor(s => s.Name)
            .WithErrorMessage("A space must have a name.");
    }

    [Fact]
    public void CannotSupply_Id_OnCreate()
    {
        var space = new SpaceModel { Name = "my-test-space", ModelId = 10 };

        var result = sut.TestValidate(space, options => options.IncludeRuleSets("default", "create"));

        result.ShouldHaveValidationErrorFor(s => s.ModelId)
            .WithErrorMessage("An id cannot be supplied when creating a space; the platform assigns it.");
    }

    [Fact]
    public async Task Rejects_Id_ThatDiffersFromRoute_OnUpdate()
    {
        var space = new SpaceModel { Name = "my-test-space", ModelId = 456 };

        var result = await ValidateUpdate(space, 1);

        result.ShouldHaveValidationErrorFor(s => s.ModelId)
            .WithErrorMessage("The id in the request body does not agree with the request URL.");
    }

    [Fact]
    public async Task Accepts_Id_ThatMatchesRoute_OnUpdate()
    {
        var space = new SpaceModel { Name = "my-test-space", ModelId = 1 };

        var result = await ValidateUpdate(space, 1);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task RequiresNothing_OnUpdate()
    {
        var space = new SpaceModel();

        var result = await ValidateUpdate(space, 1);

        result.ShouldNotHaveAnyValidationErrors();
    }

    private async Task<TestValidationResult<SpaceModel>> ValidateUpdate(SpaceModel space, int routeSpaceId)
        => new(await sut.ValidateUpdateAsync(space, routeSpaceId));
}
