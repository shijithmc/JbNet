using Amazon.CDK;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace JbNet.Cdk.Stacks;

public sealed class JbNetStackProps : StackProps
{
    public required string Stage { get; init; }
}

public sealed class JbNetStack : Stack
{
    public JbNetStack(Construct scope, string id, JbNetStackProps props) : base(scope, id, props)
    {
        var stage = props.Stage;

        // ── DynamoDB single-table ─────────────────────────────────────────────
        var table = new Table(this, "JbNetTable", new TableProps
        {
            TableName         = $"jbnet-table-{stage}",
            PartitionKey      = new Attribute { Name = "PK", Type = AttributeType.STRING },
            SortKey           = new Attribute { Name = "SK", Type = AttributeType.STRING },
            BillingMode       = BillingMode.PAY_PER_REQUEST,
            PointInTimeRecovery  = true,
            RemovalPolicy     = stage == "prod" ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY,
            TimeToLiveAttribute = "TTL"
        });

        // GSI1 — pending referrals by participant
        table.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
        {
            IndexName    = "GSI1-PendingByParticipant",
            PartitionKey = new Attribute { Name = "GSI1PK", Type = AttributeType.STRING },
            SortKey      = new Attribute { Name = "GSI1SK", Type = AttributeType.STRING },
            ProjectionType = ProjectionType.ALL
        });

        // ── S3 resume bucket ──────────────────────────────────────────────────
        var resumeBucket = new Bucket(this, "ResumeBucket", new BucketProps
        {
            BucketName        = $"jbnet-resumes-{stage}-{Account}",
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption        = BucketEncryption.S3_MANAGED,
            Versioned         = false,
            RemovalPolicy     = stage == "prod" ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY,
            AutoDeleteObjects = stage != "prod",
            LifecycleRules    = new[]
            {
                // Purge unclaimed uploads (file uploaded but request withdrawn within 24h)
                new LifecycleRule { Expiration = Duration.Days(stage == "prod" ? 365 : 30) }
            }
        });

        // ── Cognito User Pool ─────────────────────────────────────────────────
        var userPool = new UserPool(this, "UserPool", new UserPoolProps
        {
            UserPoolName    = $"jbnet-users-{stage}",
            SelfSignUpEnabled  = true,
            SignInAliases      = new SignInAliases { Email = true },
            AutoVerify         = new AutoVerifiedAttrs { Email = true },
            PasswordPolicy     = new PasswordPolicy
            {
                MinLength        = 8,
                RequireDigits    = true,
                RequireLowercase = true,
                RequireUppercase = false,
                RequireSymbols   = false
            },
            AccountRecovery    = AccountRecovery.EMAIL_ONLY,
            RemovalPolicy      = stage == "prod" ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY
        });

        var userPoolClient = new UserPoolClient(this, "UserPoolClient", new UserPoolClientProps
        {
            UserPool      = userPool,
            GenerateSecret = false,
            AuthFlows      = new AuthFlow { UserSrp = true, UserPassword = false },
            AccessTokenValidity  = Duration.Hours(1),
            IdTokenValidity      = Duration.Hours(1),
            RefreshTokenValidity = Duration.Days(30)
        });

        // ── EventBridge custom bus ────────────────────────────────────────────
        var eventBus = new EventBus(this, "JbNetBus", new EventBusProps
        {
            EventBusName = $"jbnet-bus-{stage}"
        });

        // ── SQS notification queue (with DLQ) ─────────────────────────────────
        var notificationDlq = new Queue(this, "NotificationDlq", new QueueProps
        {
            QueueName     = $"jbnet-notifications-dlq-{stage}",
            RetentionPeriod = Duration.Days(14)
        });

        var notificationQueue = new Queue(this, "NotificationQueue", new QueueProps
        {
            QueueName              = $"jbnet-notifications-{stage}",
            VisibilityTimeout      = Duration.Seconds(60),
            DeadLetterQueue        = new DeadLetterQueue
            {
                Queue             = notificationDlq,
                MaxReceiveCount   = 3
            }
        });

        // ── Lambda execution role (shared) ────────────────────────────────────
        var lambdaRole = new Role(this, "LambdaRole", new RoleProps
        {
            RoleName   = $"jbnet-lambda-role-{stage}",
            AssumedBy  = new ServicePrincipal("lambda.amazonaws.com"),
            ManagedPolicies = new IManagedPolicy[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
            }
        });

        table.GrantReadWriteData(lambdaRole);
        resumeBucket.GrantReadWrite(lambdaRole);
        eventBus.GrantPutEventsTo(lambdaRole);
        notificationQueue.GrantSendMessages(lambdaRole);

