using Amazon.DynamoDBv2.DataModel;

namespace Cataloguify.Documents
{
    [DynamoDBTable("users")]
    public class User
    {
        [DynamoDBHashKey("email")]
        public string? Email { get; set; }
        [DynamoDBProperty("username")]
        public string? Username { get; set; }
        [DynamoDBProperty("password")]
        public string? Password { get; set; }

        public User(string? email, string? username, string? password)
        {
            Email = email;
            Username = username;
            Password = password;
        }
    }
}