using System.Net;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Newtonsoft.Json;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using Amazon.S3.Model;
using Cataloguify.Documents;
using Cataloguify.Requests;
using Cataloguify.Dynamo;
using Amazon.Auth.AccessControlPolicy;
using System.Text.Json;
using Cataloguify.Entities;
using System.Runtime.CompilerServices;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
[assembly: InternalsVisibleTo("Cataloguify.Tests")]

namespace Cataloguify;

public class Functions
{
    private const string S3_BUCKET_NAME = "cataloguify-bucket";
    public const float DEFAULT_MIN_CONFIDENCE = 70f;

    /// <summary>
    /// The name of the environment variable to set which will override the default minimum confidence level.
    /// </summary>
    public const string MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME = "MinConfidence";

    IAmazonS3 S3Client { get; }
    IAmazonRekognition RekognitionClient { get; }
    IAmazonDynamoDB AmazonDynamoDBClient { get; }
    IDynamoDBContext DynamoDBContext { get; }
    IDynamoDBHelper DynamoDBHelper { get; }

    float MinConfidence { get; set; } = DEFAULT_MIN_CONFIDENCE;

    /// <summary>
    /// Default constructor that Lambda will invoke.
    /// </summary>
    public Functions() : this(new AmazonS3Client(), new AmazonRekognitionClient(), new AmazonDynamoDBClient(), null, null)
    {
    }

    internal Functions(IAmazonS3 s3Client, IAmazonRekognition rekognitionClient, IAmazonDynamoDB amazonDynamoDBClient, IDynamoDBContext dynamoDBContext, IDynamoDBHelper dynamoDBHelper)
    {
        S3Client = s3Client;
        RekognitionClient = rekognitionClient;
        AmazonDynamoDBClient = amazonDynamoDBClient;
        DynamoDBContext = dynamoDBContext ?? new DynamoDBContext(amazonDynamoDBClient);
        DynamoDBHelper = dynamoDBHelper ?? new DynamoDBHelper(amazonDynamoDBClient);
    }


