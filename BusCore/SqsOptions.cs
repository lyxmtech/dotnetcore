namespace Contracts;

/// <summary>
/// Amazon SQS/SNS connection settings, bound from the "Sqs" configuration section.
/// Env var form: Sqs__Region, Sqs__ServiceUrl, Sqs__AccessKey, Sqs__SecretKey.
/// When ServiceUrl is set (e.g. http://localhost:4566 for LocalStack) the SDK
/// talks to that endpoint instead of real AWS; otherwise the default AWS endpoint
/// for Region is used and credentials come from the default credential chain
/// (IAM task role in ECS).
/// </summary>
public class SqsOptions
{
    public const string SectionName = "Sqs";

    public string Region { get; set; } = "us-east-2";

    /// <summary>Optional override for local testing (LocalStack). Null/empty in AWS.</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Optional explicit credentials (LocalStack). Leave empty in AWS.</summary>
    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }
}