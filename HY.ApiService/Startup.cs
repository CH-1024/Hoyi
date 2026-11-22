using HY.ApiService.Hubs;
using HY.ApiService.Setups;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.IdentityModel.Tokens;
using System.Configuration;
using System.Text;

namespace HY.ApiService
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // 配置服务容器
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMapperSetup();

            // Add services to the container.
            services.AddControllersSetup();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            services.AddOpenApi();

            // 配置 SqlSugar
            services.AddSqlSugarSetup(Configuration);

            // 配置 Redis 
            services.AddRedisSetup(Configuration);

            // 配置 CORS
            services.AddCorsSetup();

            // 配置身份验证
            services.AddAuthenticationSetup();

            // 配置授权
            services.AddAuthorizationSetup();

            services.AddSignalRSetup(Configuration);
        }

        // 配置 HTTP 请求管道
        public void Configure(WebApplication app, IWebHostEnvironment env)
        {
            // 配置管道中间件（按顺序！）
            if (app.Environment.IsDevelopment())
            {
                // 添加请求响应日志中间件
                app.Use(async (context, next) =>
                {
                    //Console.WriteLine($"收到请求: {context.Request.Path}");
                    await next();
                    //Console.WriteLine($"响应状态码: {context.Response.StatusCode}");
                });

                app.MapOpenApi();
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();                                  // 重定向到 HTTPS
            app.UseStaticFiles();                                       // 如果提供静态文件
            app.UseRouting();                                           // 开始路由
            app.UseCors("AllowAll");                                    // 应用 CORS 策略
            app.UseAuthentication();                                    // 身份验证
            app.UseAuthorization();                                     // 授权检查
            app.MapControllers();                                       // 映射控制器端点
            app.MapHub<ChatHub>("/chatHub").RequireAuthorization();     // 映射 SignalR 集线器端点

        }
    }
}
