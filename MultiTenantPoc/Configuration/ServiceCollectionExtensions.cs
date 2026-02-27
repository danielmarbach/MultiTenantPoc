using Microsoft.Extensions.Options;

namespace MultiTenantPoc;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMultiTenantOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<PocOptions>()
            .BindConfiguration(PocOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<PocOptions>, PocOptionsValidator>();
        services.Configure<HostOptions>(configuration.GetSection("HostOptions"));

        return services;
    }
}