using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class JwtOptionsSetup(IConfiguration configuration) : IConfigureOptions<JwtOptions>
{
    public void Configure(JwtOptions options)
    {
        var opt = configuration.GetSection("JwtOptions").Get<JwtOptions>();
        if (opt == null) throw new Exception("JWT options not found");

        options.SecretKey = Environment.GetEnvironmentVariable("SD_JWT_SECRET_KEY") ?? opt.SecretKey;
        options.Issuer = opt.Issuer;
        options.Audience = opt.Audience;
        options.Expires = opt.Expires;
    }
}