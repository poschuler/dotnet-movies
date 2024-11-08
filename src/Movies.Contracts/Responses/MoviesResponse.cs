using System;

namespace Movies.Contracts.Responses;

public class MoviesResponse : PagedResponse<MovieResponse>
{
    public IEnumerable<MovieResponse> Items { get; init; } = Enumerable.Empty<MovieResponse>();

}
