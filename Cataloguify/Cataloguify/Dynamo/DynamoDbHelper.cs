using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Cataloguify.Documents;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Cataloguify.Dynamo;

public class DynamoDBHelper
{
    private readonly string _userTableName = "users";
    private readonly string _imageInfoTableName = "imageinfos";
    private readonly string _emailIndex = "EmailIndex";
    private readonly string _userIdIndex = "UserIndex";
    private readonly IAmazonDynamoDB amazonDynamoDBClient;

    public DynamoDBHelper(IAmazonDynamoDB amazonDynamoDBClient)
    {
        this.amazonDynamoDBClient = amazonDynamoDBClient;
    }

    public async Task<User> GetUserByEmailAsync(string email)
    {
        var queryRequest = new QueryRequest
        {
            TableName = _userTableName,
            IndexName = _emailIndex,
            KeyConditionExpression = "Email = :v_Email",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":v_Email", new AttributeValue { S = email } }
            }
        };

        var queryResponse = await amazonDynamoDBClient.QueryAsync(queryRequest);

        if (queryResponse.Items.Count > 0)
        {
            var document = queryResponse.Items[0];
            var user = new User
            {
                UserId = new Guid(document["UserId"].S),
                Email = document["Email"].S,
                Username = document["Username"].S,
                Password = document["Password"].S
            };
            return user;
        }

        return null;
    }

    public async Task<IEnumerable<ImageInfo>> GetUserImagesAsync(string userId)
    {
        var queryRequest = new QueryRequest
        {
            TableName = _imageInfoTableName,
            IndexName = _userIdIndex,
            KeyConditionExpression = "UserId = :v_UserId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":v_UserId", new AttributeValue { S = userId } }
            }
        };

        var queryResponse = await amazonDynamoDBClient.QueryAsync(queryRequest);

        if (queryResponse.Items.Count > 0)
        {
            var imagesInfos = queryResponse.Items.Select(x => new ImageInfo
            {
                ImageKey = new Guid(x["ImageKey"].S),
                UserId = new Guid(x["UserId"].S),
                Tags = x["Tags"].SS
            });
            return imagesInfos;
        }

        return null;
    }
}