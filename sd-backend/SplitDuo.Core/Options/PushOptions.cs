namespace SplitDuo.Core.Options;

public class PushOptions
{
    public const string SectionName = "Push";
    public const string DefaultSubject = "mailto:admin@splitduo.local";
    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;
    public string Subject { get; set; } = DefaultSubject;

    /// <summary>
    /// Whether any key was supplied at all. Distinct from <see cref="IsConfigured"/> so
    /// startup logging can tell "push turned off" from "push misconfigured".
    /// </summary>
    public bool HasAnyKey =>
        !string.IsNullOrWhiteSpace(VapidPublicKey) || !string.IsNullOrWhiteSpace(VapidPrivateKey);

    /// <summary>
    /// Push is usable only with a complete pair whose public key is an actual VAPID key.
    /// A typo'd or truncated key otherwise looks configured: the UI enables the toggle,
    /// the browser accepts the subscription, and every send fails afterwards. Treating it
    /// as unconfigured keeps the feature honestly disabled instead.
    /// </summary>
    public bool IsConfigured =>
        VapidKeyFormat.LooksLikePublicKey(VapidPublicKey) && !string.IsNullOrWhiteSpace(VapidPrivateKey);
}