using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Services;
using RealEstateStar.Api.Features.Cma.Submit;
using RealEstateStar.Api.Hubs;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Tests.Integration;

namespace RealEstateStar.Api.Tests.Features.Cma.Submit;

public class SubmitCmaEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SubmitCmaEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCma_Returns202_WithJobId()
    {
        var lead = new
        {
            firstName = "John",
            lastName = "Doe",
            email = "john@example.com",
            phone = "555-1234",
            address = "123 Main St",
            city = "Springfield",
            state = "NJ",
            zip = "07081",
            timeline = "3-6 months"
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/cma", lead);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("jobId", out var jobId));
        Assert.False(string.IsNullOrEmpty(jobId.GetString()));
        Assert.Equal("processing", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostCma_Returns400_WhenFirstNameMissing()
    {
        var lead = new
        {
            firstName = "",
            lastName = "Doe",
            email = "john@example.com",
            phone = "555-1234",
            address = "123 Main St",
            city = "Springfield",
            state = "NJ",
            zip = "07081",
            timeline = "3-6 months"
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/cma", lead);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task PostCma_Returns400_WhenEmailInvalid()
    {
        var lead = new
        {
            firstName = "John",
            lastName = "Doe",
            email = "not-an-email",
            phone = "555-1234",
            address = "123 Main St",
            city = "Springfield",
            state = "NJ",
            zip = "07081",
            timeline = "3-6 months"
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/cma", lead);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task PostCma_Returns400_WhenAddressMissing()
    {
        var lead = new
        {
            firstName = "John",
            lastName = "Doe",
            email = "john@example.com",
            phone = "555-1234",
            address = "",
            city = "",
            state = "NJ",
            zip = "07081",
            timeline = "3-6 months"
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/cma", lead);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task PostCma_Returns400_WhenZipInvalid()
    {
        var lead = new
        {
            firstName = "John",
            lastName = "Doe",
            email = "john@example.com",
            phone = "555-1234",
            address = "123 Main St",
            city = "Springfield",
            state = "NJ",
            zip = "ABCDE",
            timeline = "3-6 months"
        };

        var response = await _client.PostAsJsonAsync("/agents/jenise-buckalew/cma", lead);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Zip", out _));
    }

    [Fact]
    public async Task GetCmaStatus_Returns404_ForUnknownJob()
    {
        var response = await _client.GetAsync("/agents/jenise-buckalew/cma/nonexistent-job/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class SubmitCmaEndpointUnitTests
{
    private static SubmitCmaRequest MakeValidRequest() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com",
        Phone = "555-1234",
        Address = "123 Main St",
        City = "Springfield",
        State = "NJ",
        Zip = "07081",
        Timeline = "3-6 months"
    };

    private static (Mock<ICmaJobStore> store, Mock<ICmaPipeline> pipeline,
        Mock<IHubContext<CmaProgressHub>> hubContext, Mock<ILogger<Program>> logger) CreateMocks()
    {
        var store = new Mock<ICmaJobStore>();
        var pipeline = new Mock<ICmaPipeline>();
        var hubContext = new Mock<IHubContext<CmaProgressHub>>();
        var logger = new Mock<ILogger<Program>>();

        // Setup hub context chain
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        hubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

        return (store, pipeline, hubContext, logger);
    }

    [Fact]
    public void Handle_ReturnsAccepted_ForValidRequest()
    {
        var (store, pipeline, hubContext, logger) = CreateMocks();

        var result = SubmitCmaEndpoint.Handle(
            "test-agent", MakeValidRequest(), store.Object, pipeline.Object,
            hubContext.Object, logger.Object, CancellationToken.None);

        result.Should().BeOfType<Accepted<SubmitCmaResponse>>();
        var accepted = (Accepted<SubmitCmaResponse>)result;
        accepted.Value!.Status.Should().Be("processing");
        accepted.Value.JobId.Should().NotBeNullOrEmpty();

        store.Verify(s => s.Set("test-agent", It.IsAny<CmaJob>()), Times.Once);
    }

    [Fact]
    public void Handle_ReturnsValidationProblem_WhenRequestInvalid()
    {
        var (store, pipeline, hubContext, logger) = CreateMocks();
        var request = new SubmitCmaRequest
        {
            FirstName = "",       // Invalid: required
            LastName = "Doe",
            Email = "invalid",    // Invalid: not email format
            Phone = "555",
            Address = "123 Main",
            City = "Springfield",
            State = "NJ",
            Zip = "07081",
            Timeline = "ASAP"
        };

        var result = SubmitCmaEndpoint.Handle(
            "test-agent", request, store.Object, pipeline.Object,
            hubContext.Object, logger.Object, CancellationToken.None);

        // Results.ValidationProblem returns ProblemHttpResult in minimal APIs
        result.Should().BeOfType<ProblemHttpResult>();
    }

    [Fact]
    public async Task Handle_FailsJob_WithExMessage_WhenPipelineThrowsArgumentException()
    {
        var (store, pipeline, hubContext, logger) = CreateMocks();
        CmaJob? capturedJob = null;

        store.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<CmaJob>()))
            .Callback<string, CmaJob>((_, job) => capturedJob = job);

        pipeline.Setup(p => p.ExecuteAsync(
                It.IsAny<CmaJob>(), It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<Func<CmaJobStatus, Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid input data"));

        SubmitCmaEndpoint.Handle(
            "test-agent", MakeValidRequest(), store.Object, pipeline.Object,
            hubContext.Object, logger.Object, CancellationToken.None);

        // Wait for the background Task.Run to complete
        await Task.Delay(200);

        capturedJob.Should().NotBeNull();
        capturedJob!.Status.Should().Be(CmaJobStatus.Failed);
        capturedJob.ErrorMessage.Should().Be("Invalid input data");
    }

    [Fact]
    public async Task Handle_FailsJob_WithGenericMessage_WhenPipelineThrowsUnexpectedException()
    {
        var (store, pipeline, hubContext, logger) = CreateMocks();
        CmaJob? capturedJob = null;

        store.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<CmaJob>()))
            .Callback<string, CmaJob>((_, job) => capturedJob = job);

        pipeline.Setup(p => p.ExecuteAsync(
                It.IsAny<CmaJob>(), It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<Func<CmaJobStatus, Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection timeout"));

        SubmitCmaEndpoint.Handle(
            "test-agent", MakeValidRequest(), store.Object, pipeline.Object,
            hubContext.Object, logger.Object, CancellationToken.None);

        await Task.Delay(200);

        capturedJob.Should().NotBeNull();
        capturedJob!.Status.Should().Be(CmaJobStatus.Failed);
        capturedJob.ErrorMessage.Should().Be("Pipeline execution failed. Please try again or contact support.");
    }

    [Fact]
    public async Task Handle_FailsJob_WithExMessage_WhenPipelineThrowsInvalidOperationException()
    {
        var (store, pipeline, hubContext, logger) = CreateMocks();
        CmaJob? capturedJob = null;

        store.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<CmaJob>()))
            .Callback<string, CmaJob>((_, job) => capturedJob = job);

        pipeline.Setup(p => p.ExecuteAsync(
                It.IsAny<CmaJob>(), It.IsAny<string>(), It.IsAny<Lead>(),
                It.IsAny<Func<CmaJobStatus, Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent config not found"));

        SubmitCmaEndpoint.Handle(
            "test-agent", MakeValidRequest(), store.Object, pipeline.Object,
            hubContext.Object, logger.Object, CancellationToken.None);

        await Task.Delay(200);

        capturedJob.Should().NotBeNull();
        capturedJob!.Status.Should().Be(CmaJobStatus.Failed);
        capturedJob.ErrorMessage.Should().Be("Agent config not found");
    }
}
