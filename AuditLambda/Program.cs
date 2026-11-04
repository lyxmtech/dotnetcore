using AuditLambda;

var function = new Function();
var response = await function.FunctionHandlerAsync(null, null);
Console.WriteLine($"{{ \"total\": {response.Total} }}");
