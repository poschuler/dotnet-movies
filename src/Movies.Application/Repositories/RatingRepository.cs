using System;
using Dapper;
using Movies.Application.Database;
using Movies.Application.Models;

namespace Movies.Application.Services;

public class RatingRepository : IRatingRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public RatingRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> DeleteRatingAsync(Guid movieId, Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition(
            commandText: @"
                DELETE FROM ratings
                WHERE movieId = @MovieId
                  AND userId = @UserId;
            ",
            parameters: new { MovieId = movieId, UserId = userId },
            cancellationToken: cancellationToken
        ));

        return result > 0;

    }

    public async Task<float?> GetRatingAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<float?>(new CommandDefinition(
            commandText: @"
                SELECT roung(avg(r.rating),1) FROM ratings r
                WHERE movieId = @MovieId;
            ",
            parameters: new { MovieId = movieId },
            cancellationToken: cancellationToken
        ));
    }

    public async Task<(float? Rating, int? UserRating)> GetRatingAsync(Guid movieId, Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<(float?, int?)>(new CommandDefinition(
            commandText: @"
                SELECT round(avg(r.rating),1),
                    (select rating
                    FROM ratings
                    WHERE movieId = @MovieId 
                      AND userId = @UserId
                    limit 1)
                FROM ratings r
                WHERE movieId = @MovieId;
            ",
            parameters: new { MovieId = movieId },
            cancellationToken: cancellationToken
        ));
    }

    public async Task<IEnumerable<MovieRating>> GetRatingsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryAsync<MovieRating>(new CommandDefinition(
            commandText: @"
                SELECT r.rating, r.movieid, m.slug
                FROM ratings r
                JOIN movies m on m.id = r.movieId
                WHERE r.userId = @UserId;
            ",
            parameters: new { UserId = userId },
            cancellationToken: cancellationToken
        ));
    }

    public async Task<bool> RateMovieAsync(Guid movieId, int rating, Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var result = await connection.ExecuteAsync(new CommandDefinition(
            commandText: @"
                INSERT INTO ratings (userId, movieId, rating)
                VALUES (@UserId, @MovieId, @Rating)
                ON CONFLICT (userId, movieId) DO UPDATE
                SET rating = @Rating;
            ",
            parameters: new { UserId = userId, MovieId = movieId, Rating = rating },
            cancellationToken: cancellationToken
        ));

        return result > 0;
    }
}
