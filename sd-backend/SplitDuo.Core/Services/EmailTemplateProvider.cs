using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Options;

namespace SplitDuo.Core.Services;

public interface IEmailTemplateProvider
{
    Notification Render(ITemplateModel model, string language = "en");
}

public class EmailTemplateProvider(IOptions<AppOptions> appOptions, TimeProvider timeProvider) : IEmailTemplateProvider
{
    private readonly AppOptions _appOptions = appOptions.Value;
    private static readonly Assembly _assembly = typeof(EmailTemplateProvider).Assembly;
    private static readonly Regex SubjectRegex = new(@"^<!--\s*SUBJECT:\s*(.+?)\s*-->", RegexOptions.Compiled);

    public Notification Render(ITemplateModel model, string language = "en")
    {
        model.Validate();

        var key = model.Template.ToString();
        var html = LoadTemplate(key, language);

        var (subject, body) = ParseSubject(html);
        var placeholders = BuildPlaceholders(model);

        body = Substitute(body, placeholders);
        subject = Substitute(subject, placeholders);

        return new Notification
        {
            To = GetTo(model),
            Subject = subject,
            Body = body
        };
    }

    private string LoadTemplate(string key, string language)
    {
        var resourceName = $"SplitDuo.Core.EmailTemplates.{language}.{key}.html";
        var stream = _assembly.GetManifestResourceStream(resourceName);

        if (stream == null && language != "en")
        {
            resourceName = $"SplitDuo.Core.EmailTemplates.en.{key}.html";
            stream = _assembly.GetManifestResourceStream(resourceName);
        }

        if (stream == null)
        {
            throw new InvalidOperationException($"Email template not found: {key} for language '{language}'");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static (string subject, string body) ParseSubject(string html)
    {
        var match = SubjectRegex.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("Email template missing SUBJECT comment on first line");
        }

        var subject = match.Groups[1].Value;
        var body = SubjectRegex.Replace(html, "").TrimStart();

        return (subject, body);
    }

    private Dictionary<string, string> BuildPlaceholders(ITemplateModel model)
    {
        var placeholders = new Dictionary<string, string>();

        // Reflect over model public instance properties (skip Template enum)
        var properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.Name == "Template") continue;

            var value = prop.GetValue(model);
            if (value != null)
            {
                placeholders[prop.Name] = value switch
                {
                    string s => s,
                    Guid g => g.ToString(),
                    decimal d => d.ToString("F2"),
                    DateOnly d => d.ToString("MMMM d, yyyy"),
                    _ => value.ToString() ?? ""
                };
            }
        }

        // Add computed values (URLs, timestamps) that depend on model data + options
        switch (model)
        {
            case PasswordResetModel prm:
                placeholders["ResetUrl"] =
                    $"{_appOptions.BaseUrl}/reset-password?email={Uri.EscapeDataString(prm.To)}&token={Uri.EscapeDataString(prm.ResetToken)}";
                break;

            case PasswordResetSuccessModel:
                placeholders["Timestamp"] = timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
                break;

            case PasswordChangedModel:
                placeholders["Timestamp"] = timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
                break;

            case GroupInvitationModel gim:
                placeholders["AcceptUrl"] =
                    $"{_appOptions.BaseUrl}/invite/accept?token={Uri.EscapeDataString(gim.RawToken)}";
                break;

            case GroupMemberAddedModel gma:
                placeholders["GroupUrl"] = $"{_appOptions.BaseUrl}/groups/{gma.GroupGuid}";
                break;

            case ExpenseAddedModel ea:
                placeholders["GroupUrl"] = $"{_appOptions.BaseUrl}/groups/{ea.GroupGuid}";
                break;

            case ExpenseDeletedModel ed:
                placeholders["GroupUrl"] = $"{_appOptions.BaseUrl}/groups/{ed.GroupGuid}";
                break;
        }

        return placeholders;
    }

    private static string Substitute(string template, Dictionary<string, string> placeholders)
    {
        foreach (var (key, value) in placeholders)
        {
            var escaped = WebUtility.HtmlEncode(value);
            template = template.Replace($"{{{{{key}}}}}", escaped);
        }

        return template;
    }

    private static string GetTo(ITemplateModel model)
    {
        var prop = model.GetType().GetProperty("To", BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(model) as string ?? "";
    }
}
