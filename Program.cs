using System;
using System.Reflection;

using Elastic.Apm.NetCoreAll;
using Elastic.Apm.SerilogEnricher;

using Microsoft.Extensions.Hosting;

using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Extensions.Hosting;
using Serilog.Sinks.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);

ConfigureLogging();
builder.Host.UseSerilog((ctx, lc) => lc
       .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Environment", "development")
        .Enrich.WithElasticApmCorrelationInfo() // NOTE: self-contained
        .Enrich.WithExceptionDetails()
        .WriteTo.Debug()
         .WriteTo.Console()
        .WriteTo.Elasticsearch(ConfigureElasticSinks(ctx.Configuration, "development"))
        .Enrich.WithProperty("Environment", "development")
       .ReadFrom.Configuration(ctx.Configuration));

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}


app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging(options =>
{
    // Customize the message template   
    options.MessageTemplate = "{RemoteIpAddress} {RequestScheme} {RequestHost} {RequestMethod} {RequestPath} responded  test{StatusCode} in {Elapsed:0.0000} ms";

    // Emit debug-level events instead of the defaults
    options.GetLevel = (httpContext, elapsed, ex) => LogEventLevel.Debug;

    // Attach additional properties to the request completion event
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);
    };
});


app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

void ConfigureLogging()
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile(
            $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json",
            optional: true)
        .Build();

    Log.Logger = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithProperty("Environment", "development")
        .Enrich.WithElasticApmCorrelationInfo() // NOTE: self-contained
        .Enrich.WithExceptionDetails()
        .WriteTo.Debug()
        .WriteTo.Console()
        .WriteTo.Elasticsearch(ConfigureElasticSink(configuration, environment))
        .Enrich.WithProperty("Environment", environment)
        .ReadFrom.Configuration(configuration)
        .CreateLogger();
}

ElasticsearchSinkOptions ConfigureElasticSink(IConfigurationRoot configuration, string environment)
{
    return new ElasticsearchSinkOptions(new Uri(configuration["ElasticConfiguration:Uri"]))
    {
        AutoRegisterTemplate = false,
        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
        BatchAction = ElasticOpType.Create,
        TypeName = null,
        TemplateName = $"timeseries_template",
        IndexAliases = new[] { $"timeseries" },
        IndexFormat = $"timeseries",
    };
}


ElasticsearchSinkOptions ConfigureElasticSinks(IConfiguration configuration, string environment)
{
    return new ElasticsearchSinkOptions(new Uri(configuration["ElasticConfiguration:Uri"]))
    {
        AutoRegisterTemplate = false,
        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
        BatchAction = ElasticOpType.Create,
        TypeName = null,
        TemplateName = $"timeseries_template",
        IndexAliases = new[] { $"timeseries" },
        IndexFormat = $"timeseries",
    };
}

