using System;
using Microsoft.AspNetCore.OutputCaching;
using Movies.Api.Auth;
using Movies.Api.Mapping;
using Movies.Application.Services;
using Movies.Contracts.Requests;
using Movies.Contracts.Responses;

namespace Movies.Api.Endpoints.Movies;

public static class CreateMovieEndpoint
{
    public const string Name = "CreateMovie";

    public static IEndpointRouteBuilder MapCreateMovie(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiEndpoints.Movies.Create, async (
            CreateMovieRequest request,
            IMovieService movieService,
            IOutputCacheStore outputCacheStore,
            CancellationToken cancellationToken) =>
        {
            var movie = request.MapToMovie();

            await movieService.CreateAsync(movie, cancellationToken);

            var movieResponse = movie.MapToResponse();

            await outputCacheStore.EvictByTagAsync("movies", cancellationToken);

            return TypedResults.CreatedAtRoute(movieResponse, GetMovieEndpoint.Name, new { idOrSlug = movie.Id });
        })
        .WithName(Name)
        .Produces<MovieResponse>(StatusCodes.Status201Created)
        .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
        .RequireAuthorization(AuthConstants.TrustedMemberPolicyName);

        return app;

    }

}
