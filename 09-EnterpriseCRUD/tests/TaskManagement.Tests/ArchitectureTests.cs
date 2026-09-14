using System.Reflection;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Tests;

/// <summary>
/// The dependency rule, enforced by the build rather than by review.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Project).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;

    private static string[] References(Assembly assembly)
        => assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Fact]
    public void Domain_references_nothing_but_the_base_class_library()
    {
        References(Domain)
            .Where(name => !name.StartsWith("System") && name != "netstandard")
            .Should().BeEmpty("the Domain layer must depend on no framework at all");
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_the_WebApi()
    {
        References(Application).Should()
            .NotContain(n => n.Contains("Infrastructure"))
            .And.NotContain(n => n.Contains("WebApi"));
    }

    [Fact]
    public void Application_does_not_reference_EntityFramework_or_AspNetCore()
    {
        // If this fails, the use cases have been pinned to one database or one
        // web framework, and the ports were pointless.
        References(Application).Should()
            .NotContain(n => n.Contains("EntityFrameworkCore"))
            .And.NotContain(n => n.Contains("AspNetCore"));
    }

    [Fact]
    public void Domain_entities_expose_no_public_setters()
    {
        var offenders = Domain.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Domain.Common.BaseEntity).IsAssignableFrom(t))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.SetMethod is { IsPublic: true })
            // Audit fields are set by the DbContext interceptor, which needs
            // access. Everything else must go through a method.
            .Where(p => p.DeclaringType != typeof(Domain.Common.AuditableEntity))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToArray();

        offenders.Should().BeEmpty();
    }
}
