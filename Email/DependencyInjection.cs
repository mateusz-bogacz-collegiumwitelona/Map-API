using Email.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Interfaces;

namespace Email
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<SmtpOptions>()
                .Bind(configuration.GetSection(SmtpOptions.SectionName))
                .Validate(SmtpOptionsValidation.IsValid, SmtpOptionsValidation.ErrorMessage)
                .ValidateOnStart();

            services.AddScoped<IEmailService, EmailService>();

            return services;
        }
    }
}
