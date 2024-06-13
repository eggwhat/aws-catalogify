namespace Cataloguify.Client.DTO;

public class ResponseDto<T>
{
    public T Body { get; set; }
    public int StatusCode { get; set; }
    public bool IsBase64Encoded { get; set; }
}
