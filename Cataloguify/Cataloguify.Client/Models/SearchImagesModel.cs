namespace Cataloguify.Client.Models;

public class SearchImagesModel
{
    public IEnumerable<string> Tags { get; set; }
    public IEnumerable<string> NotSelectedTags { get; set; }
    public int Page { get; set; }
    public int Results { get; set; }
    public string SortOrder { get; set; }
}
