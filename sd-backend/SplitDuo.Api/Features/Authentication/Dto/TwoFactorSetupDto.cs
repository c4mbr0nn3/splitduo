namespace SplitDuo.Api.Features.Authentication.Dto;

public class TwoFactorSetupDto
{
    public string Secret { get; set; } = "";
    public string QrCodeUri { get; set; } = "";
    public List<string> BackupCodes { get; set; } = [];
}