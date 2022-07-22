using FakeItEasy;
using Microsoft.Extensions.Options;

namespace Test.Helpers.Settings;

public class OptionsHelpers
{
    /// <summary>
    /// Creates an <see cref="IOptionsMonitor{TOptions}"/> that returns provided object for CurrentValue
    /// </summary>
    /// <param name="expectedResult">The CurrentValue of OptionsMonitor</param>
    /// <typeparam name="T">Type of object</typeparam>
    /// <returns>Fake <see cref="IOptionsMonitor{TOptions}"/> with CurrentValue setup.</returns>
    public static IOptionsMonitor<T> GetOptionsMonitor<T>(T expectedResult)
    {
        var optionsMonitor = A.Fake<IOptionsMonitor<T>>();
        A.CallTo(() => optionsMonitor.CurrentValue).Returns(expectedResult);
        return optionsMonitor;
    }
}