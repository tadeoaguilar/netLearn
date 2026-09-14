using System.Reflection;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Tests;

/// <summary>
/// Architecture is only real if something enforces it. A convention that lives
/// in a README is a convention until the first deadline.
///
/// These tests read the assemblies' actual references, so the build fails the
/// moment a dependency points the wrong way.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Project).Assembly;
    private static readonly Assembly Application = typeof(Application.Abstractions.IClock).Assembly;

    private static string[] ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Fact]
    public void Domain_references_nothing_but_the_base_class_library()
    {
        var offenders = ReferencedNames(Domain)
            .Where(name => !name.StartsWith("System") && name != "netstandard")
            .ToArray();

        offenders.Should().BeEmpty(
            "the Domain layer must not depend on any framework, ORM or application assembly");
    }

    [Fact]
    public void Domain_does_not_know_about_EntityFramework()
    {
        ReferencedNames(Domain).Should().NotContain(n => n.Contains("EntityFrameworkCore"));
    }

    [Fact]
    public void Domain_does_not_know_about_AspNetCore()
    {
        ReferencedNames(Domain).Should().NotContain(n => n.Contains("AspNetCore"));
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure()
    {
        // If this ever fails, the dependency rule has been inverted: the use
        // cases would be pinned to one database rather than to an interface.
        ReferencedNames(Application).Should().NotContain(n => n.Contains("Infrastructure"));
    }

    [Fact]
    public void Application_does_not_reference_EntityFramework_or_AspNetCore()
    {
        ReferencedNames(Application).Should()
            .NotContain(n => n.Contains("EntityFrameworkCore"))
            .And.NotContain(n => n.Contains("AspNetCore"));
    }

    [Fact]
    public void Domain_entities_do_not_expose_public_setters()
    {
        // Public setters would let any caller put an entity into a state its
        // own methods forbid, which defeats every rule in DomainTests.
        var entityTypes = Domain.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Entity).IsAssignableFrom(t));

        var offenders = entityTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToArray();

        offenders.Should().BeEmpty();
    }
}
