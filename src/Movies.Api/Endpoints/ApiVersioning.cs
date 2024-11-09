using System;
using Asp.Versioning.Builder;
using Asp.Versioning.Conventions;

namespace Movies.Api.Endpoints;

public static class ApiVersioning
{
    public static ApiVersionSet VersionSet { get; private set; }

    public static IEndpointRouteBuilder CreateApiVersioning(this IEndpointRouteBuilder app)
    {
        VersionSet = app.NewApiVersionSet()
            .HasApiVersion(1.0)
            .HasApiVersion(2.0)
            .ReportApiVersions()
            .Build();

        return app;
    }

}
