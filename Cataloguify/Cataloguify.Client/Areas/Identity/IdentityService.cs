using System.IdentityModel.Tokens.Jwt;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Cataloguify.Client.HttpClients;

namespace Cataloguify.Client.Areas.Identity;

public class IdentityService : IIdentityService
{
    private readonly IHttpClient _httpClient;
    private readonly JwtSecurityTokenHandler _jwtHandler;
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _navigationManager;

    public string Token { get; private set;}
    public string Email { get; private set; }
    public string Username { get; private set; }
    public bool IsAuthenticated { get; set; }

    public IdentityService(IHttpClient httpClient, ILocalStorageService localStorage,
        NavigationManager navigationManager)
    {
        _httpClient = httpClient;
        _jwtHandler = new JwtSecurityTokenHandler();
        _localStorage = localStorage;
        _navigationManager = navigationManager;
    }

    public async Task<HttpResponse<object>> SignUpAsync(string email, string username, string password)
    {
        return await _httpClient.PostAsync<object, object>("sign-up",
            new { email, username, password });

    }

    public async Task<HttpResponse<string>> SignInAsync(string email, string password)
    {
        var response = await _httpClient.PostAsync<object, string>("generate-token", new { email, password });
        Token = response.Content;
        await _localStorage.SetItemAsStringAsync("Token", Token);

        var jwtToken = _jwtHandler.ReadJwtToken(Token);
        var payload = jwtToken.Payload;
        Email = payload.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        Username = payload.Claims.FirstOrDefault(c => c.Type == "username")?.Value;
        IsAuthenticated = true;

        return response;
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync("Token");
        Token = null;
        Email = null;
        Username = null;
        IsAuthenticated = false;
        _navigationManager.NavigateTo("signin", forceLoad: true);
    }
}