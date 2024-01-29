using DatabaseAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using TestApis.Controllers;
using TestApis.Services;

namespace TestApis
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddTransient<Random>();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            services.AddDbContext<SampleDbContext>(builder =>
            {
                builder.UseSqlite(Configuration["ConnectionStrings:Sql"]);                
            });
            services.AddHttpClient<WeatherApiProxy>(httpClient =>
            {
                httpClient.BaseAddress = new Uri(Configuration["OpenWeatherApi:Url"]);
                httpClient.DefaultRequestHeaders.Add("x-rapidapi-host", Configuration["OpenWeatherApi:Host"]);
                httpClient.DefaultRequestHeaders.Add("x-rapidapi-key", Configuration["OpenWeatherApi:Key"]);
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, SampleDbContext dbContext)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            dbContext.Database.EnsureCreated();

            app.UseRouting();

            app.UseAuthentication()
               .UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
