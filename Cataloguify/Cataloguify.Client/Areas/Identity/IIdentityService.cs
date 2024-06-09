using Cataloguify.Client.HttpClients;

namespace Cataloguify.Client.Areas.Identity;

public interface IIdentityService
{ 
    string? Token { get; }
    string? Email { get; }
    string? Username { get; }
    bool IsAuthenticated { get; set; }
    
    Task<HttpResponse<object>> SignUpAsync(string email, string username, string password);
    Task<HttpResponse<string>> SignInAsync(string email, string password);
    Task Logout();
}