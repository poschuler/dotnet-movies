// See https://aka.ms/new-console-template for more information
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Movies.Api.Sdk;
using Movies.Api.Sdk.Consumer;
using Movies.Contracts.Requests;
using Refit;


//var moviesApi = RestService.For<IMoviesApi>("http://localhost:5038");

var services = new ServiceCollection();

services
.AddHttpClient()
.AddSingleton<AuthTokenProvider>()
.AddRefitClient<IMoviesApi>(q => new RefitSettings
{
    AuthorizationHeaderValueGetter = async (request, cancellationToken) => await q.GetRequiredService<AuthTokenProvider>().GetTokenAsync()
})
.ConfigureHttpClient(c => c.BaseAddress = new Uri("http://localhost:5038"));

var provider = services.BuildServiceProvider();

var moviesApi = provider.GetRequiredService<IMoviesApi>();

var movie = await moviesApi.GetMovieAsync("7ea232b8-c82c-4a54-abe3-fd61786e4193");

var request = new GetAllMoviesRequest
{
    Title = null,
    Year = null,
    SortBy = null,
    Page = 1,
    PageSize = 3
};

//var movies = await moviesApi.GetMoviesAsync(request);

//Console.WriteLine(JsonSerializer.Serialize(JsonSerializer.Serialize(movie)));


