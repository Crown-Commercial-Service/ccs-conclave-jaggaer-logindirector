using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using logindirector.Services;

namespace logindirector
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Set cookies to always be secure
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.Secure = CookieSecurePolicy.Always;
            });

            // Register any custom services we have
            services.AddScoped<IAdaptorClientServices, AdaptorClientServices>();

            // Configure our Authentication setup
            services.AddAuthentication(options =>
            {
                // Try to authenticate from cookies by default - this will be set after an initial authentication
                options.DefaultAuthenticateScheme = "CookieAuth";
                options.DefaultSignInScheme = "CookieAuth";

                // If there's no cookie set, use the "SsoService" path configured below to enforce authentication
                options.DefaultChallengeScheme = "SsoService";
            })
            .AddCookie("CookieAuth", options =>
            {
                // First check should be against the cookies for an active session.  Make sure cookies expire after 30 mins rather than hanging round forever
                options.ExpireTimeSpan = System.TimeSpan.FromMinutes(30);

                // Do not allow the 30 minute timer to reset on requests - since we're not checking with the SSO Service each time, we want a user to re-authenticate after 30 mins
                options.SlidingExpiration = false;
            })
            .AddOAuth("SsoService", options =>
            {
                // Second, check against the external SSO Service using OAuth
                string ssoDomain = Configuration.GetValue<string>("SsoService:SsoDomain");

                // Configure the Authorize Url
                options.AuthorizationEndpoint = ssoDomain + Configuration.GetValue<string>("SsoService:RoutePaths:AuthorizePath");

                // Set the service scopes of our OpenIdConnect Auth requests
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                // Configure what we want the callback URL to be - will be auto intercepted and processed by the system
                options.CallbackPath = new PathString(Configuration.GetValue<string>("SsoService:RoutePaths:CallbackPath"));

                // Set the Client ID and Client Secret required for authorization operations
                options.ClientId = Configuration.GetValue<string>("SsoService:ClientId");
                options.ClientSecret = Configuration.GetValue<string>("SsoService:ClientSecret");

                // Configure the Token Request Url
                options.TokenEndpoint = ssoDomain + Configuration.GetValue<string>("SsoService:RoutePaths:TokenPath");

                // We don't access the adaptor service here - we can't get to external API clients here.  But we do need to decode and store the user email so that we can access it later
                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        // Use a Jwt Decoder to decode the access token, and fetch the "sub" value
                        JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                        JwtSecurityToken tokenValues = handler.ReadJwtToken(context.AccessToken);

                        // Save the "sub" value to our Claims as the Email value
                        List<Claim> userClaims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Email, tokenValues.Subject)
                        };

                        ClaimsIdentity appIdentity = new ClaimsIdentity(userClaims);
                        context.Principal.AddIdentity(appIdentity);
                    }
                };
            });

            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // Enable SSO Authentication for the application.  Set authenticate BEFORE authorization to prevent redirect looping
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
