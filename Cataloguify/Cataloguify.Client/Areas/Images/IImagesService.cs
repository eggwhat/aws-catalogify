using Cataloguify.Client.DTO;
using Cataloguify.Client.HttpClients;

namespace Cataloguify.Client.Areas.Images;

public interface IImagesService
{
    Task<HttpResponse<ResponseDto<object>>> UploadImageAsync(string image);
    Task<HttpResponse<ResponseDto<IEnumerable<ImageDto>>>> SearchImagesAsync(IEnumerable<string> tags, int page,
        int results, string sortOrder);
}
