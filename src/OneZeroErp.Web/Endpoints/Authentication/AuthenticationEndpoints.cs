using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using OneZeroErp.Application;
using OneZeroErp.Application.Time;
using ApplicationAuthenticationService = OneZeroErp.Application.IAuthenticationService;

namespace OneZeroErp.Web.Endpoints.Authentication;

public static class AuthenticationEndpoints
{
	public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapPost("/account/login", (Delegate)LoginAsync);
		endpoints.MapPost("/account/logout", (Delegate)LogoutAsync).RequireAuthorization();
		return endpoints;
	}

	private static async Task<IResult> LoginAsync(HttpContext httpContext, [FromForm] LoginRequest request,
			ApplicationAuthenticationService authentication,
			IClock clock)
	{
		var result = await authentication.AuthenticateAsync(request);
		if (!result.Succeeded || result.Principal is null)
			return Results.Redirect("/login?error=1");

		await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
				result.Principal,
				new AuthenticationProperties
				{
					IsPersistent = true,
					ExpiresUtc = clock.UtcNow.AddHours(8)
				});

		return Results.Redirect("/");
	}

	private static async Task<IResult> LogoutAsync(HttpContext httpContext)
	{
		await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
		return Results.Redirect("/login");
	}
}
