using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using Rollbar;
using Rollbar.NetCore.AspNet;
using Steeltoe.Extensions.Configuration.CloudFoundry;
using logindirector.Services;
using logindirector.Helpers;
using Amazon.SecurityToken;
using System.Linq;

namespace logindirector
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _currentEnvironment = env;
        }

        public IConfiguration _configuration { get; }
        private IWebHostEnvironment _currentEnvironment { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.ConfigureCloudFoundryOptions(_configuration);

            services.AddDefaultAWSOptions(_configuration.GetAWSOptions());
            services.AddAWSService<IAmazonSecurityTokenService>();

            // Enable Rollbar logging
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            ConfigureRollbarSingleton();

            // Enable application caching
            services.AddMemoryCache();

            // Set cookies to always be secure
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.Secure = CookieSecurePolicy.Always;
            });

            // Register any custom services we have
            services.AddScoped<IAdaptorClientServices, AdaptorClientServices>();
            services.AddScoped<ITendersClientServices, TendersClientServices>();
            services.AddScoped<IHelpers, UserHelpers>();

            // Enable Session for the app
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(15);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

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
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);

                // Do not allow the 30 minute timer to reset on requests - since we're not checking with the SSO Service each time, we want a user to re-authenticate after 30 mins
                options.SlidingExpiration = false;
            })
            .AddOAuth("SsoService", options =>
            {
                // Second, check against the external SSO Service using OAuth
                string ssoDomain = _configuration.GetValue<string>("SsoService:SsoDomain");

                // Configure the Authorize Url
                options.AuthorizationEndpoint = ssoDomain + _configuration.GetValue<string>("SsoService:RoutePaths:AuthorizePath");

                // Set the service scopes of our OpenIdConnect Auth requests
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                // Configure what we want the callback URL to be - will be auto intercepted and processed by the system
                options.CallbackPath = new PathString(_configuration.GetValue<string>("SsoService:RoutePaths:CallbackPath"));

                // Set the Client ID and Client Secret required for authorization operations
                options.ClientId = _configuration.GetValue<string>("SsoService:ClientId");
                options.ClientSecret = _configuration.GetValue<string>("SsoService:ClientSecret");

                // Configure the Token Request Url
                options.TokenEndpoint = ssoDomain + _configuration.GetValue<string>("SsoService:RoutePaths:TokenPath");

                // Configure the Access Denied Path
                options.AccessDeniedPath = _configuration.GetValue<string>("UnauthorisedDisplayPath");

                // We don't access the adaptor service here - we can't get to external API clients here.  But we do need to decode and store the user email so that we can access it later
                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = context => 
                    { 
                        // Use a Jwt Decoder to decode the access token, and fetch the "sub" value
                        JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                        JwtSecurityToken tokenValues = handler.ReadJwtToken(context.AccessToken);

                        // Save the "sub" value to our Claims as the Email value
                        List<Claim> userClaims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Email, tokenValues.Subject)
                        };

                        // Save the "sid" value to our Claims as the SID value
                        Claim sessionIdClaim = tokenValues.Claims.FirstOrDefault(p => p.Type == "sid");

                        if (sessionIdClaim != null && !string.IsNullOrWhiteSpace(sessionIdClaim.Value))
                        {
                            userClaims.Add(new Claim(ClaimTypes.Sid, sessionIdClaim.Value));
                        }

                        // We also need to fetch "session_start" from the raw token response
                        if (context.TokenResponse != null && context.TokenResponse.Response != null)
                        {
                            string sessionState = context.TokenResponse.Response.RootElement.GetProperty("session_state").ToString();

                            if (!string.IsNullOrWhiteSpace(sessionState))
                            {
                                // Save the session state to our Claims in the Hash value
                                userClaims.Add(new Claim(ClaimTypes.Hash, sessionState));
                            }
                        }

                        // Save the token to our Claims in the Authentication value
                        userClaims.Add(new Claim(ClaimTypes.Authentication, context.AccessToken));

                        ClaimsIdentity appIdentity = new ClaimsIdentity(userClaims);
                        context.Principal.AddIdentity(appIdentity);

                        return Task.CompletedTask;
                    },
                    OnAccessDenied = context =>
                    {
                        RollbarLocator.RollbarInstance.Error("Access Denied by .NET OAuth middleware");

                        context.HandleResponse();
                        context.Response.Redirect(_configuration.GetValue<string>("UnauthorisedDisplayPath"));
                        return Task.FromResult(0);
                    },
                    OnRemoteFailure = context =>
                    {
                        RollbarLocator.RollbarInstance.Error("Failure within SSO Service - user probably doesn't have the correct role to use Login Director");

                        // Log more detail of the errors / responses in this situation
                        if (context.Failure != null)
                        {
                            RollbarLocator.RollbarInstance.Error(context.Failure);
                        }

                        if (context.Result != null)
                        {
                            RollbarLocator.RollbarInstance.Error(context.Result);
                        }

                        context.HandleResponse();
                        context.Response.Redirect(_configuration.GetValue<string>("UnauthorisedDisplayPath"));
                        return Task.FromResult(0);
                    }
                };
            });

            services.AddRollbarLogger(loggerOptions =>
            {
                loggerOptions.Filter =
                  (loggerName, loglevel) => loglevel >= LogLevel.Trace;
            });

            services.AddControllersWithViews(options => {
                options.Filters.Add<Filters.ViewBagActionFilter>();
            });
            services.AddMiniProfiler(options => options.RouteBasePath = "/profiler");

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.Use((context, next) =>
            {
                context.Request.Scheme = "https";
                return next();
            });

            if (env.IsDevelopment())
            {
                app.UseMiniProfiler();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(_configuration.GetValue<string>("UnauthorisedDisplayPath"));
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // Enable SSO Authentication for the application.  Set authenticate BEFORE authorization to prevent redirect looping
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSession();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Request}/{action=Index}/{id?}");
            });
        }

        // Configures the Rollbar singleton notifier
        private void ConfigureRollbarSingleton()
        {
            string rollbarAccessToken = _configuration.GetValue<string>("Rollbar:AccessToken");
            string rollbarEnvironment = _currentEnvironment.EnvironmentName;

            RollbarLocator.RollbarInstance.Configure(new RollbarLoggerConfig(rollbarAccessToken, rollbarEnvironment));
        }
    }
}
