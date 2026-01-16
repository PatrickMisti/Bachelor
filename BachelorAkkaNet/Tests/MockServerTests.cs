using System.Net;
using MockOpenF1Service.Utilities;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;

namespace Tests;


public class MockServerTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public MockServerTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task Get_Right_Url()
    {
        var firstPath = ("session_key", "1234");
        var lastPath = ("csv", true);

        var httpMock = new Mock<HttpMessageHandler>();

        httpMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                Assert.Equal(
                    $"{HttpClientWrapper.ApiBaseUrlConfig}{HttpClientWrapper.DriverEndpoint}?{firstPath.Item1}={firstPath.Item2}&{lastPath.Item1}={lastPath.Item2}",
                    request.RequestUri!.ToString());

                _testOutputHelper.WriteLine("Http url {0}",request.RequestUri);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3])
                };
            });

        var httpClient = new HttpClient(httpMock.Object)
        {
            BaseAddress = new Uri(HttpClientWrapper.ApiBaseUrlConfig)
        };

        var sut = new HttpClientWrapper(httpClient);

        var response = await sut.GetDataAsync(HttpClientWrapper.DriverEndpoint, firstPath, lastPath);
        Assert.Equal(response, [1, 2, 3]);
    }
}
