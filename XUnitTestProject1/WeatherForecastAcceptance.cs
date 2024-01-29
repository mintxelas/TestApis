using Acheve.AspNetCore.TestHost.Security;
using Acheve.TestHost;
using ApprovalTests;
using ApprovalTests.Reporters;
using ApprovalTests.Scrubber;
using DatabaseAccess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using NSubstitute;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using TestApis.Controllers;
using TestApis.Services;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace XUnitTestProject1
{
    [UseReporter(typeof(DiffReporter))]
    public class WeatherForecastAcceptance
    {
        private readonly TestServer _server;
        private readonly Random _mockRandom;
        private readonly WireMockServer _externalApiServer;

        public WeatherForecastAcceptance()
        {
            _mockRandom = Substitute.For<Random>();
            _externalApiServer = WireMockServer.Start();
            _server = new TestServer(new WebHostBuilder()
                .ConfigureTestServices(services =>
                {
                    services.AddControllers()
                            .AddJsonOptions(options =>
                            {
                                options.JsonSerializerOptions.WriteIndented = true;
                            });
                    services.AddTransient<Random>(_ => _mockRandom);
                    services.AddAuthentication(TestServerDefaults.AuthenticationScheme)
                        .AddTestServer(options =>
                        {
                            options.NameClaimType = "name";
                        });

                    services.RemoveAll<DbContextOptions<SampleDbContext>>();
                    services.AddDbContext<SampleDbContext>(builder =>
                    { 
                        builder.UseInMemoryDatabase("testdb");
                    });

                    var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
                    configuration["OpenWeatherApi:Url"] = _externalApiServer.Urls[0];
                    configuration["OpenWeatherApi:Host"] = "some-host";
                    configuration["OpenWeatherApi:Key"] = "some-key";
                })
                .UseEnvironment("Test")
                .ConfigureAppConfiguration(builder =>
                {
                    builder.AddJsonFile("appsettings.test.json", optional: false);
                })
                .UseStartup<TestApis.Startup>()
            );
        }


        [Fact]
        public async Task CheckActualForecastContent()
        {
            GivenAForecast();
            var response = await WhenForecastIsRequested();
            await ThenForecastMatchesTheSpecification(response);
        }

        private void GivenAForecast()
        {
            _mockRandom.Next(-20, 55).Returns(0);
            _mockRandom.Next(10).Returns(0);
        }

        private async Task<HttpResponseMessage> WhenForecastIsRequested()
        {
            using var client = _server.CreateClient();
            return await client.GetAsync("weatherforecast");
        }

        private async Task ThenForecastMatchesTheSpecification(HttpResponseMessage message)
        {
            message.EnsureSuccessStatusCode();
            _mockRandom.Received().Next(-20, 55);
            _mockRandom.Received().Next(10);
            var contentString = await message.Content.ReadAsStringAsync();
            var scrubDate = ScrubberUtils.RemoveLinesContaining("\"date\"");
            Approvals.Verify(contentString, scrubDate);
        }

        [Fact]
        public async Task PassUserForAuthorizedEndpoint()
        {
            var response = await WhenRequestingTheAuthorizedEndpointWhileAuthenticated();
            await ThenEnpointRespondsAuthorized(response);
        }

        private Task<HttpResponseMessage> WhenRequestingTheAuthorizedEndpointWhileAuthenticated()
        {
            return _server.CreateHttpApiRequest<WeatherForecastController>(c => c.GetPrivate())
                .WithIdentity(new[] { new Claim("name", "testuser") })
                .GetAsync();
        }

        private async Task ThenEnpointRespondsAuthorized(HttpResponseMessage message)
        {
            message.EnsureSuccessStatusCode();
            var contentString = await message.Content.ReadAsStringAsync();
            Approvals.Verify(contentString);
        }

        [Fact]
        public async Task DenyAccessWhenUnauthorized()
        {
            var response = await WhenRequestingTheAuthorizedEndpointWhileNotAuthenticated();
            ThenEnpointRespondsUnauthorized(response);
        }

        private Task<HttpResponseMessage> WhenRequestingTheAuthorizedEndpointWhileNotAuthenticated()
        {
            return _server.CreateHttpApiRequest<WeatherForecastController>(c => c.GetPrivate())
                .GetAsync();
        }

        private void ThenEnpointRespondsUnauthorized(HttpResponseMessage message)
        {
            Assert.Equal(HttpStatusCode.Unauthorized , message.StatusCode);
        }

        [Fact]
        public async Task GetUrlFromId()
        {
            CleanupDatabase();
            GivenUrlInDatabase(1);
            var result = await WhenUrlIsRequested(1);
            await ThenTheUrlIsRetrieved(result);
        }

        private void GivenUrlInDatabase(int id)
        {
            var dbContext = _server.Services.GetRequiredService<SampleDbContext>();
            dbContext.Urls.Add(new Url
            {
                Id = id,
                Address = "http://www.google.es"
            });
            dbContext.SaveChanges();
        }

        private Task<HttpResponseMessage> WhenUrlIsRequested(int id)
        {
            return _server.CreateHttpApiRequest<WeatherForecastController>(c => c.GetUrl(id))
                .GetAsync();
        }

        private async Task ThenTheUrlIsRetrieved(HttpResponseMessage message)
        {
            message.EnsureSuccessStatusCode();
            var contentString = await message.Content.ReadAsStringAsync();
            Approvals.Verify(contentString);
        }

        [Fact]
        public async Task GetUrlNotInDatabase()
        {
            CleanupDatabase();
            GivenUrlInDatabase(1); 
            var result = await WhenUrlIsRequested(2);
            ThenTheUrlIsNotRetrieved(result);
        }

        private void ThenTheUrlIsNotRetrieved(HttpResponseMessage message)
        {
            Assert.Equal(HttpStatusCode.NotFound, message.StatusCode);
        }

        [Fact]
        public async Task ProxyWeatherForecast()
        {
            var expectedResponse = GivenAnExternalWeatherForecastApi();
            var result = await WhenTheProxiedForecastIsRequested();
            await ThenTheForecastFromTheApiIsReturned(result, expectedResponse);
        }

        private string GivenAnExternalWeatherForecastApi()
        {
            _externalApiServer.Reset();

            var request = Request.Create()
                            .WithPath("/weather")
                            .WithParam("q", "Gandia,es")
                            .WithParam("units", "metric")
                            .WithHeader("x-rapidapi-host", "some-host")
                            .WithHeader("x-rapidapi-key", "some-key")
                            .UsingGet();

            var responseContent = File.ReadAllText("example-response.txt");
            var response = Response.Create()
                            .WithBody(responseContent);

            _externalApiServer.Given(request)
                .RespondWith(response);

            return responseContent;
        }

        private Task<HttpResponseMessage> WhenTheProxiedForecastIsRequested()
        {
            return _server.CreateHttpApiRequest<WeatherForecastController>(c => c.GetFromExternalService())
                .GetAsync();
        }

        private async Task ThenTheForecastFromTheApiIsReturned(HttpResponseMessage message, string expectedResponse)
        {
            message.EnsureSuccessStatusCode();
            var contentString = await message.Content.ReadAsStringAsync();
            Assert.Equal(expectedResponse, contentString);
            
        }

        private void CleanupDatabase()
        {
            var dbContext = _server.Services.GetRequiredService<SampleDbContext>();
            dbContext.Urls.RemoveRange(dbContext.Urls);
            dbContext.SaveChanges();
        }
    }
}
