using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using Amazon.S3;
using API.Client;
using DLCS.AWS.S3;
using DLCS.Core.Encryption;
using DLCS.Core.Settings;
using DLCS.Mediatr.Behaviours;
using DLCS.Model.Spaces;
using DLCS.Repository;
using DLCS.Repository.Spaces;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Portal.Behaviours;
using Portal.Settings;

namespace Portal
{
    public class Startup
    {
        private readonly IConfiguration configuration;
        
        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<PortalSettings>(configuration.GetSection("Portal"));
            services.Configure<DlcsSettings>(configuration.GetSection("DLCS"));
            var dlcsSettings = configuration.GetSection("DLCS").Get<DlcsSettings>();
            
            services.AddRazorPages(opts =>
            {
                opts.Conventions.AllowAnonymousToFolder("/Account");
                opts.Conventions.AllowAnonymousToPage("/AccessDenied");
                opts.Conventions.AllowAnonymousToPage("/Error");
                opts.Conventions.AllowAnonymousToPage("/Index");
                opts.Conventions.AllowAnonymousToPage("/Features");
                opts.Conventions.AllowAnonymousToPage("/About");
                opts.Conventions.AllowAnonymousToPage("/Pricing");
                opts.Conventions.AllowAnonymousToPage("/Signup");
                opts.Conventions.AuthorizeFolder("/Admin", "Administrators");
            });

            // Add auth to everywhere - with the exception of those configured in AddRazorPages
            services.AddAuthorization(opts =>
            {
                opts.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                opts.AddPolicy("Administrators",
                    policy => policy.RequireClaim(ClaimTypes.Role, ClaimsPrincipalUtils.Roles.Admin));
            });
            
            services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(opts =>
                {
                    opts.AccessDeniedPath = new PathString("/AccessDenied");
                });

            services
                .AddHttpContextAccessor()
                .AddSingleton<IEncryption, SHA256>()
                .AddSingleton<DeliveratorApiAuth>()
                .AddTransient<ClaimsPrincipal>(s => s.GetService<IHttpContextAccessor>().HttpContext.User)
                .AddMediatR(typeof(Startup))
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(AuditBehaviour<,>))
                .AddAWSService<IAmazonS3>()
                .AddSingleton<IBucketReader, S3BucketReader>()
                .AddSingleton<IBucketWriter, S3BucketWriter>()
                .AddTransient<ISpaceRepository, SpaceRepository>(); // This shouldn't be here... use API

            services.AddDlcsContext(configuration);

            services.AddHttpClient<IDlcsClient, DlcsClient>(GetHttpClientSettings);
            services.AddHttpClient<AdminDlcsClient>(GetHttpClientSettings);

            services
                .AddHealthChecks()
                .AddUrlGroup(dlcsSettings.ApiRoot, "DLCS API")
                .AddDbContextCheck<DlcsContext>("DLCS-DB");
        }

        private void GetHttpClientSettings(HttpClient client)
        {
            var dlcsSection = configuration.GetSection("DLCS");
            var dlcsOptions = dlcsSection.Get<DlcsSettings>();

            client.BaseAddress = dlcsOptions.ApiRoot;
            client.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "DLCS-Portal-Protagonist");
            client.Timeout = TimeSpan.FromMilliseconds(dlcsOptions.DefaultTimeoutMs);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseCookiePolicy(new CookiePolicyOptions {MinimumSameSitePolicy = SameSiteMode.Strict});

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
                endpoints.MapHealthChecks("/ping").AllowAnonymous();
            });
        }
    }
}