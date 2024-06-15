using Blazored.LocalStorage;
using Cataloguify.Client.DTO;
using Cataloguify.Client.HttpClients;

namespace Cataloguify.Client.Areas.Images;

public class ImagesService : IImagesService
{
    private readonly IHttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;

    public ImagesService(IHttpClient httpClient, ILocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
    }
    
    public async Task<HttpResponse<object>> UploadImageAsync(string image)
    {
        _httpClient.SetAccessToken(await _localStorage.GetItemAsStringAsync("Token"));
        return await _httpClient.PostAsync<object, object>("prod/upload-image", new { image });
    }

    public async Task<HttpResponse<IEnumerable<ImageDto>>> SearchImagesAsync(IEnumerable<string> tags, int page,
        int results, string sortOrder)
    {
        _httpClient.SetAccessToken(await _localStorage.GetItemAsStringAsync("Token"));
        return await _httpClient.PostAsync<object, IEnumerable<ImageDto>>("prod/images",
            new { tags, page, results, sortOrder });
    }
}
