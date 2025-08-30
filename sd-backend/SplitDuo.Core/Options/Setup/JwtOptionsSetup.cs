using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;


public class JwtOptionsSetup(IConfiguration configuration) : IConfigureOptions<JwtOptions>
{
    public void Configure(JwtOptions options)
    {
        configuration.Bind(JwtOptions.SectionName, options);
    }
}
