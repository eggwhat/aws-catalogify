namespace Cataloguify.Client.DTO;

public class ResponseDto
{
    public string Body { get; set; }
    public int StatusCode { get; set; }
    public bool IsBase64Encoded { get; set; }
}
