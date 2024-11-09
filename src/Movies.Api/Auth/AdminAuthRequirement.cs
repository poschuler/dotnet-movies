using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Movies.Api.Auth;

public class AdminAuthRequirement : IAuthorizationHandler, IAuthorizationRequirement
{
    private readonly string _apiKey;

    public AdminAuthRequirement(string apiKey)
    {
        _apiKey = apiKey;
    }

    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.User.HasClaim(AuthConstants.AdminUserClaimName, "true"))
        {
            context.Succeed(this);
            return Task.CompletedTask;
        }

        var HttpContext = context.Resource as HttpContext;
        if (HttpContext is null)
        {
            return Task.CompletedTask;
        }

        if (!HttpContext.Request.Headers.TryGetValue(AuthConstants.ApiKeyHeaderName,
            out var apiKey))
        {
            context.Fail();
            return Task.CompletedTask;
        }

        if (_apiKey != apiKey)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        var identity = (ClaimsIdentity)HttpContext.User.Identity!;
        identity.AddClaim(new Claim("userId", Guid.Parse("00000000-0000-0000-0000-000000000000").ToString()));
        context.Succeed(this);
        return Task.CompletedTask;
    }
}
