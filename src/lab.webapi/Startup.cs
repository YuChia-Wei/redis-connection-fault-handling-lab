using Asp.Versioning;
using Asp.Versioning.Conventions;
using HealthChecks.Redis;
using lab.common.HealthChecker;
using lab.repository.Entities;
using lab.repository.Implements;
using lab.repository.Interfaces;
using lab.service.Implements;
using lab.service.Interfaces;
using lab.webapi.Infrastructure.BackgroundServices;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Retry;

namespace lab.webapi;

/// <summary>
/// Startup
/// </summary>
public class Startup
{
    private readonly string? _redisConnectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public Startup(IConfiguration configuration)
    {
        this.Configuration = configuration;
        this._redisConnectionString = this.Configuration.GetConnectionString("Redis");
    }

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    private IConfiguration Configuration { get; }

    /// <summary>
    /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="env">The env.</param>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // API 站台不建議在系統上強轉 https，可能導致預期外行為
        // REF: https://docs.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-6.0&tabs=visual-studio
        // app.UseHttpsRedirection();

        app.UseRouting();

        app.UseSwagger();

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "Sample WebService V1");
        });

        app.UseReDoc(options =>
        {
            options.SpecUrl("/swagger/v1/swagger.json");
            options.RoutePrefix = "redoc";
            options.DocumentTitle = "Sample WebService V1";
        });

        app.UseHealthChecks("/health");

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDefaultControllerRoute();
            endpoints.MapControllers();
        });
    }

    /// <summary>
    /// This method gets called by the runtime. Use this method to add services to the container.
    /// </summary>
    /// <param name="services">The services.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        // url 小寫顯示
        services.AddRouting(options => options.LowercaseUrls = true);

        services.AddControllers();

        services.AddApiVersioning(options =>
                {
                    options.ReportApiVersions = true;
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                })
                .AddMvc(options => options.Conventions.Add(new VersionByNamespaceConvention()))
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

        // Redis Register
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = this._redisConnectionString;
            options.InstanceName = "ConnectionHealthChecking.Sample:sample:";
        });

        //記憶體快取只保留 1 小時
        services.AddMemoryCache(options =>
        {
            options.ExpirationScanFrequency = TimeSpan.FromHours(1);
            options.CompactionPercentage = 0.02d;
        });

        // Swagger Register
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = $"{AppDomain.CurrentDomain.FriendlyName} V1",
                Version = "v1"
            });

            // Set the comments path for the Swagger JSON and UI.
            var basePath = AppContext.BaseDirectory;
            var xmlFiles = Directory.EnumerateFiles(basePath, "*.xml", SearchOption.TopDirectoryOnly);

            foreach (var xmlFile in xmlFiles)
            {
                options.IncludeXmlComments(xmlFile);
            }
        });

        services.AddScoped<IMemberService, MemberService>();

        services.AddScoped<IMemberRepository, MemberRepository>();

        //MemberInRedisCache 為主要的資料來源物件
        //MemberInMemoryCache 為裝飾 MemberInRedisCache 的裝飾器物件
        services.AddScoped<ICache<Member>, MemberInRedisCache>()
                .Decorate<ICache<Member>, MemberInMemoryCache>();

        // 健康監控設定
        services.AddHealthChecks();

        services.AddResiliencePipeline<string, HealthStatus>("redis-health-check-retry-pipeline", pipelineBuilder =>
        {
            pipelineBuilder.AddFallback(new FallbackStrategyOptions<HealthStatus>
                           {
                               ShouldHandle = arguments => arguments.Outcome switch
                               {
                                   { Exception: Exception } => PredicateResult.True(),
                                   { Result: HealthStatus.Unhealthy } => PredicateResult.True(),
                                   _ => PredicateResult.False()
                               },
                               FallbackAction = _ => Outcome.FromResultAsValueTask(HealthStatus.Unhealthy)
                           })
                           .AddRetry(new RetryStrategyOptions<HealthStatus>
                           {
                               //最高重式次數
                               MaxRetryAttempts = 3,
                               //重試時的等待時間
                               BackoffType = DelayBackoffType.Exponential,
                               UseJitter = false,
                               //預設延遲 2 秒，配合 DelayBackoffType.Exponential + MaxRetryAttempts 設定，最高等待 8 秒
                               Delay = TimeSpan.FromSeconds(2),
                               ShouldHandle = arguments => arguments.Outcome switch
                               {
                                   { Exception: Exception } => PredicateResult.True(),
                                   _ => PredicateResult.False()
                               }
                           })
                           .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HealthStatus>
                           {
                               ShouldHandle = arguments => arguments.Outcome switch
                               {
                                   { Exception: Exception } => PredicateResult.True(),
                                   _ => PredicateResult.False()
                               },
                               //熔斷後停止多久
                               BreakDuration = TimeSpan.FromSeconds(10)
                           });
        });

        // Redis Health Check Hosting Service
        services.AddSingleton<RedisHealthCheck>(o => new RedisHealthCheck(this._redisConnectionString));
        services.AddSingleton<IRedisHealthStatusProvider, RedisHealthStatusProvider>();
        services.AddHostedService<RedisHealthCheckBackgroundService>(); // Redis 連線狀態檢查
    }
}