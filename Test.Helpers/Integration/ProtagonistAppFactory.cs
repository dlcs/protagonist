using System;
using System.Collections.Generic;
using System.IO;
using Amazon.S3;
using LazyCache;
using LazyCache.Mocks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Test.Helpers.Integration
{
    /// <summary>
    /// Basic appFactory for protagonist, configuring <see cref="TestAuthHandler"/> for auth and LocalStack for aws
    /// </summary>
    /// <typeparam name="TStartup"></typeparam>
    public class ProtagonistAppFactory<TStartup> : WebApplicationFactory<TStartup>
        where TStartup: class
    {
        private readonly Dictionary<string, string> configuration = new();
        private readonly List<IDisposable> disposables = new();
        private LocalStackFixture localStack;
        private Action<IServiceCollection> configureTestServices;

        /// <summary>
        /// Specify connection string to use for dlcsContext when building services
        /// </summary>
        /// <param name="connectionString">connection string to use for dbContext - docker instance</param>
        /// <returns>Current instance</returns>
        public ProtagonistAppFactory<TStartup> WithConnectionString(string connectionString)
        {
            configuration["ConnectionStrings:PostgreSQLConnection"] = connectionString;
            return this;
        }
        
        /// <summary>
        /// Specify a configuration value to be set in appFactory
        /// </summary>
        /// <param name="key">Key of setting to update, in format ("Thumbs:ThumbsBucket")</param>
        /// <param name="value">Value to set</param>
        /// <returns>Current instance</returns>
        public ProtagonistAppFactory<TStartup> WithConfigValue(string key, string value)
        {
            configuration[key] = value;
            return this;
        }
        
        /// <summary>
        /// <see cref="LocalStackFixture"/> to use for replacing AWS services.
        /// </summary>
        /// <param name="fixture"><see cref="LocalStackFixture"/> to use.</param>
        /// <returns>Current instance</returns>
        public ProtagonistAppFactory<TStartup> WithLocalStack(LocalStackFixture fixture)
        {
            localStack = fixture;
            return this;
        }

        /// <summary>
        /// Action to call in ConfigureTestServices
        /// </summary>
        /// <returns>Current instance</returns>
        public ProtagonistAppFactory<TStartup> WithTestServices(Action<IServiceCollection> configureTestServices)
        {
            this.configureTestServices = configureTestServices;
            return this;
        }

        /// <summary>
        /// <see cref="IDisposable"/> implementation that will be disposed of alongside appfactory
        /// </summary>
        public ProtagonistAppFactory<TStartup> WithDisposable(IDisposable disposable)
        {
            disposables.Add(disposable);
            return this;
        }
        
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var projectDir = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(projectDir, "appsettings.Testing.json");

            builder
                .ConfigureAppConfiguration((context, conf) =>
                {
                    conf.AddJsonFile(configPath);
                    conf.AddInMemoryCollection(configuration);
                })
                .ConfigureTestServices(services =>
                {
                    if (configureTestServices != null)
                    {
                        configureTestServices(services);
                    }
                    
                    if (localStack != null)
                    {
                        ConfigureS3Services(services);
                    }

                    services.AddSingleton<IAppCache, MockCachingService>();
                })
                .UseEnvironment("Testing"); 
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var d in disposables)
            {
                d.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ConfigureS3Services(IServiceCollection services)
        {
            services.Remove(new ServiceDescriptor(typeof(IAmazonS3),
                a => a.GetService(typeof(IAmazonS3)), ServiceLifetime.Singleton));
            
            services.AddSingleton<IAmazonS3>(p => localStack.AmazonS3);
        }
    }
}