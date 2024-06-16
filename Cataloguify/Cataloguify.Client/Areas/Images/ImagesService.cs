using System;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Cataloguify.Client.DTO;
using Cataloguify.Client.HttpClients;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json; 

namespace Cataloguify.Client.Areas.Images
{
    public class ImagesService : IImagesService
    {
        private readonly IHttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<ImagesService> _logger;

        public ImagesService(IHttpClient httpClient, ILocalStorageService localStorage, ILogger<ImagesService> logger)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _logger = logger;
        }

        public async Task<HttpResponse<object>> UploadImageAsync(string base64Image)
        {
            try
            {
                var token = await _localStorage.GetItemAsStringAsync("Token");
                _httpClient.SetAccessToken(token);
                var imageData = new { image = base64Image };

                _logger.LogInformation($"Sending image to API: {System.Text.Json.JsonSerializer.Serialize(imageData)}");
                var response = await _httpClient.PostAsJsonAsync("prod/upload-image", imageData);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Image uploaded successfully!");
                    var content = await response.Content.ReadFromJsonAsync<object>();
                    return new HttpResponse<object>(content);
                }
                else
                {
                    _logger.LogError($"Failed to upload image. Status Code: {response.StatusCode}");
                    return new HttpResponse<object>(new ErrorMessage($"Failed with status code: {response.StatusCode}"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occurred: {ex.Message}");
                return new HttpResponse<object>(new ErrorMessage(ex.Message));
            }
        }



        public async Task<HttpResponse<PageableDto>> SearchImagesAsync(IEnumerable<string> tags, int page, int results, string sortOrder)
        {
            _httpClient.SetAccessToken(await _localStorage.GetItemAsStringAsync("Token"));
            return await _httpClient.PostAsync<object, PageableDto>("prod/images", new { tags, page, results, sortOrder });
        }
    }

    public async Task DeleteImageAsync(Guid imageKey)
    {
        _httpClient.SetAccessToken(await _localStorage.GetItemAsStringAsync("Token"));
        await _httpClient.DeleteAsync($"prod/images?imageKey={imageKey}");
    }
}
