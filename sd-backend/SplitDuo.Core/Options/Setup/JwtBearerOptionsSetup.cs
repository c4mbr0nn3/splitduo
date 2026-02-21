using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace SplitDuo.Core.Options.Setup;

public class JwtBearerOptionsSetup(IOptions<JwtOptions> jwtOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public void Configure(JwtBearerOptions options) =>
        Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SecretKey))
            throw new InvalidOperationException("JWT SecretKey is not configured.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidAudience = _jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtOptions.SecretKey))
        };
    }
}