using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using DLCS.Core.Encryption;
using DLCS.Core.Settings;
using DLCS.Mediatr.Behaviours;
using DLCS.Repository;
using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Portal.Legacy;
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
                .AddTransient<ClaimsPrincipal>(s => s.GetService<IHttpContextAccessor>().HttpContext.User)
                .AddMediatR(typeof(Startup))
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

            services.AddDbContext<DlcsContext>(opts =>
                opts.UseNpgsql(configuration.GetConnectionString("PostgreSQLConnection"))
            );

            services.AddHttpClient<DlcsClient>(client =>
            {
                var dlcsSection = configuration.GetSection("DLCS");
                var dlcsOptions = dlcsSection.Get<DlcsSettings>();

                client.BaseAddress = dlcsOptions.ApiRoot;
                client.DefaultRequestHeaders.Accept
                    .Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromMilliseconds(dlcsOptions.DefaultTimeoutMs);
            });

            services
                .AddHealthChecks()
                .AddUrlGroup(dlcsSettings.ApiRoot, "DLCS API")
                .AddDbContextCheck<DlcsContext>("DLCS-DB");
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