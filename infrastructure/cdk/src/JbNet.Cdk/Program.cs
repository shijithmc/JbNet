using Amazon.CDK;
using JbNet.Cdk.Stacks;

var app = new App();
var env = new Amazon.CDK.Environment
{
    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
    Region  = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION") ?? "ap-south-1"
};

var stage = app.Node.TryGetContext("stage")?.ToString() ?? "dev";

new JbNetStack(app, $"JbNet-{stage}", new JbNetStackProps
{
    Env   = env,
    Stage = stage
});

app.Synth();
