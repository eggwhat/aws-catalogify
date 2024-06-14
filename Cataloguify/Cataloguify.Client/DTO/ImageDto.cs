namespace Cataloguify.Client.DTO;

public class ImageDto
{
    public Guid ImageKey { get; set; }
    public Guid UserId { get; set; }
    public string ImageUrl { get; set; }
    public IEnumerable<string> Tags { get; set; }
    public DateTime UploadetAt { get; set; }
}
