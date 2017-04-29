﻿using System;
using System.Collections.Generic;
using CleanKludge.Core.Modules;
using CleanKludge.Data.File.Modules;
using CleanKludge.Data.Git.Modules;
using CleanKludge.Server.Authorization.Modules;
using CleanKludge.Server.Filters;
using CleanKludge.Services.Modules;
using LightInject;
using LightInject.Microsoft.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace CleanKludge.Server
{
    public class Startup : IStartup
    {
        public IHostingEnvironment HostingEnvironment { get; }
        public IConfigurationRoot Configuration { get; }

        public Startup(IHostingEnvironment hostingEnvironment, ILoggerFactory loggerFactory)
        {
            HostingEnvironment = hostingEnvironment;

            Configuration = new ConfigurationBuilder()
                .SetBasePath(hostingEnvironment.ContentRootPath)
                .AddInMemoryCollection(new[] { new KeyValuePair<string, string>("BasePath", hostingEnvironment.ContentRootPath) })
                .AddJsonFile("appsettings.json", false, true)
                .AddEnvironmentVariables()
                .Build();

            var minimumLogLevel = Configuration.GetValue("MinimumLogLevel", LogEventLevel.Error);

           var loggingConfigurtion = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Information()
                .WriteTo.RollingFile(new JsonFormatter(), "logs/log-{Date}.log", minimumLogLevel, 10485760, 2);

            if(Configuration.GetValue("EnableConsoleLogging", false))
                loggingConfigurtion.WriteTo.LiterateConsole(minimumLogLevel);

            Log.Logger = loggingConfigurtion.CreateLogger();
            loggerFactory.AddSerilog();
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.TryAddSingleton(Configuration);
            services.TryAddSingleton(Log.Logger);

            services.AddOptions();
            services.AddCoreServices(Configuration);
            services.AddFileServices();
            services.AddGitServices(Configuration);
            services.AddSevices(Configuration);
            services.AddResponseCaching();
            services.AddMvc(options =>
            {
                options.CacheProfiles.Add("Default", new CacheProfile
                {
                    Location = ResponseCacheLocation.Any,
                    NoStore = false,
                    VaryByQueryKeys = new []{ "groupBy" },
                    Duration = 60
                });
                
                options.Filters.Add(new ResponseCacheAttribute { CacheProfileName = "Default" });
                options.Filters.Add(new SiteVersionAttribute(Configuration));
            });

            services.AddAuthorizations(Configuration);

            return new ServiceContainer()
                .CreateServiceProvider(services);
        }

        public void Configure(IApplicationBuilder applicationBuilder)
        {
            if(HostingEnvironment.IsDevelopment())
                applicationBuilder.UseDeveloperExceptionPage();
            else
            {
                applicationBuilder.UseExceptionHandler("/error");
                applicationBuilder.LoadContent();
            }

            applicationBuilder.UseResponseCaching();
            applicationBuilder.UseStatusCodePagesWithRedirects("/error/{0}");
            applicationBuilder.UseStaticFiles();
            applicationBuilder.UseMvc();
        }
    }
}