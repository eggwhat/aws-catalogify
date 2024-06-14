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
    
    public async Task<HttpResponse<ResponseDto<object>>> UploadImageAsync(string image)
    {
        _httpClient.SetAccessToken(await _localStorage.GetItemAsStringAsync("Token"));
        return await _httpClient.PostAsync<object, ResponseDto<object>>("upload-image", new { image });
    }

    public async Task<HttpResponse<ResponseDto<IEnumerable<ImageDto>>>> SearchImagesAsync(IEnumerable<string> tags, int page,
        int results, string sortOrder)
    {
        _httpClient.SetAccessToken(await _localStorage.GetItemAsStringAsync("Token"));
        return await _httpClient.PostAsync<object, ResponseDto<IEnumerable<ImageDto>>>("images",
            new { tags, page, results, sortOrder });
    }
}
