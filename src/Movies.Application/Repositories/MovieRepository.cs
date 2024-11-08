using System;
using System.Security.Cryptography.X509Certificates;
using Dapper;
using Movies.Application.Database;
using Movies.Application.Models;

namespace Movies.Application.Repositories;

public class MovieRepository : IMovieRepository
{

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public MovieRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<bool> CreateAsync(Movie movie, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var result = await connection.ExecuteAsync(new CommandDefinition(
            commandText: """
                INSERT INTO movies (id, slug, title, yearofrelease)
                VALUES (@Id, @Slug, @Title, @YearOfRelease);
            """,
            parameters: new { movie.Id, movie.Slug, movie.Title, movie.YearOfRelease },
            transaction: transaction,
            cancellationToken: cancellationToken
        ));

        if (result > 0)
        {
            foreach (var genre in movie.Genres)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    commandText: """
                        INSERT INTO genres (movieId, name)
                        VALUES (@MovieId, @Name);
                    """,
                    parameters: new { MovieId = movie.Id, Name = genre },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                ));
            }
        }

        transaction.Commit();

        return result > 0;
    }

    public async Task<Movie?> GetByIdAsync(Guid id, Guid? userId = default, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var movie = await connection.QuerySingleOrDefaultAsync<Movie>(new CommandDefinition(
            commandText: """
                SELECT m.id, m.slug, m.title, m.yearofrelease,
                    round(avg(r.rating), 1) as rating,
                    myr.rating as userrating
                FROM movies m
                left join ratings r on m.id = r.movieid
                left join ratings myr on m.id = myr.movieid 
                    and myr.userid = @UserId
                WHERE id = @Id
                group by id, userrating;
            """,
            parameters: new { Id = id, UserId = userId },
            cancellationToken: cancellationToken
        ));

        if (movie is null)
        {
            return null;
        }

        var genres = await connection.QueryAsync<string>(new CommandDefinition(
            commandText: """
                SELECT name
                FROM genres
                WHERE movieId = @Id;
            """,
            parameters: new { Id = id },
            cancellationToken: cancellationToken
        ));

        foreach (var genre in genres)
        {
            movie.Genres.Add(genre);
        }

        return movie;
    }

    public async Task<Movie?> GetBySlugAsync(string slug, Guid? userId = default, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        var movie = await connection.QuerySingleOrDefaultAsync<Movie>(new CommandDefinition(
            commandText: """
                SELECT m.id, m.slug, m.title, m.yearofrelease,
                    round(avg(r.rating), 1) as rating,
                    myr.rating as userrating
                FROM movies m
                left join ratings r on m.id = r.movieid
                left join ratings myr on m.id = myr.movieid 
                    and myr.userid = @UserId
                WHERE slug = @Slug
                group by id, userrating;
            """,
            parameters: new { Slug = slug, UserId = userId },
            cancellationToken: cancellationToken
        ));

        if (movie is null)
        {
            return null;
        }

        var genres = await connection.QueryAsync<string>(new CommandDefinition(
            commandText: """
                SELECT name
                FROM genres
                WHERE movieId = @Id;
            """,
            parameters: new { movie.Id },
            cancellationToken: cancellationToken
        ));

        foreach (var genre in genres)
        {
            movie.Genres.Add(genre);
        }

        return movie;
    }

    public async Task<IEnumerable<Movie>> GetAllAsync(GetAllMoviesOptions options, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);

        var orderClause = string.Empty;
        if (options.SortField is not null)
        {
            orderClause = $"""
            , m.{options.SortField} 
            order by m.{options.SortField} {(options.SortOrder == SortOrder.Descending ? "desc" : "asc")}
            """;
        }

        var movies = await connection.QueryAsync(new CommandDefinition(
            commandText: $"""
                SELECT m.id, m.slug, m.title, m.yearofrelease, 
                string_agg(distinct g.name, ',') as genres,
                round(avg(r.rating), 1) as rating,
                myr.rating as userrating
                FROM movies m 
                LEFT JOIN genres g ON m.id = g.movieid
                left join ratings r on m.id = r.movieid
                left join ratings myr on m.id = myr.movieid 
                    and myr.userid = @UserId
                where (@Title is null or m.title like ('%' || @Title || '%'))
                and (@YearOfRelease is null or m.yearofrelease = @YearOfRelease)
                GROUP BY id, userrating {orderClause}
                limit @PageSize 
                offset @PageOffSet;
            """,
            parameters: new
            {
                UserId = options.UserId,
                Title = options.Title,
                YearOfRelease = options.YearOfRelease,
                PageSize = options.PageSize,
                PageOffSet = (options.Page - 1) * options.PageSize
            },
            cancellationToken: cancellationToken
        ));

        return movies.Select(movie => new Movie
        {
            Id = movie.id,
            Title = movie.title,
            YearOfRelease = movie.yearofrelease,
            Rating = (float?)movie.rating,
            UserRating = (int?)movie.userrating,
            Genres = Enumerable.ToList(movie.genres.Split(","))
        });
    }

    public async Task<bool> UpdateAsync(Movie movie, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            commandText: """
                DELETE FROM genres
                WHERE movieId = @Id;
            """,
            parameters: new { movie.Id },
            transaction: transaction,
            cancellationToken: cancellationToken
        ));

        foreach (var genre in movie.Genres)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                commandText: """
                    INSERT INTO genres (movieId, name)
                    VALUES (@MovieId, @Name);
                """,
                parameters: new { MovieId = movie.Id, Name = genre },
                transaction: transaction,
                cancellationToken: cancellationToken
            ));
        }

        var result = await connection.ExecuteAsync(new CommandDefinition(
            commandText: """
                UPDATE movies
                SET slug = @Slug, title = @Title, yearofrelease = @YearOfRelease
                WHERE id = @Id;
            """,
            parameters: movie,
            transaction: transaction,
            cancellationToken: cancellationToken
        ));

        transaction.Commit();

        return result > 0;
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            commandText: """
                DELETE FROM genres
                WHERE movieId = @Id;
            """,
            parameters: new { Id = id },
            transaction: transaction,
            cancellationToken: cancellationToken
        ));

        var result = await connection.ExecuteAsync(new CommandDefinition(
            commandText: """
                DELETE FROM movies
                WHERE id = @Id;
            """,
            parameters: new { Id = id },
            transaction: transaction,
            cancellationToken: cancellationToken
        ));

        transaction.Commit();
        return result > 0;

    }

    public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            commandText: """
                SELECT EXISTS (
                    SELECT 1
                    FROM movies
                    WHERE id = @Id
                );
            """,
            parameters: new { Id = id },
            cancellationToken: cancellationToken
        ));
    }

    public async Task<int> GetCountAsync(string? title, int? yearOfRelease, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            commandText: """
                SELECT count(id)
                FROM movies
                WHERE (@Title is null or title like ('%' || @Title || '%'))
                and (@YearOfRelease is null or yearofrelease = @YearOfRelease);
            """,
            parameters: new { Title = title, YearOfRelease = yearOfRelease },
            cancellationToken: cancellationToken
        ));

    }
}
