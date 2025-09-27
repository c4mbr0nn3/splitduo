using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class JwtOptionsSetup(IConfiguration configuration) : IConfigureOptions<JwtOptions>
{
    public void Configure(JwtOptions options)
    {
        var opt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
        if (opt == null) throw new Exception("JWT options not found");

        options.SecretKey = Environment.GetEnvironmentVariable("SD_JWT_SECRET_KEY") ?? opt.SecretKey;
        options.Issuer = Environment.GetEnvironmentVariable("SD_JWT_ISSUER") ?? opt.Issuer;
        options.Audience = Environment.GetEnvironmentVariable("SD_JWT_AUDIENCE") ?? opt.Audience;
        options.Expires = int.TryParse(Environment.GetEnvironmentVariable("SD_JWT_EXPIRES"), out var expires)
            ? expires
            : opt.Expires;
    }
}