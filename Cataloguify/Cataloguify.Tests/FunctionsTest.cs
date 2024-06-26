using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Cataloguify.Documents;
using Cataloguify.Dynamo;
using Cataloguify.Entities;
using Cataloguify.Requests;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Text.Json;
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
        _functions = Functions.MockFunctions(_mockS3Client.Object, _mockRekognition.Object, _mockDynamoDB.Object, 
            _mockDynamoDBContext.Object, _mockDynamoDBHelper.Object);
    }

    [Fact]
    public async Task GenerateTokenAsync_UserDoesNotExist_ReturnsBadRequest()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new { Email = "test@example.com", Password = "password" })
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

        var user = new Documents.User { Email = "test@example.com", Password = "password" };
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
            Body = JsonConvert.SerializeObject(new { Email = "test@example.com", Password = "password" })
        };

        _mockDynamoDBHelper.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())).ThrowsAsync(new System.Exception("Database error"));

        // Act
        var response = await _functions.GenerateTokenAsync(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(500);
        response.Body.Should().Be(JsonConvert.SerializeObject(new { Message = "An error occurred during signup" }));
    }

    [Fact]
    public async Task SignUpAsync_RequestBodyIsEmpty_ReturnsBadRequest()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = ""
        };

        // Act
        var response = await _functions.SignUpAsync(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Be(JsonConvert.SerializeObject(new { Message = "Request body is empty" }));
    }

    [Fact]
    public async Task SignUpAsync_InvalidUserData_ReturnsBadRequest()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new { Username = "", Email = "", Password = "" })
        };

        // Act
        var response = await _functions.SignUpAsync(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Be(JsonConvert.SerializeObject(new { Message = "Invalid user data" }));
    }

    [Fact]
    public async Task SignUpAsync_UserAlreadySignedUp_ReturnsBadRequest()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new { Username = "testuser", Email = "test@example.com", Password = "password" })
        };

        var existingUser = new Documents.User { Email = "test@example.com" };
        _mockDynamoDBHelper.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(existingUser);

        // Act
        var response = await _functions.SignUpAsync(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Be(JsonConvert.SerializeObject(new { Message = "User already signed up" }));
    }

    [Fact]
    public async Task SignUpAsync_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new { Username = "testuser", Email = "test@example.com", Password = "password" })
        };

        _mockDynamoDBHelper.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((Documents.User)null);
        _mockDynamoDBContext.Setup(x => x.SaveAsync(It.IsAny<Documents.User>(), default)).Returns(Task.CompletedTask);

        // Act
        var response = await _functions.SignUpAsync(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(201);
        response.Body.Should().Be(JsonConvert.SerializeObject(new { Message = "User signed up successfully" }));
    }

    [Fact]
    public async Task SignUpAsync_ExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new { Username = "testuser", Email = "test@example.com", Password = "password" })
        };

        _mockDynamoDBHelper.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())).ThrowsAsync(new System.Exception("Database error"));

        // Act
        var response = await _functions.SignUpAsync(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(500);
        response.Body.Should().Be(JsonConvert.SerializeObject(new { Message = "An error occurred during signup" }));
    }

    [Fact]
    public void ValidateTokenAsync_NoAuthorizationHeader_ReturnsDenyPolicy()
    {
        // Arrange
        var request = new APIGatewayCustomAuthorizerRequest
        {
            Headers = new Dictionary<string, string>()
        };

        // Act & Assert
        var act = () => _functions.ValidateTokenAsync(request, _mockContext.Object); 
        act.Should().Throw<KeyNotFoundException>();
    
    }

    [Fact]
    public async Task UploadImage_SuccessfulUpload_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new ImageRequest { Image = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }) }),
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                {
                    Lambda = new Dictionary<string, object>
                    {
                        { "UserId", JsonDocument.Parse($"\"{userId}\"").RootElement }
                    }
                }
            }
        };

        var detectLabelsResponse = new DetectLabelsResponse
        {
            Labels = new List<Label> { new Label { Name = "TestLabel", Confidence = 99F } }
        };
        _mockRekognition.Setup(x => x.DetectLabelsAsync(It.IsAny<DetectLabelsRequest>(), default)).ReturnsAsync(detectLabelsResponse);
        _mockS3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default)).ReturnsAsync(new PutObjectResponse());

        // Act
        var response = await _functions.UploadImage(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(200);
        response.Headers["Content-Type"].Should().Be("text/plain");
        _mockRekognition.Verify(x => x.DetectLabelsAsync(It.IsAny<DetectLabelsRequest>(), default), Times.Once);
        _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
        _mockDynamoDBContext.Verify(x => x.SaveAsync(It.IsAny<ImageInfo>(), default), Times.Once);
    }

    [Fact]
    public async Task UploadImage_ExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new ImageRequest { Image = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }) }),
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                {
                    Lambda = new Dictionary<string, object>
                    {
                        { "UserId", JsonDocument.Parse("\"test-user-id\"").RootElement }
                    }
                }
            }
        };

        _mockRekognition.Setup(x => x.DetectLabelsAsync(It.IsAny<DetectLabelsRequest>(), default)).ThrowsAsync(new System.Exception("Rekognition error"));

        // Act
        var response = await _functions.UploadImage(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(500);
        response.Body.Should().Be("Error processing image");
        _mockRekognition.Verify(x => x.DetectLabelsAsync(It.IsAny<DetectLabelsRequest>(), default), Times.Once);
        _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Never);
        _mockDynamoDBContext.Verify(x => x.SaveAsync(It.IsAny<ImageInfo>(), default), Times.Never);
    }

    [Fact]
    public async Task GetImages_SuccessfulRetrieval_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new SearchImagesRequest { Tags = new List<string>(), SortOrder = "asc", Page = 1, Results = 10 }),
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                {
                    Lambda = new Dictionary<string, object>
                    {
                        { "UserId", JsonDocument.Parse($"\"{userId}\"").RootElement }
                    }
                }
            }
        };

        var images = new List<Documents.ImageInfo>
        {
            new Documents.ImageInfo { ImageKey = Guid.NewGuid(), UserId = userId, Tags = new List<string> { "tag1" }, UploadedAt = DateTime.UtcNow }
        };

        _mockDynamoDBHelper.Setup(x => x.GetUserImagesAsync(userId.ToString())).ReturnsAsync(images);

        // Act
        var response = await _functions.GetImages(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(200);
        response.Headers["Content-Type"].Should().Be("application/json");
        var responseBody = JsonConvert.DeserializeObject<PagedResponse<IEnumerable<Entities.Image>>>(response.Body);
        responseBody.Content.Should().NotBeEmpty();
        _mockDynamoDBHelper.Verify(x => x.GetUserImagesAsync(userId.ToString()), Times.Once);
    }

    [Fact]
    public async Task GetImages_ExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            Body = JsonConvert.SerializeObject(new SearchImagesRequest { Tags = new List<string>(), SortOrder = "asc", Page = 1, Results = 10 }),
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                {
                    Lambda = new Dictionary<string, object>
                    {
                        { "UserId", JsonDocument.Parse("\"test-user-id\"").RootElement }
                    }
                }
            }
        };

        _mockDynamoDBHelper.Setup(x => x.GetUserImagesAsync("test-user-id")).ThrowsAsync(new System.Exception("DynamoDB error"));

        // Act
        var response = await _functions.GetImages(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(500);
        response.Body.Should().Contain("An error occurred during retrieving images");
        _mockDynamoDBHelper.Verify(x => x.GetUserImagesAsync("test-user-id"), Times.Once);
    }

    [Fact]
    public async Task DeleteImage_SuccessfulDeletion_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var imageKey = Guid.NewGuid();
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string> { { "imageKey", $"{imageKey}" } },
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                {
                    Lambda = new Dictionary<string, object>
                    {
                        { "UserId", JsonDocument.Parse($"\"{userId}\"").RootElement }
                    }
                }
            }
        };

        _mockS3Client.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default)).ReturnsAsync(new DeleteObjectResponse());
        _mockDynamoDBContext.Setup(x => x.LoadAsync<ImageInfo>(It.IsAny<object>(), default)).ReturnsAsync(new ImageInfo());
        _mockDynamoDBContext.Setup(x => x.DeleteAsync(It.IsAny<ImageInfo>(), default)).Returns(Task.CompletedTask);

        // Act
        var response = await _functions.DeleteImage(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(200);
        response.Body.Should().Contain("Image deleted successfully");
        _mockS3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default), Times.Once);
        _mockDynamoDBContext.Verify(x => x.LoadAsync<ImageInfo>(imageKey, userId, default), Times.Once);
    }

    [Fact]
    public async Task DeleteImage_MissingImageKeyParameter_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string>(),
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                {
                    Lambda = new Dictionary<string, object>
                    {
                        { "UserId", JsonDocument.Parse($"\"{userId}\"").RootElement }
                    }
                }
            }
        };

        // Act
        var response = await _functions.DeleteImage(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(400);
        response.Body.Should().Contain("Missing imageKey parameter");
        _mockS3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default), Times.Never);
        _mockDynamoDBContext.Verify(x => x.LoadAsync<ImageInfo>(It.IsAny<object>(), default), Times.Never);
        _mockDynamoDBContext.Verify(x => x.DeleteAsync(It.IsAny<ImageInfo>(), default), Times.Never);
    }

    [Fact]
    public async Task DeleteImage_ExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string> { { "imageKey", "test-image-key" } },
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription
                {
                    Lambda = new Dictionary<string, object>
                    {
                        { "UserId", JsonDocument.Parse("\"{userId}\"").RootElement }
                    }
                }
            }
        };

        _mockS3Client.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default)).ThrowsAsync(new System.Exception("S3 error"));

        // Act
        var response = await _functions.DeleteImage(request, _mockContext.Object);

        // Assert
        response.StatusCode.Should().Be(500);
        response.Body.Should().Contain("An error occurred while deleting the image");
        _mockS3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default), Times.Once);
        _mockDynamoDBContext.Verify(x => x.LoadAsync<ImageInfo>(It.IsAny<object>(), default), Times.Never);
        _mockDynamoDBContext.Verify(x => x.DeleteAsync(It.IsAny<ImageInfo>(), default), Times.Never);
    }

}
