namespace Contracts;

/// <summary>
/// RabbitMQ connection settings, bound from the "RabbitMq" configuration section.
/// Env var form: RabbitMq__Host, RabbitMq__Username, RabbitMq__Password, ...
/// </summary>
public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "rabbitmq";
    public ushort Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
}
