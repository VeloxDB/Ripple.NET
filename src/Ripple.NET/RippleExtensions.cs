using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Ripple.NET;

public static class RippleExtensions
{
    public static IServiceCollection AddRipple(this IServiceCollection services)
    {
        Console.WriteLine("Adding RippleService to services.");
        services.AddSingleton<RippleService>();
        return services;
    }

    public static IApplicationBuilder UseRipple(this IApplicationBuilder app)
    {
        app.Map("/ripple", builder =>
        {
            EmbeddedFileProvider embeddedProvider = new EmbeddedFileProvider(typeof(RippleExtensions).Assembly, "Ripple.NET.static");
            builder.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = embeddedProvider,
                RequestPath = ""
            });

#if !DEBUG
            builder.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = embeddedProvider,
                RequestPath = ""
            });
#else
            builder.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Path.GetDirectoryName(GetCurrentFileName())!, "static")),
                RequestPath = ""
            });
#endif

            builder.UseRouting();

            builder.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/data", (RippleService rippleService) =>
                {
                    KeyValuePair<string, List<APICall>>[] apiCalls = rippleService.GetAPICalls().ToArray();

                    Array.Sort(apiCalls, (a, b) =>
                    {
                        bool HasTran(KeyValuePair<string, List<APICall>> entry) => entry.Value.SelectMany(tx => tx.Transactions).Any();

                        // First put those with transactions first
                        int cmp = -HasTran(a).CompareTo(HasTran(b));
                        if (cmp != 0) return cmp;
                        // Then sort alphabetically
                        return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
                    });

                    return Results.Json(apiCalls);
                });

                endpoints.MapPost("/merge", async (IFormFile file, RippleService rippleService) =>
                {
                    if (file == null || file.Length == 0)
                        return Results.BadRequest("No file uploaded.");

                    using var stream = file.OpenReadStream();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new APICallConverter(), new TransactionConverter() }
                    };

                    var apiCalls = await JsonSerializer.DeserializeAsync<KeyValuePair<string, List<APICall>>[]>(stream, options);

                    if (apiCalls != null)
                    {
                        foreach (var entry in apiCalls)
                        {
                            if (entry.Value == null) continue;

                            foreach (var call in entry.Value)
                            {
                                rippleService.AddAPICall(call);
                            }
                        }
                    }

                    return Results.Ok();
                })
                .DisableAntiforgery();

                endpoints.MapPost("/clear", (RippleService rippleService) =>
                {
                    rippleService.Clear();
                    return Results.Ok();
                });
            });
        });
        app.Use(async (context, next) =>
        {
            RippleService service = context.RequestServices.GetRequiredService<RippleService>();
            Interceptor interceptor = service.Interceptor;

            var endpoint = context.GetEndpoint();
            if (endpoint == null)
            {
                await next.Invoke();
                return;
            }
            interceptor.StartAPICall(endpoint);
            await next.Invoke();

            APICall? apiCall = interceptor.EndAPICall();
            if (apiCall != null)
            {
                service.AddAPICall(apiCall);
            }
        });

        return app;
    }

    #if DEBUG
    private static string GetCurrentFileName([CallerFilePath] string fileName = "")
    {
        return fileName;
    }
    #endif

    public static DbContextOptionsBuilder UseRipple(
        this DbContextOptionsBuilder optionsBuilder)
    {
        var serviceProvider = optionsBuilder.Options.GetExtension<CoreOptionsExtension>()?.ApplicationServiceProvider;
        var rippleService = serviceProvider?.GetService<RippleService>();
        if (rippleService == null)
        {
            throw new InvalidOperationException("RippleService is not registered in the service provider. Please ensure that AddRipple() is called during service configuration.");
        }
        var interceptor = rippleService.Interceptor;
        interceptor.Register(optionsBuilder);
        return optionsBuilder;
    }
}