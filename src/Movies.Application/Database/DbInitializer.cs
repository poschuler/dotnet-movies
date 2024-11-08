using System;
using Dapper;

namespace Movies.Application.Database;

public class DbInitializer
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public DbInitializer(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task InitializeAsync()
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();

        await connection.ExecuteAsync("""
           CREATE TABLE IF NOT EXISTS movies (
               id UUID PRIMARY KEY,
               slug TEXT NOT NULL,
               title TEXT NOT NULL,
               yearofrelease integer NOT NULL) ;
        """);

        await connection.ExecuteAsync("""
            create unique index concurrently if not exists movies_slug_idx 
            on movies 
            using btree(slug);
        """);

        await connection.ExecuteAsync("""
            create table if not exists genres (
                movieId UUID references movies(id),
                name TEXT NOT NULL);
        """);

        await connection.ExecuteAsync("""
            create table if not exists ratings (
                userid UUID,
                movieid UUID references movies(id),
                rating integer NOT NULL,
                primary key (userid, movieid));
        """);

    }

}
