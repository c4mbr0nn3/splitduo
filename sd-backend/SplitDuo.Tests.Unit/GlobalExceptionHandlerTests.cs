using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SplitDuo.Core.Exceptions;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class GlobalExceptionHandlerTests
{
    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? CapturedContext { get; private set; }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext problemDetailsContext)
        {
            CapturedContext = problemDetailsContext;
            return ValueTask.FromResult(true);
        }

        public ValueTask WriteAsync(ProblemDetailsContext problemDetailsContext) =>
            ValueTask.CompletedTask;
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "SplitDuo.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (DefaultHttpContext HttpContext, CapturingProblemDetailsService Service)
        CreateContext()
    {
        var httpContext = new DefaultHttpContext();
        var service = new CapturingProblemDetailsService();
        return (httpContext, service);
    }

    private static async Task Handle(
        CapturingProblemDetailsService service,
        IHostEnvironment environment,
        DefaultHttpContext httpContext,
        Exception exception)
    {
        var handler = new GlobalExceptionHandler(
            service,
            NullLogger<GlobalExceptionHandler>.Instance,
            environment);

        await handler.TryHandleAsync(httpContext, exception, default);
    }

    [Fact]
    public async Task Production_GenericException_HidesDetailAndTypeName_AddsTraceId()
    {
        var (httpContext, service) = CreateContext();
        var environment = new FakeHostEnvironment("Production");

        await Handle(service, environment, httpContext, new InvalidOperationException("boom"));

        var problemDetails = Assert.IsType<ProblemDetails>(service.CapturedContext?.ProblemDetails);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Null(problemDetails.Detail);
        Assert.NotEqual("InvalidOperationException", problemDetails.Type);
        Assert.True(problemDetails.Extensions.TryGetValue("traceId", out var traceId));
        Assert.False(string.IsNullOrEmpty(traceId?.ToString()));
    }

    [Fact]
    public async Task Development_GenericException_KeepsDetailAndTypeName()
    {
        var (httpContext, service) = CreateContext();
        var environment = new FakeHostEnvironment("Development");

        await Handle(service, environment, httpContext, new InvalidOperationException("boom"));

        var problemDetails = Assert.IsType<ProblemDetails>(service.CapturedContext?.ProblemDetails);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal("boom", problemDetails.Detail);
        Assert.Equal("InvalidOperationException", problemDetails.Type);
    }

    [Fact]
    public async Task Production_ApplicationException_Returns400WithUserFacingDetail()
    {
        var (httpContext, service) = CreateContext();
        var environment = new FakeHostEnvironment("Production");

        await Handle(service, environment, httpContext, new ApplicationException("user-facing"));

        var problemDetails = Assert.IsType<ProblemDetails>(service.CapturedContext?.ProblemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal("user-facing", problemDetails.Detail);
    }
}