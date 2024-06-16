using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazored.LocalStorage;
using Cataloguify.Client;
using Cataloguify.Client.Areas.Identity;
using Cataloguify.Client.Areas.Images;
using Cataloguify.Client.HttpClients;
using MudBlazor;
using MudBlazor.Services;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient
var httpClientOptions = new HttpClientOptions();
builder.Configuration.Bind("HttpClientOptions", httpClientOptions);
builder.Services.AddSingleton(httpClientOptions);
builder.Services.AddHttpClient<IHttpClient, CustomHttpClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<HttpClientOptions>();
    client.BaseAddress = new Uri(options.ApiUrl);
});

// Add local storage and other services
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<Radzen.DialogService, Radzen.DialogService>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IImagesService, ImagesService>();

// Correctly add and configure MudServices
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 10000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});



await builder.Build().RunAsync();
