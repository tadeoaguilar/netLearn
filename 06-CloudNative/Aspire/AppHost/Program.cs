// Follow EXERCISE.md Part A.1 to build this out step by step.

using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CatalogApi>("catalogapi");
builder.AddProject<Projects.Storefront>("storefront");

builder.Build().Run();
