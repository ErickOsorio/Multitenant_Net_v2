using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using TentiaCloud.WebAPI.Helpers;
using TentiaCloud.WebAPI.Models.Db;
using TentiaCloud.WebAPI.Repository;
using TentiaCloud.WebAPI.Repository.IRepository;


namespace TentiaCloud.WebAPI
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
            // Repositories
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IInventoryRepository, InventoryRepository>();
            services.AddScoped<ISalesAnalysisRepo, SalesAnalisysRepo>();

            services.AddControllers();

            services.AddCors();

            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = (int)HttpStatusCode.TemporaryRedirect;
                options.HttpsPort = 443;
            });

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddDbContext<DBContext>((serviceProvider, dbContextBuilder) =>
            {
                var connectionStringPlaceHolder = Configuration.GetConnectionString("DefaultConnection");
                var serverVersion = new MySqlServerVersion(new Version(5, 7, 32));
                var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
                var dbName = httpContextAccessor.HttpContext.Request.Headers["tenantId"].FirstOrDefault();
                var connectionString = connectionStringPlaceHolder.Replace("{dbName}", Configuration["TentiaCloud:Prefix"] + dbName);
                dbContextBuilder.UseMySql(connectionString, serverVersion);
            });

            // configure basic authentication
            services.AddAuthentication("BasicAuthentication")
                .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc(Configuration["APIDoc:Version"], new OpenApiInfo
                {
                    Title = Configuration["APIDoc:Title"],
                    Version = Configuration["APIDoc:Version"],
                    Description = Configuration["APIDoc:Description"],
                    Contact = new OpenApiContact { Name = Configuration["APIDoc:CompanyName"], Email = Configuration["APIDoc:Email"], Url = new Uri(Configuration["APIDoc:URL"]) }
                });
                c.AddSecurityDefinition("basic", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "basic",
                    In = ParameterLocation.Header,
                    Description = "Basic Authorization header using the Bearer scheme."
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                          new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "basic"
                                }
                            },
                            new string[] {}
                    }
                });

                c.OperationFilter<AddRequiredHeaderParameter>();
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);

            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web API Tentia Cloud");
                c.RoutePrefix = string.Empty;
                c.InjectStylesheet("/swagger-ui/custom.css");
                c.InjectJavascript("/swagger-ui/custom.js", "text/javascript");
                c.DocumentTitle = Configuration["APIDoc:Title"];
            });
            app.UseStaticFiles();// Enable use files from wwwroot


            app.UseRouting();

            app.UseCors(x => x
                .AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(origin => true) // allow any origin
                .AllowCredentials()); // allow credentials

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
