using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using SplitDuo.Core.Localization;

namespace SplitDuo.Core.Options.Setup;

public class RequestLocalizationOptionsSetup : IConfigureOptions<RequestLocalizationOptions>
{
    public void Configure(RequestLocalizationOptions options)
    {
        options.DefaultRequestCulture = new RequestCulture(SupportedLanguages.Default);
        options.SupportedCultures = SupportedLanguages.Cultures;
        options.SupportedUICultures = SupportedLanguages.Cultures;

        options.AddInitialRequestCultureProvider(new CustomRequestCultureProvider(async context =>
        {
            try
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    return null;

                var token = authHeader["Bearer ".Length..].Trim();
                if (string.IsNullOrEmpty(token))
                    return null;

                // Parse the JWT payload (middle segment) without full validation.
                // Full validation happens later in UseAuthentication.
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var langClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "lang")?.Value;

                if (SupportedLanguages.IsSupported(langClaim))
                    return new ProviderCultureResult(langClaim);
            }
            catch
            {
                // Any parsing error — fall through to Accept-Language / defaults
            }

            return null;
        }));
    }
}
