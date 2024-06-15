using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Rekognition;
using Amazon.S3;
using Cataloguify.Dynamo;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Cataloguify.Tests;

public class FunctionTest
{
    private readonly Functions _functions;
    private readonly Mock<ILambdaContext> _mockContext;
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<IAmazonRekognition> _mockRekognition;
    private readonly Mock<IAmazonDynamoDB> _mockDynamoDB;
    private readonly Mock<IDynamoDBContext> _mockDynamoDBContext;
    private readonly Mock<IDynamoDBHelper> _mockDynamoDBHelper;
    private readonly TestLambdaLogger _testLogger;


    public FunctionTest()
    {
        _mockContext = new Mock<ILambdaContext>();
        _mockS3Client = new Mock<IAmazonS3>();
        _mockRekognition = new Mock<IAmazonRekognition>();
        _mockDynamoDB = new Mock<IAmazonDynamoDB>();
        _mockDynamoDBContext = new Mock<IDynamoDBContext>();
        _mockDynamoDBHelper = new Mock<IDynamoDBHelper>();
        _testLogger = new TestLambdaLogger();
        _mockContext.Setup(x => x.Logger).Returns(_testLogger);
        _functions = new Functions(_mockS3Client.Object, _mockRekognition.Object, _mockDynamoDB.Object, 
            _mockDynamoDBContext.Object, _mockDynamoDBHelper.Object);
    }

    [Fact]
    public async Task GenerateTokenAsync_UserDoesNotExist_ReturnsBadRequest()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new { Email = "test@example.com", Password = "password123" })
        };

        _mockDynamoDBHelper.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((Documents.User)null);

        // Act
        var response = await _functions.GenerateTokenAsync(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Be(JsonConvert.SerializeObject(new { Message = "User does not exist!" }));
    }

    [Fact]
    public async Task GenerateTokenAsync_InvalidPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new { Email = "test@example.com", Password = "wrongpassword" })
        };

        var user = new Documents.User { Email = "test@example.com", Password = "password123" };
        _mockDynamoDBHelper.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        // Act
        var response = await _functions.GenerateTokenAsync(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Be(JsonConvert.SerializeObject(new { Message = "Invalid credentials!" }));
    }

    [Fact]
    public async Task GenerateTokenAsync_ValidCredentials_ReturnsAccessToken()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new { Email = "test@example.com", Password = "password" })
        };

        var user = new Documents.User { UserId = Guid.NewGuid(), Email = "test@example.com", Password = "password", Username = "username" };
        _mockDynamoDBHelper.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        // Act
        var response = await _functions.GenerateTokenAsync(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(200);
        var responseBody = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.Body);
        responseBody.Should().ContainKey("AccessToken");
        responseBody["AccessToken"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateTokenAsync_ExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new { Email = "test@example.com", Password = "password123" })
        };

        _mockDynamoDBHelper.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())).ThrowsAsync(new System.Exception("Database error"));

        // Act
        var response = await _functions.GenerateTokenAsync(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(500);
        response.Body.Should().Be(JsonConvert.SerializeObject(new { Message = "An error occurred during signup" }));
    }



}
