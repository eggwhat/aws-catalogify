namespace Cataloguify.Entities;

public class Image
{
    public Guid ImageKey { get; set; }
    public Guid UserId { get; set; }
    public string ImageUrl { get; set; }
    public List<string> Tags { get; set; }
    public DateTime UploadedAt { get; set; }
}