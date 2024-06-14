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
        Console.WriteLine($"Error during sign in: {response.ErrorMessage}");
        return response;
    }

    try
    {
        Token = response.Content;
        Console.WriteLine($"Received Token: {Token}");
        
        await _localStorage.SetItemAsStringAsync("Token", Token);
        var jwtToken = _jwtHandler.ReadJwtToken(Token);
        var payload = jwtToken.Payload;
        
        Email = payload.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
        Username = payload.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value;
        
        if (Email == null || Username == null)
        {
            Console.WriteLine("Error: Missing claims in token.");
            return response;
        }

        IsAuthenticated = true;
        
        await _localStorage.SetItemAsStringAsync("Email", Email);
        await _localStorage.SetItemAsStringAsync("Username", Username);
        await _localStorage.SetItemAsStringAsync("IsAuthenticated", IsAuthenticated.ToString());

        return response;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error parsing token: {ex.Message}");
        return response;
    }
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