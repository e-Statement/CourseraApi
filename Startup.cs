using System;
using System.IO;
using System.Reflection;
using AutoMapper;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Serilog;
using Server.Managers;
using Server.Managers.Interfaces;
using Server.Profiles;
using server.Repository;
using Server.Repository;
using Server.Repository.Interfaces;
using Server.Services;
using Server.Settings;

namespace Server
{
    public class Startup
    {
        private readonly string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
             services
                .AddFluentMigratorCore()
                .ConfigureRunner(rb => rb
                    .AddMySql5()
                    .WithGlobalConnectionString(Configuration.GetValue<string>("ConnectionString"))
                    .ScanIn(Assembly.GetExecutingAssembly()).For.All())
                .AddLogging(lb => lb.AddFluentMigratorConsole());

             services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo {Title = "Server", Version = "v1"}); 
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
                
            });


            //Репозитории
            services
                .AddTransient<IStudentRepository, StudentRepository>()
                .AddTransient<ISpecializationRepository, SpecializationRepository>()
                .AddTransient<ICourseRepository, CourseRepository>()
                .AddTransient<IAssignmentRepository, AssignmentRepository>()
                .AddTransient<IFileRepository, FileRepository>()
                .AddSingleton<UserRepository, UserRepository>();

            //Managers
            services
                .AddTransient<ICsvParserManager, CsvParserManager>()
                .AddTransient<IUploadManager, UploadManager>()
                .AddTransient<IDataManager, DataManager>()
                .AddTransient<IUnloadManager, UnloadManager>();
            
            //Настройки
            services
                .AddTransient<IAppSettings, AppSettings>();
            
            //WorksheetAppenders
            services
                .AddTransient<CoursesWorksheetAppender>()
                .AddTransient<SpecializationWorksheetAppender>();

            var mapperConfig = new MapperConfiguration(mc =>
            {
                mc.AddProfile(new MainProfile());
            });
            var mapper = mapperConfig.CreateMapper();
            services.AddSingleton(mapper);

            services.AddCors(options =>
            {
                options.AddPolicy("foo",
                    builder =>
                    {
                        // Not a permanent solution, but just trying to isolate the problem
                        //кажется, уже не нужно
                        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                    });
            });

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = new PathString("/Auth/Login");
                });
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseStaticFiles();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger(c =>
                {
                    c.SerializeAsV2 = true;
                });

                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Server v1"));
            }
            
            
            
            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors("foo");

            
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

            app.UseSerilogRequestLogging();
            
            using var scope = app.ApplicationServices.CreateScope();
            var migrator = scope.ServiceProvider.GetService<IMigrationRunner>();
            migrator?.MigrateUp();
        }
    }
}