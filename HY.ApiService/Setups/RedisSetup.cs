using HY.ApiService.Services;
using SqlSugar;
using StackExchange.Redis;
using System.Runtime.InteropServices;

namespace HY.ApiService.Setups
{
    public static class RedisSetup
    {
        public static void AddRedisSetup(this IServiceCollection services, IConfiguration configuration)
        {
            // 获取连接字符串
            var conn = configuration.GetConnectionString("Redis")!;

            var multiplexer = ConnectionMultiplexer.Connect(conn);

            services.AddSingleton<IConnectionMultiplexer>(multiplexer);

            services.AddHostedService<RedisTimeoutService>();

            services.AddRedisServices();
        }


        public static void AddRedisServices(this IServiceCollection services)
        {
            // 在这里注册使用 Redis 的服务，例如：
            services.AddSingleton<IRedisBaseService, RedisBaseService>();
            services.AddSingleton<IRedisTokenService, RedisTokenService>();
            services.AddSingleton<IRedisConnectionService, RedisConnectionService>();
            services.AddSingleton<IRedisCallService, RedisCallService>();
            services.AddSingleton<IRedisFileService, RedisFileService>();
        }
    }
}