using Amazon.DynamoDBv2;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using VELoyalty.Data;
using VELoyalty.Data.Repositories;
using VELoyalty.StreamProcessor;

// Configure services
var dynamoDbClient = new AmazonDynamoDBClient();
var lambdaClient = new AmazonLambdaClient();

var tableName = Environment.GetEnvironmentVariable("DYNAMODB_TABLE") ?? DynamoDbContext.TableName;
var notificationFunctionName = Environment.GetEnvironmentVariable("NOTIFICATION_FUNCTION_NAME")
    ?? "VELoyalty-NotificationHandler";

var context = new DynamoDbContext(dynamoDbClient, tableName);

var purchaseRepository = new PurchaseRepository(context);
var customerRepository = new CustomerRepository(context);
var configRepository = new ConfigRepository(context);
var cycleRepository = new CycleRepository(context);
var eligibilityRepository = new EligibilityRepository(context);
var verificationCodeRepository = new VerificationCodeRepository(context);
var notificationInvoker = new LambdaNotificationInvoker(lambdaClient, notificationFunctionName);

var function = new StreamProcessorFunction(
    purchaseRepository,
    customerRepository,
    configRepository,
    cycleRepository,
    eligibilityRepository,
    verificationCodeRepository,
    notificationInvoker);

// Create the Lambda runtime handler using source-generated serializer for Native AOT
var serializer = new SourceGeneratorLambdaJsonSerializer<StreamProcessorJsonContext>();

var handler = async (DynamoDBEvent dynamoDbEvent, ILambdaContext lambdaContext) =>
{
    await function.HandleAsync(dynamoDbEvent, lambdaContext);
};

await LambdaBootstrapBuilder
    .Create<DynamoDBEvent>(handler, serializer)
    .Build()
    .RunAsync();