        // SNS publish permission — granted to all SNS resources (endpoints added at runtime)
        lambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect    = Effect.ALLOW,
            Actions   = new[] { "sns:Publish" },
            Resources = new[] { "*" } // narrowed to topic ARNs in prod via SCPs
        }));

        // SES send permission
        lambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect    = Effect.ALLOW,
            Actions   = new[] { "ses:SendEmail", "ses:SendRawEmail" },
            Resources = new[] { "*" }
        }));

        // ── Shared Lambda environment variables ───────────────────────────────
        var commonEnv = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = stage == "prod" ? "Production" : "Development",
            ["DynamoDB__TableName"]    = table.TableName,
            ["S3__ResumeBucketName"]   = resumeBucket.BucketName,
            ["EventBridge__BusName"]   = eventBus.EventBusName,
            ["Cognito__Authority"]     =
                $"https://cognito-idp.{Region}.amazonaws.com/{userPool.UserPoolId}"
        };

        // ── API Lambda ────────────────────────────────────────────────────────
        var apiFunction = new Function(this, "ApiFunction", new FunctionProps
        {
            FunctionName = $"jbnet-api-{stage}",
            Runtime      = Runtime.DOTNET_9,
            Handler      = "JbNet.Api",
            Code         = Code.FromAsset("../../src/Api/publish"),
            MemorySize   = 512,
            Timeout      = Duration.Seconds(30),
            Role         = lambdaRole,
            Environment  = commonEnv,
            LogRetention = RetentionDays.ONE_MONTH,
            Tracing      = Tracing.ACTIVE
        });

        // ── Expiry Lambda ─────────────────────────────────────────────────────
        var expiryFunction = new Function(this, "ExpiryFunction", new FunctionProps
        {
            FunctionName = $"jbnet-expiry-{stage}",
            Runtime      = Runtime.DOTNET_9,
            Handler      = "JbNet.Functions::JbNet.Functions.ExpiryFunction::Handler",
            Code         = Code.FromAsset("../../src/Functions/publish"),
            MemorySize   = 256,
            Timeout      = Duration.Minutes(5),
            Role         = lambdaRole,
            Environment  = commonEnv,
            LogRetention = RetentionDays.ONE_MONTH,
            Tracing      = Tracing.ACTIVE
        });

        // Trigger expiry function hourly
        new Rule(this, "ExpirySchedule", new RuleProps
        {
            Schedule    = Schedule.Expression("rate(1 hour)"),
            Description = "Hourly referral request expiry sweep",
            Targets     = new IRuleTarget[] { new LambdaFunction(expiryFunction) }
        });

        // ── Notification Lambda ───────────────────────────────────────────────
        var notificationFunction = new Function(this, "NotificationFunction", new FunctionProps
        {
            FunctionName = $"jbnet-notifications-{stage}",
            Runtime      = Runtime.DOTNET_9,
            Handler      = "JbNet.Functions::JbNet.Functions.NotificationFunction::Handler",
            Code         = Code.FromAsset("../../src/Functions/publish"),
            MemorySize   = 256,
            Timeout      = Duration.Seconds(60),
            Role         = lambdaRole,
            Environment  = new Dictionary<string, string>(commonEnv)
            {
                // SNS_PLATFORM_ARN is set post-deploy when mobile apps register
                ["SNS_PLATFORM_ARN"] = ""
            },
            LogRetention = RetentionDays.ONE_MONTH,
            Tracing      = Tracing.ACTIVE
        });

        notificationQueue.GrantConsumeMessages(notificationFunction);

        // EventBridge rule: forward all jbnet events → SQS for notification Lambda
        new Rule(this, "NotificationRule", new RuleProps
        {
            EventBus    = eventBus,
            Description = "Route all JbNet domain events to notification queue",
            EventPattern = new EventPattern
            {
                Source = new[] { "jbnet.api" }
            },
            Targets = new IRuleTarget[] { new SqsQueue(notificationQueue) }
        });

        // ── Outputs ───────────────────────────────────────────────────────────
        _ = new CfnOutput(this, "UserPoolId",       new CfnOutputProps { Value = userPool.UserPoolId });
        _ = new CfnOutput(this, "UserPoolClientId", new CfnOutputProps { Value = userPoolClient.UserPoolClientId });
        _ = new CfnOutput(this, "TableName",        new CfnOutputProps { Value = table.TableName });
        _ = new CfnOutput(this, "ResumeBucketName", new CfnOutputProps { Value = resumeBucket.BucketName });
        _ = new CfnOutput(this, "EventBusName",     new CfnOutputProps { Value = eventBus.EventBusName });
        _ = new CfnOutput(this, "ApiLambdaArn",     new CfnOutputProps { Value = apiFunction.FunctionArn });
    }
}
