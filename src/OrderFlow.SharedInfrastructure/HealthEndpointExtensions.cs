using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderFlow.SharedInfrastructure;

namespace OrderFlow.Health;

public static class HealthEndpointExtensions
{
    public static void MapOrderFlowHealthChecks(
        this WebApplication app)
    {
        // Liveness: is the HTTP host responding?
        app.MapHealthChecks(
            "/health/live",
            new HealthCheckOptions
            {
                Predicate = _ => false,

                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] =
                        StatusCodes.Status200OK,

                    [HealthStatus.Degraded] =
                        StatusCodes.Status503ServiceUnavailable,

                    [HealthStatus.Unhealthy] =
                        StatusCodes.Status503ServiceUnavailable
                }
            });

        // Readiness: are required dependencies usable?
        var messagingSchema = TestMessaging.GetSchema(app.Configuration);
        app.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions
            {
                Predicate = registration =>
                    registration.Tags.Contains("ready"),

                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] =
                        StatusCodes.Status200OK,

                    [HealthStatus.Degraded] =
                        StatusCodes.Status503ServiceUnavailable,

                    [HealthStatus.Unhealthy] =
                        StatusCodes.Status503ServiceUnavailable
                },

                ResponseWriter = (context, report) =>
                {
                    if (messagingSchema is not null)
                        context.Response.Headers["OrderFlow-Messaging-Schema"] = messagingSchema;

                    return WriteHealthResponse(context, report);
                }
            });
    }

    private static async Task WriteHealthResponse(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType =
            "application/json";

        var result = new
        {
            Status = report.Status.ToString(),

            TotalDuration =
                report.TotalDuration.TotalMilliseconds,

            Checks = report.Entries.Select(entry => new
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                Description = entry.Value.Description,
                Duration =
                    entry.Value.Duration.TotalMilliseconds
            })
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }
}
