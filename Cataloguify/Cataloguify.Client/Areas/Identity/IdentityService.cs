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
        if (response.ErrorMessage != null)
        {
            return response;
        }

        Token = response.Content;
        await _localStorage.SetItemAsStringAsync("Token", Token);
        
        var jwtToken = _jwtHandler.ReadJwtToken(Token);
        var payload = jwtToken.Payload;
        Email = payload.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
        Username = payload.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
        IsAuthenticated = true;

        await _localStorage.SetItemAsStringAsync("Email", email ?? string.Empty);
        await _localStorage.SetItemAsStringAsync("Username", Username ?? string.Empty);
        await _localStorage.SetItemAsStringAsync("IsAuthenticated", IsAuthenticated.ToString());
        
        return response;
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync("Token");
        Token = null;
        Email = null;
        Username = null;
        IsAuthenticated = false;
        await _localStorage.RemoveItemAsync("Email");
        await _localStorage.RemoveItemAsync("Username");
        await _localStorage.SetItemAsStringAsync("IsAuthenticated", IsAuthenticated.ToString());
        _navigationManager.NavigateTo("signin", forceLoad: true);
    }
}