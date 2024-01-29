using DatabaseAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TestApis.Services;

namespace TestApis.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly Random _rnd;
        private readonly SampleDbContext _dbContext;
        private readonly WeatherApiProxy _apiProxy;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, Random rnd, SampleDbContext dbContext, WeatherApiProxy apiProxy)
        {
            _logger = logger;
            _rnd = rnd;
            _dbContext = dbContext;
            _apiProxy = apiProxy;
        }

        [HttpGet]
        public IEnumerable<WeatherForecast> Get() => Enumerable.Range(1, 5).Select(CreateForecast).ToArray();

        private WeatherForecast CreateForecast(int index)
        {
            return new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = _rnd.Next(-20, 55),
                Summary = Summaries[_rnd.Next(Summaries.Length)]
            };
        }

        [HttpGet("private")]
        [Authorize]
        public string GetPrivate() => $"Hi {User.Identity.Name}.";

        [HttpGet("url/{id}")]
        public ActionResult<string> GetUrl(int id)
        {
            var url = _dbContext.Urls.Find(id);
            if (url is null)
                return NotFound();
            return Ok(url);
        }

        [HttpGet("proxy")]
        public async Task<string> GetFromExternalService()
        {
            return await _apiProxy.GetForecast();
        }

    }
}
