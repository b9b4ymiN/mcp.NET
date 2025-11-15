using Mcp.SqlApiServer.Config;
using Mcp.SqlApiServer.Models;
using Mcp.SqlApiServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;

namespace Mcp.SqlApiServer.Tests.Unit.Tools;

public class HttpToolServiceTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IOptions<HttpToolOptions>> _mockOptions;
    private readonly Mock<ILogger<HttpToolService>> _mockLogger;
    private readonly HttpToolOptions _options;

    public HttpToolServiceTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockOptions = new Mock<IOptions<HttpToolOptions>>();
        _mockLogger = new Mock<ILogger<HttpToolService>>();

        _options = new HttpToolOptions
        {
            AllowedHosts = new List<string> { "api.github.com", "example.com" },
            DefaultTimeoutSeconds = 30,
            MaxTimeoutSeconds = 120,
            AllowAllHosts = false
        };

        _mockOptions.Setup(x => x.Value).Returns(_options);
    }

    [Fact]
    public async Task CallAsync_WithValidUrl_ReturnsResult()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"message\":\"success\"}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("McpHttpClient")).Returns(httpClient);

        var service = new HttpToolService(_mockHttpClientFactory.Object, _mockOptions.Object, _mockLogger.Object);

        var parameters = new HttpCallParams
        {
            Method = "GET",
            Url = "https://api.github.com/users/octocat"
        };

        // Act
        var result = await service.CallAsync(parameters);

        // Assert
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("success", result.BodyText);
    }

    [Fact]
    public async Task CallAsync_WithDisallowedHost_ThrowsException()
    {
        // Arrange
        var service = new HttpToolService(_mockHttpClientFactory.Object, _mockOptions.Object, _mockLogger.Object);

        var parameters = new HttpCallParams
        {
            Method = "GET",
            Url = "https://evil.com/api"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CallAsync(parameters));
        Assert.Contains("not in the allowed hosts list", exception.Message);
    }

    [Fact]
    public async Task CallAsync_WithEmptyUrl_ThrowsArgumentException()
    {
        // Arrange
        var service = new HttpToolService(_mockHttpClientFactory.Object, _mockOptions.Object, _mockLogger.Object);

        var parameters = new HttpCallParams
        {
            Method = "GET",
            Url = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.CallAsync(parameters));
    }

    [Fact]
    public async Task CallAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        var service = new HttpToolService(_mockHttpClientFactory.Object, _mockOptions.Object, _mockLogger.Object);

        var parameters = new HttpCallParams
        {
            Method = "GET",
            Url = "not-a-valid-url"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.CallAsync(parameters));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task CallAsync_WithDifferentMethods_Succeeds(string method)
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("OK")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("McpHttpClient")).Returns(httpClient);

        var service = new HttpToolService(_mockHttpClientFactory.Object, _mockOptions.Object, _mockLogger.Object);

        var parameters = new HttpCallParams
        {
            Method = method,
            Url = "https://api.github.com/test"
        };

        // Act
        var result = await service.CallAsync(parameters);

        // Assert
        Assert.Equal(200, result.StatusCode);
    }
}
