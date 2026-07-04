using NetArchTest.Rules;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// Reglas de dependencia de Clean Architecture. Si alguna falla, la arquitectura
/// se ha roto: las capas internas no pueden conocer a las externas.
/// </summary>
public class ArchitectureTests
{
    private const string Domain = "AgentPilot.Domain";
    private const string Application = "AgentPilot.Application";
    private const string Infrastructure = "AgentPilot.Infrastructure";
    private const string Api = "AgentPilot.Api";

    [Fact]
    public void Domain_NoDependeDeNingunaOtraCapa()
    {
        var result = Types.InAssembly(typeof(Domain.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Report(result));
    }

    [Fact]
    public void Application_NoDependeDeInfrastructureNiApi()
    {
        var result = Types.InAssembly(typeof(Application.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Report(result));
    }

    private static string Report(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Tipos que violan la regla: " + string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>());
}
