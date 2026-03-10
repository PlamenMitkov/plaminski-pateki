using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EcoTrails.Api.OpenApi;

public sealed class AuthAndRateLimitOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Responses ??= [];

        var methodAttributes = context.MethodInfo.GetCustomAttributes(inherit: true).OfType<Attribute>();
        var controllerAttributes = context.MethodInfo.DeclaringType?
            .GetCustomAttributes(inherit: true)
            .OfType<Attribute>()
            ?? Enumerable.Empty<Attribute>();

        var allAttributes = methodAttributes.Concat(controllerAttributes).ToArray();
        var hasAllowAnonymous = allAttributes.OfType<AllowAnonymousAttribute>().Any();
        var hasAuthorize = !hasAllowAnonymous && allAttributes.OfType<AuthorizeAttribute>().Any();
        var hasRateLimiting = allAttributes.OfType<EnableRateLimitingAttribute>().Any();

        if (hasAuthorize)
        {
            operation.Responses.TryAdd("401", new OpenApiResponse
            {
                Description = "Unauthorized (missing or invalid Bearer token)."
            });

            operation.Responses.TryAdd("403", new OpenApiResponse
            {
                Description = "Forbidden (insufficient permissions)."
            });

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, null, null)] = []
            });
        }

        if (hasRateLimiting)
        {
            operation.Responses.TryAdd("429", new OpenApiResponse
            {
                Description = "Too Many Requests.",
                Headers = new Dictionary<string, IOpenApiHeader>
                {
                    ["Retry-After"] = new OpenApiHeader
                    {
                        Description = "Seconds to wait before retrying.",
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Integer,
                            Format = "int32"
                        }
                    }
                }
            });
        }
    }
}