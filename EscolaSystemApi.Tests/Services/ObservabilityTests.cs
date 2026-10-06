using EscolaSystemApi.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class ObservabilityTests
{
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "EscolaSystemApi";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddObservability(configuration, new TestEnvironment());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void WithoutEndpoint_RegistersNothing()
    {
        using var provider = Build([]);

        provider.GetService<TracerProvider>().Should().BeNull();
        provider.GetService<MeterProvider>().Should().BeNull();
    }

    [Theory]
    [InlineData("OpenTelemetry:OtlpEndpoint")]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT")]
    public void WithEndpoint_RegistersTracingAndMetrics(string key)
    {
        using var provider = Build(new() { [key] = "http://localhost:4317" });

        provider.GetService<TracerProvider>().Should().NotBeNull();
        provider.GetService<MeterProvider>().Should().NotBeNull();
    }
}
