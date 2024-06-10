using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazored.LocalStorage;
using Cataloguify.Client;
using Cataloguify.Client.Areas.Identity;
using Cataloguify.Client.HttpClients;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var httpClientOptions = new HttpClientOptions();
builder.Configuration.Bind("HttpClientOptions", httpClientOptions);
builder.Services.AddSingleton(httpClientOptions);

builder.Services.AddHttpClient<IHttpClient, CustomHttpClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<HttpClientOptions>();
    client.BaseAddress = new Uri(options.ApiUrl);
});

builder.Services.AddBlazoredLocalStorage(); 
builder.Services.AddScoped<IIdentityService, IdentityService>();

await builder.Build().RunAsync();
