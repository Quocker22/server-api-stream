using BackendNet.DAL;
using BackendNet.Hubs;
using BackendNet.Repositories;
using BackendNet.Repositories.IRepositories;
using BackendNet.Services;
using BackendNet.Services.IService;
using BackendNet.Setting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using System.Reflection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();

        // Configure Swagger/OpenAPI
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.AddSecurityDefinition("Authorization", new OpenApiSecurityScheme
            {
                Description = "Api key needed to access the endpoints. Authorization: Bearer xxxx",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Header,
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Authorization"
                        },
                    },
                    new string[] {}
                }
            });
        });

        // Configure CORS to allow specific origins
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
            });
        });

        // Configure settings
        builder.Services.Configure<LiveStreamDatabaseSetting>(
            builder.Configuration.GetSection("LiveStreamDatabase"));
        builder.Services.Configure<EmailSetting>(
            builder.Configuration.GetSection("EmailServerSetting"));

        builder.Services.AddSingleton<ILiveStreamDatabaseSetting>(sp =>
            sp.GetRequiredService<IOptions<LiveStreamDatabaseSetting>>().Value);
        builder.Services.AddSingleton(sp =>
            sp.GetRequiredService<IOptions<EmailSetting>>().Value);

        // Configure MongoDB context
        builder.Services.AddScoped<IMongoContext, MongoContext>();

        // Configure Redis connection
        builder.Services.AddSingleton<IConnectionMultiplexer>(cfg =>
        {
            return ConnectionMultiplexer.Connect(builder.Configuration.GetValue<string>("Redis")!);
        });

        // Configure repositories
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IVideoRepository, VideoRepository>();
        builder.Services.AddScoped<IRoomRepository, RoomRepository>();
        builder.Services.AddScoped<IChatliveRepository, ChatliveRepository>();
        builder.Services.AddScoped<IFollowRepository, FollowRepository>();
        builder.Services.AddScoped<ICourseRepository, CourseRepository>();
        builder.Services.AddScoped<ICrsContentRepository, CrsContentRepository>();

        // Configure services
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IVideoService, VideoService>();
        builder.Services.AddScoped<IRoomService, RoomService>();
        builder.Services.AddScoped<IAwsService, AwsService>();
        builder.Services.AddScoped<IStreamService, StreamService>();
        builder.Services.AddScoped<IChatliveService, ChatliveService>();
        builder.Services.AddScoped<IFollowService, FollowService>();
        builder.Services.AddScoped<IEmailService, EmailService>();
        builder.Services.AddScoped<ICourseService, CourseService>();
        builder.Services.AddScoped<ICrsContentService, CrsContentService>();

        // Configure JWT Authentication
        builder.Services.AddAuthentication(cfg =>
        {
            cfg.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            cfg.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            cfg.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(x =>
        {
            x.RequireHttpsMetadata = false;
            x.SaveToken = false;
            x.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("this is my top jwt secret key for authentication and i append it to have enough length")
                ),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
        });

        // Configure SignalR
        builder.Services.AddSignalR();
        builder.Services.AddAutoMapper(typeof(Program));

        // Additional configurations
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseCors("AllowAll");
        app.UseHttpsRedirection();

        app.UseMiddleware<JwtCookieMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseStaticFiles();

        app.MapControllers();
        app.MapHub<StreamHub>("/hub");
        app.MapHub<ChatLiveHub>("/chatHub");

        app.Run();
    }
}
