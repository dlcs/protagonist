using System;
using Stubbery;

namespace Test.Helpers;

public static class ApiStubX
{
    /// <summary>
    /// Safely start provided <see cref="ApiStub"/>, swallowing any exceptions due to already being in 'started' state
    /// </summary>
    /// <remarks>Opened https://github.com/markvincze/Stubbery/issues/37 to see if there is better way</remarks>
    public static void SafeStart(this ApiStub apiStub)
    {
        try
        {
            apiStub.Start();
        }
        catch (InvalidOperationException ex) when (ex.Message == "The api stub is already started.")
        {
            // swallow, it's not possible to check state prior to calling .Start()
        }
    }
}