    /// <summary>
    /// A Lambda function to respond to HTTP Get methods from API Gateway
    /// </summary>
    /// <remarks>
    /// This uses the <see href="https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.Annotations/README.md">Lambda Annotations</see> 
    /// programming model to bridge the gap between the Lambda programming model and a more idiomatic .NET model.
    /// 
    /// This automatically handles reading parameters from an APIGatewayProxyRequest
    /// as well as syncing the function definitions to serverless.template each time you build.
    /// 
    /// If you do not wish to use this model and need to manipulate the API Gateway 
    /// objects directly, see the accompanying Readme.md for instructions.
    /// </remarks>
    /// <param name="context">Information about the invocation, function, and execution environment</param>
    /// <returns>The response as an implicit <see cref="APIGatewayProxyResponse"/></returns>
    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Get, "/")]
    public IHttpResult Get(ILambdaContext context)
    {
        context.Logger.LogInformation("Handling the 'Get' Request");

        return HttpResults.Ok("Hello AWS Serverless");
    }

    private const string key = "eiquief5phee9pazo0Faegaez9gohThailiur5woy2befiech1oarai4aiLi6ahVecah3ie9Aiz6Peij";

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/generate-token")]
    public async Task<APIGatewayProxyResponse> GenerateTokenAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var response = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };

        try
        {
            var tokenRequest = JsonConvert.DeserializeObject<Documents.User>(request.Body);

            //check if user exists in ddb
            var user = await DynamoDBHelper.GetUserByEmailAsync(tokenRequest?.Email);
            if (user == null)
            {
                response.StatusCode = 400;
                response.Body = JsonConvert.SerializeObject(new { Message = "User does not exist!" });
                return response;
            }

            if (user.Password != tokenRequest.Password)
            {
                response.StatusCode = 400;
                response.Body = JsonConvert.SerializeObject(new { Message = "Invalid credentials!" });
                return response;
            }
            var token = GenerateJWT(user);
            response.StatusCode = 200;
            response.Body = JsonConvert.SerializeObject(new { AccessToken = token });
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error: {ex.Message}");
            response.StatusCode = 500;
            response.Body = JsonConvert.SerializeObject(new { Message = "An error occurred during signup" });
        }

        return response;
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/sign-up")]
    public async Task<APIGatewayProxyResponse> SignUpAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var response = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };

        try
        {
            if (string.IsNullOrEmpty(request.Body))
            {
                response.StatusCode = 400;
                response.Body = JsonConvert.SerializeObject(new { Message = "Request body is empty" });
                return response;
            }

            var signUpRequest = JsonConvert.DeserializeObject<SignUpRequest>(request.Body);

            if (string.IsNullOrEmpty(signUpRequest.Username) ||
                string.IsNullOrEmpty(signUpRequest.Email) ||
                string.IsNullOrEmpty(signUpRequest.Password))
            {
                response.StatusCode = 400;
                response.Body = JsonConvert.SerializeObject(new { Message = "Invalid user data" });
                return response;
            }

            var existingUser = await DynamoDBHelper.GetUserByEmailAsync(signUpRequest.Email);
            if (existingUser != null)
            {
                response.StatusCode = 400;
                response.Body = JsonConvert.SerializeObject(new { Message = "User already signed up" });
                return response;
            }

            var user = new Documents.User
            {
                UserId = Guid.NewGuid(),
                Email = signUpRequest.Email,
                Username = signUpRequest.Username,
                Password = signUpRequest.Password
            };
            await DynamoDBContext.SaveAsync(user);

            response.StatusCode = 201;
            response.Body = JsonConvert.SerializeObject(new { Message = "User signed up successfully" });
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error: {ex.Message}");
            response.StatusCode = 500;
            response.Body = JsonConvert.SerializeObject(new { Message = "An error occurred during signup" });
        }

        return response;
    }

    public string GenerateJWT(Documents.User user)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email), new(ClaimTypes.Name, user.Username) };
        byte[] secret = Encoding.UTF8.GetBytes(key);
        var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddMinutes(60), signingCredentials: signingCredentials);
        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    [LambdaFunction]
    public APIGatewayCustomAuthorizerResponse ValidateTokenAsync(APIGatewayCustomAuthorizerRequest request, ILambdaContext context)
    {
        var authToken = request.Headers["authorization"];
        var claimsPrincipal = GetClaimsPrincipal(authToken);
        var effect = claimsPrincipal == null ? "Deny" : "Allow";
        var principalId = claimsPrincipal == null ? "401" : claimsPrincipal?.FindFirst(ClaimTypes.Name)?.Value;
        string userId = claimsPrincipal?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        return new APIGatewayCustomAuthorizerResponse()
        {
            PrincipalID = principalId,
            Context = new APIGatewayCustomAuthorizerContextOutput()
            {
                {"UserId", userId}
            },
            PolicyDocument = new APIGatewayCustomAuthorizerPolicy()
            {
                Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
            {
                new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement()
                {
                    Effect = effect,
                    Resource = new HashSet<string> { "arn:aws:execute-api:us-east-1:885422015476:9tty1ch09d/*/*" },
                    Action = new HashSet<string> { "execute-api:Invoke" }
                }
            }
            }
        };
    }

    internal ClaimsPrincipal GetClaimsPrincipal(string authToken)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParams = new TokenValidationParameters()
        {
            ValidateLifetime = true,
            ValidateAudience = false,
            ValidateIssuer = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        };
        try
        {
            return tokenHandler.ValidateToken(authToken, validationParams, out SecurityToken securityToken);
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/upload-image")]
    public async Task<APIGatewayProxyResponse> UploadImage(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var response = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" }, {"Access-Control-Allow-Origin", "*" },
                { "Access-Control-Allow-Credentials", "true"} }
        };
    
        var environmentMinConfidence = System.Environment.GetEnvironmentVariable(MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME);
        if (!string.IsNullOrWhiteSpace(environmentMinConfidence))
        {
            float value;
            if (float.TryParse(environmentMinConfidence, out value))
            {
                this.MinConfidence = value;
                Console.WriteLine($"Setting minimum confidence to {this.MinConfidence}");
            }
            else
            {
                Console.WriteLine($"Failed to parse value {environmentMinConfidence} for minimum confidence. Reverting back to default of {this.MinConfidence}");
            }
        }
        else
        {
            Console.WriteLine($"Using default minimum confidence of {this.MinConfidence}");
        }

        try
        {
            var authorizerContext = request.RequestContext.Authorizer;
            var userId = ((JsonElement)authorizerContext.Lambda["UserId"]).Deserialize<string>();
            
            var imageRequest = JsonConvert.DeserializeObject<ImageRequest>(request.Body);
            byte[] imageBytes = Convert.FromBase64String(imageRequest.Image);

            // Detect labels in the image
            DetectLabelsRequest detectLabelsRequest = new DetectLabelsRequest
            {
                Image = new Amazon.Rekognition.Model.Image { Bytes = new MemoryStream(imageBytes) },
                MaxLabels = 10,
                MinConfidence = 75F
            };

            DetectLabelsResponse detectLabelsResponse = await RekognitionClient.DetectLabelsAsync(detectLabelsRequest);
            foreach (Label label in detectLabelsResponse.Labels)
                Console.WriteLine("{0}: {1}", label.Name, label.Confidence);

            // Upload the image to S3
            var s3Key = Guid.NewGuid(); // Generate a unique key for the S3 object
            using (var s3Client = S3Client)
            {
                PutObjectRequest s3Request = new PutObjectRequest
                {
                    BucketName = S3_BUCKET_NAME,
                    Key = s3Key.ToString(),
                    InputStream = new MemoryStream(imageBytes)
                };
                PutObjectResponse s3Response = await s3Client.PutObjectAsync(s3Request);
            }

            var imageInfo = new ImageInfo
            {
                ImageKey = s3Key,
                UserId = new Guid(userId),
                Tags = detectLabelsResponse.Labels.Select(x => x.Name).ToList(),
                UploadedAt = DateTime.UtcNow,
            };
            await DynamoDBContext.SaveAsync(imageInfo);

            // Return response
            response.StatusCode = 200;
        }
        catch (Exception ex)
        {
            // Handle any errors
            LambdaLogger.Log($"Error processing image: {ex.Message}");
            response.StatusCode = 500;
            response.Body = "Error processing image";
        }
        return response;
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/images")]
    public async Task<APIGatewayProxyResponse> GetImages(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var response = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
        try
        {
            var authorizerContext = request.RequestContext.Authorizer;
            var userId = ((JsonElement)authorizerContext.Lambda["UserId"]).Deserialize<string>();
            var imagesInfos = await DynamoDBHelper.GetUserImagesAsync(userId);

            var searchImageRequest = JsonConvert.DeserializeObject<SearchImagesRequest>(request.Body);
            var filterImages = imagesInfos;
            if(searchImageRequest.Tags.Count > 0)
            {
                filterImages = imagesInfos.Where(x => x.Tags.Where(tag => searchImageRequest.Tags.Contains(tag)).Any());
            }
            filterImages = searchImageRequest.SortOrder == "des" ? filterImages.OrderByDescending(x => x.UploadedAt).ThenBy(x => x.ImageKey)
                                                                 : filterImages.OrderBy(x => x.UploadedAt).ThenBy(x => x.ImageKey);
            var totalPages = (int)Math.Ceiling((decimal)filterImages.Count() / searchImageRequest.Results);
            filterImages = filterImages.Skip((searchImageRequest.Page - 1) * searchImageRequest.Results).Take(searchImageRequest.Results);


            var images = filterImages.Select(x => new Entities.Image
            {
                ImageKey = x.ImageKey,
                UserId = x.UserId,
                ImageUrl = GeneratePresignedURL(x.ImageKey.ToString(), 60),
                Tags = x.Tags,
                UploadedAt = x.UploadedAt,
            });

            var responseBody = new PagedResponse<IEnumerable<Entities.Image>>
            {
                Page = searchImageRequest.Page,
                Results = images.Count(),
                TotalPages = totalPages,
                Content = images
            };

            response.Body = JsonConvert.SerializeObject(responseBody);
            response.StatusCode = 200;
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error: {ex.Message}");
            response.StatusCode = 500;
            response.Body = JsonConvert.SerializeObject(new { Message = "An error occurred during retrieving images" });
        }

        return response;    
    }

    private string GeneratePresignedURL(string objectKey, double duration)
    {
        string urlString = string.Empty;
        try
        {
            var request = new GetPreSignedUrlRequest()
            {
                BucketName = S3_BUCKET_NAME,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddHours(duration),
            };
            urlString = S3Client.GetPreSignedURL(request);
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error:'{ex.Message}'");
        }

        return urlString;
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Delete, "/images")]
    public async Task<APIGatewayProxyResponse> DeleteImage(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var response = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };

        try
        {
            // Get user ID from authorizer context
            var authorizerContext = request.RequestContext.Authorizer;
            var userId = ((JsonElement)authorizerContext.Lambda["UserId"]).Deserialize<string>();


            // Check if the imageKey parameter exists and is not null or empty
            if (request.QueryStringParameters == null || !request.QueryStringParameters.TryGetValue("imageKey", out string imageKey) || string.IsNullOrEmpty(imageKey))
            {
                response.StatusCode = 400;
                response.Body = JsonConvert.SerializeObject(new { Message = "Missing imageKey parameter" });
                return response;
            }

            // Delete the image from S3
            var deleteObjectRequest = new Amazon.S3.Model.DeleteObjectRequest
            {
                BucketName = S3_BUCKET_NAME,
                Key = imageKey
            };
            await S3Client.DeleteObjectAsync(deleteObjectRequest);

            // Delete the image record from DynamoDB
            var imageInfo = await DynamoDBContext.LoadAsync<ImageInfo>(new Guid(imageKey), new Guid(userId));
            if (imageInfo != null)
            {
                await DynamoDBContext.DeleteAsync(imageInfo);
            }

            // Return success response
            response.StatusCode = 200;
            response.Body = JsonConvert.SerializeObject(new { Message = "Image deleted successfully" });
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error: {ex.Message}");
            response.StatusCode = 500;
            response.Body = JsonConvert.SerializeObject(new { Message = "An error occurred while deleting the image" });
        }

        return response;
    }

}
