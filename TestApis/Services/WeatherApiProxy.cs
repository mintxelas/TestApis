using System.Net.Http;
using System.Threading.Tasks;

namespace TestApis.Services
{
    public class WeatherApiProxy
    {
        private readonly HttpClient _httpClient;
        public WeatherApiProxy(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetForecast()
        {
            var response = await _httpClient.GetAsync("/weather?q=Gandia%2Ces&units=metric");
            var resultString = await response.Content.ReadAsStringAsync();
            return resultString;
        }
    }
}
