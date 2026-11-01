using HY.ApiService.Repositories;
using HY.ApiService.Services;
using SqlSugar;
using DbType = SqlSugar.DbType;

namespace HY.ApiService.Setups
{
    /// <summary>
    /// SqlSugar 配置
    /// </summary>
    public static class SqlSugarSetup
    {
        public static void AddSqlSugarSetup(this IServiceCollection services, IConfiguration configuration)
        {
            //注册上下文：AOP里面可以获取IOC对象，如果有现成框架比如Furion可以不写这一行
            services.AddHttpContextAccessor();

            // 获取连接字符串
            var conn = configuration.GetConnectionString("MySql");

            //注册SqlSugar用AddScoped
            services.AddScoped<ISqlSugarClient>(s =>
            {
                //Scoped用SqlSugarClient 
                var sqlSugar = new SqlSugarClient(new ConnectionConfig()
                {
                    DbType = DbType.MySql,
                    ConnectionString = conn,
                    IsAutoCloseConnection = true,
                },
               db =>
               {
                   //每次上下文都会执行

                   //获取IOC对象不要求在一个上下文
                   //var log=s.GetService<Log>()

                   //获取IOC对象要求在一个上下文
                   //var appServive = s.GetService<IHttpContextAccessor>();
                   //var log= appServive?.HttpContext?.RequestServices.GetService<Log>();

                   db.Aop.OnLogExecuting = (sql, pars) =>
                   {

                   };

                   // 执行错误
                   db.Aop.OnError = ex =>
                   {
                       System.Diagnostics.Debug.WriteLine(ex.Message);
                   };
               });

                return sqlSugar;
            });

            // 注册所有的 Services 和 Repositories
            services.AddSqlSugarRepositories();
            services.AddSqlSugarServices();

        }


        public static void AddSqlSugarRepositories(this IServiceCollection services)
        {
            services.AddScoped<IUserRepository, UserRepository>();

            services.AddScoped<ILoginDeviceRepository, LoginDeviceRepository>();
            services.AddScoped<ILoginTokenRepository, LoginTokenRepository>();

            services.AddScoped<IChatRepository, ChatRepository>();

            services.AddScoped<IMessageRepository, MessageRepository>();
            services.AddScoped<IMessageActionRepository, MessageActionRepository>();

            services.AddScoped<IContactRepository, ContactRepository>();
            services.AddScoped<IContactRequestRepository, ContactRequestRepository>();

            services.AddScoped<IGroupRepository, GroupRepository>();
            services.AddScoped<IGroupMemberRepository, GroupMemberRepository>();

            services.AddScoped<IMediaFileRepository, MediaFileRepository>();
            services.AddScoped<IMediaStorageRepository, MediaStorageRepository>();
            services.AddScoped<IMediaStorageVariantRepository, MediaStorageVariantRepository>();

            services.AddScoped<IPermissionRepository, PermissionRepository>();
        }

        public static void AddSqlSugarServices(this IServiceCollection services)
        {
            // 领域级
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IGroupService, GroupService>();
            services.AddScoped<IGroupMemberService, GroupMemberService>();

            // 用例级
            services.AddScoped<ILoginService, LoginService>();                      // LoginToken + LoginDevice
            services.AddScoped<IMediaService, MediaService>();                      // MediaStorage + MediaFile + MediaStorageVariant
            services.AddScoped<IContactService, ContactService>();                  // Contact + ContactRequest
            services.AddScoped<IMessageService, MessageService>();                  // Message + MessageAction

            // 权限服务
            services.AddScoped<IPermissionService, PermissionService>();
        }


    }
}
