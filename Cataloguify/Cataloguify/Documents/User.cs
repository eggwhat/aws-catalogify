using Amazon.DynamoDBv2.DataModel;

namespace Cataloguify.Documents
{
    [DynamoDBTable("users")]
    public class User
    {
        [DynamoDBHashKey("UserId")]
        public Guid UserId { get; set; }
        [DynamoDBProperty("Email")]
        public string? Email { get; set; }
        [DynamoDBProperty("Username")]
        public string? Username { get; set; }
        [DynamoDBProperty("Password")]
        public string? Password { get; set; }
    }
}