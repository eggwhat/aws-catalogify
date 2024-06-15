namespace Cataloguify.Client.DTO;

public class PageableDto
{
    public int Page { get; set; }
    public int Results { get; set; }
    public int TotalPages { get; set; }
    public IEnumerable<ImageDto> Content { get; set; }
}
