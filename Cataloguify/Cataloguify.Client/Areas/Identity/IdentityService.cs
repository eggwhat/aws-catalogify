using System.IdentityModel.Tokens.Jwt;
using Blazored.LocalStorage;
using Cataloguify.Client.DTO;
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
        return await _httpClient.PostAsync<object, object>("prod/sign-up",
            new { email, username, password });
    }

    public async Task<HttpResponse<TokenDto>> SignInAsync(string email, string password)
    {
        var response = await _httpClient.PostAsync<object, TokenDto>("prod/generate-token", new { email, password });
        if (response.ErrorMessage != null)
        {
            return response;
        }

        Token = response.Content.AccessToken;
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
        Token = null;
        await _localStorage.RemoveItemAsync("Token");

        Email = null;
        await _localStorage.RemoveItemAsync("Email");

        Username = null;
        await _localStorage.RemoveItemAsync("Username");

        IsAuthenticated = false;
        await _localStorage.SetItemAsStringAsync("IsAuthenticated", IsAuthenticated.ToString());

        await _localStorage.RemoveItemAsync("searchImagesCriteria");
        _navigationManager.NavigateTo("signin", forceLoad: true);
    }
}
