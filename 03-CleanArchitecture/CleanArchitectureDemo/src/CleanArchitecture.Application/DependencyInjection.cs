using CleanArchitecture.Application.UseCases.Projects;
using CleanArchitecture.Application.UseCases.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Application;

/// <summary>
/// Each layer registers its own services. The composition root (WebApi) calls
/// these without needing to know what is inside.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateProjectUseCase>();
        services.AddScoped<GetProjectsUseCase>();
        services.AddScoped<ArchiveProjectUseCase>();
        services.AddScoped<CreateTaskUseCase>();
        services.AddScoped<AssignTaskUseCase>();
        services.AddScoped<CompleteTaskUseCase>();
        services.AddScoped<SearchTasksUseCase>();
        return services;
    }
}
