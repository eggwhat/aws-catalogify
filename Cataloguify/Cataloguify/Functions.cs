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
using static System.Net.Mime.MediaTypeNames;
using Amazon.S3;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using Amazon.S3.Model;
using Cataloguify.Documents;
using Cataloguify.Requests;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

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

    float MinConfidence { get; set; } = DEFAULT_MIN_CONFIDENCE;

    HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".jpg", ".jpeg" };

    /// <summary>
    /// Default constructor that Lambda will invoke.
    /// </summary>
    public Functions()
    {
        this.S3Client = new AmazonS3Client();
        this.RekognitionClient = new AmazonRekognitionClient();

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
    public async Task<string> GenerateTokenAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var tokenRequest = JsonConvert.DeserializeObject<Documents.User>(request.Body);
        AmazonDynamoDBClient client = new AmazonDynamoDBClient();
        DynamoDBContext dbContext = new DynamoDBContext(client);

        //check if user exists in ddb
        var user = await dbContext.LoadAsync<Documents.User>(tokenRequest?.Email);
        if (user == null) throw new Exception("User Not Found!");
        if (user.Password != tokenRequest.Password) throw new Exception("Invalid Credentials!");
        var token = GenerateJWT(user);
        return token;
    }

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/sign-up")]
    public async Task SignUpAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var signUp = JsonConvert.DeserializeObject<SignUpRequest>(request.Body);
        AmazonDynamoDBClient client = new AmazonDynamoDBClient();
        DynamoDBContext dbContext = new DynamoDBContext(client);

        //check if user exists in ddb
        var user = await dbContext.LoadAsync<Documents.User>(signUp?.Email);
        if (user != null) throw new Exception("User Already Exists!");
        if (signUp.Email == null || signUp.Username == null || signUp.Password == null)
        {
            throw new ArgumentException("Invalid sign up request");
        }

        var userDocument = new Documents.User(signUp.Email, signUp.Username, signUp.Password);
        await dbContext.SaveAsync(userDocument);
    }

    public string GenerateJWT(Documents.User user)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, user.Email), new(ClaimTypes.Name, user.Username) };
        byte[] secret = Encoding.UTF8.GetBytes(key);
        var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: signingCredentials);
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
        return new APIGatewayCustomAuthorizerResponse()
        {
            PrincipalID = principalId,
            PolicyDocument = new APIGatewayCustomAuthorizerPolicy()
            {
                Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
            {
                new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement()
                {
                    Effect = effect,
                    Resource = new HashSet<string> { "arn:aws:execute-api:us-east-1:885422015476:xuo9x6k2e6/*/*" },
                    Action = new HashSet<string> { "execute-api:Invoke" }
                }
            }
            }
        };
    }
    
    private ClaimsPrincipal GetClaimsPrincipal(string authToken)
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
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        try
        {
            var imageRequest = JsonConvert.DeserializeObject<ImageRequest>(request.Body);
            byte[] imageBytes = Convert.FromBase64String(imageRequest.Image);


            // Detect faces in the image
            DetectFacesRequest detectFacesRequest = new DetectFacesRequest
            {
                Image = new Amazon.Rekognition.Model.Image { Bytes = new MemoryStream(imageBytes) }
            };

            DetectFacesResponse detectFacesResponse = await RekognitionClient.DetectFacesAsync(detectFacesRequest);

            // Upload the image to S3
            string s3Key = Guid.NewGuid().ToString(); // Generate a unique key for the S3 object
            using (var s3Client = S3Client)
            {
                PutObjectRequest s3Request = new PutObjectRequest
                {
                    BucketName = S3_BUCKET_NAME,
                    Key = s3Key,
                    InputStream = new MemoryStream(imageBytes)
                };
                PutObjectResponse s3Response = await s3Client.PutObjectAsync(s3Request);
            }


            // Process the response
            StringBuilder responseBuilder = new StringBuilder();
            foreach (FaceDetail faceDetail in detectFacesResponse.FaceDetails)
            {
                responseBuilder.AppendLine($"Detected face with confidence: {faceDetail.Confidence}");
            }

            // Return response
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = responseBuilder.ToString(),
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };
        }
        catch (Exception ex)
        {
            // Handle any errors
            LambdaLogger.Log($"Error processing image: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = "Error processing image",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };
        }
    }
}
