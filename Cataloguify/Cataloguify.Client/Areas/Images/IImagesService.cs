using Cataloguify.Client.DTO;
using Cataloguify.Client.HttpClients;

namespace Cataloguify.Client.Areas.Images;

public interface IImagesService
{
    Task<HttpResponse<object>> UploadImageAsync(string image);
    Task<HttpResponse<PageableDto>> SearchImagesAsync(IEnumerable<string> tags, int page,
        int results, string sortOrder);
}
