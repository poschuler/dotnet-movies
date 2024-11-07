using System;
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

    public async Task<bool> CreateAsync(Movie movie)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        var result = await connection.ExecuteAsync(new CommandDefinition(
            commandText: """
                INSERT INTO movies (id, slug, title, yearofrelease)
                VALUES (@Id, @Slug, @Title, @YearOfRelease);
            """,
            parameters: new { movie.Id, movie.Slug, movie.Title, movie.YearOfRelease },
            transaction: transaction
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
                    transaction: transaction
                ));
            }
        }

        transaction.Commit();

        return result > 0;
    }

    public async Task<Movie?> GetByIdAsync(Guid id)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var movie = await connection.QuerySingleOrDefaultAsync<Movie>(new CommandDefinition(
            commandText: """
                SELECT id, slug, title, yearofrelease
                FROM movies
                WHERE id = @Id;
            """,
            parameters: new { Id = id }
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
            parameters: new { Id = id }
        ));

        foreach (var genre in genres)
        {
            movie.Genres.Add(genre);
        }

        return movie;
    }

    public async Task<Movie?> GetBySlugAsync(string slug)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var movie = await connection.QuerySingleOrDefaultAsync<Movie>(new CommandDefinition(
            commandText: """
                SELECT id, slug, title, yearofrelease
                FROM movies
                WHERE slug = @Slug;
            """,
            parameters: new { Slug = slug }
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
            parameters: new { movie.Id }
        ));

        foreach (var genre in genres)
        {
            movie.Genres.Add(genre);
        }

        return movie;
    }

    public async Task<IEnumerable<Movie>> GetAllAsync()
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var movies = await connection.QueryAsync(new CommandDefinition(
            commandText: """
                SELECT m.id, m.slug, m.title, m.yearofrelease, string_agg(g.name, ',') as genres
                FROM movies m 
                LEFT JOIN genres g ON m.id = g.movieId
                GROUP BY m.id;
            """
        ));

        return movies.Select(movie => new Movie
        {
            Id = movie.id,
            Title = movie.title,
            YearOfRelease = movie.yearofrelease,
            Genres = Enumerable.ToList(movie.genres.Split(","))
        });
    }

    public async Task<bool> UpdateAsync(Movie movie)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            commandText: """
                DELETE FROM genres
                WHERE movieId = @Id;
            """,
            parameters: new { movie.Id },
            transaction: transaction
        ));

        foreach (var genre in movie.Genres)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                commandText: """
                    INSERT INTO genres (movieId, name)
                    VALUES (@MovieId, @Name);
                """,
                parameters: new { MovieId = movie.Id, Name = genre },
                transaction: transaction
            ));
        }

        var result = await connection.ExecuteAsync(new CommandDefinition(
            commandText: """
                UPDATE movies
                SET slug = @Slug, title = @Title, yearofrelease = @YearOfRelease
                WHERE id = @Id;
            """,
            parameters: movie,
            transaction: transaction
        ));

        transaction.Commit();

        return result > 0;
    }

    public async Task<bool> DeleteByIdAsync(Guid id)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            commandText: """
                DELETE FROM genres
                WHERE movieId = @Id;
            """,
            parameters: new { Id = id },
            transaction: transaction
        ));

        var result = await connection.ExecuteAsync(new CommandDefinition(
            commandText: """
                DELETE FROM movies
                WHERE id = @Id;
            """,
            parameters: new { Id = id },
            transaction: transaction
        ));

        transaction.Commit();
        return result > 0;

    }

    public async Task<bool> ExistsByIdAsync(Guid id)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            commandText: """
                SELECT EXISTS (
                    SELECT 1
                    FROM movies
                    WHERE id = @Id
                );
            """,
            parameters: new { Id = id }
        ));
    }
}
