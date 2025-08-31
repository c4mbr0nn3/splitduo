using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SplitDuo.Core.Common;
using SplitDuo.Core.Options;

namespace SplitDuo.Core.Services;

public interface ISmtpService
{
    public Task<Result> SendEmailAsync(string email, string subject, string body);
}

public class SmtpService(IOptions<SmtpOptions> options) : ISmtpService
{
    private readonly SmtpOptions _options = options.Value;

    public async Task<Result> SendEmailAsync(string email, string subject, string body)
    {
        try
        {
            var message = new MimeMessage();
            var from = new MailboxAddress(_options.SenderName, _options.SenderAddress);
            message.From.Add(from);
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.SmtpServer, _options.SmtpPort, _options.UseSsl);
            await client.AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            return Result.Success();
        }
        catch (Exception ex) when (ex is AuthenticationException)
        {
            return Result.Unauthorized("SMTP authentication failed");
        }
        catch (Exception ex) when (ex is SmtpCommandException)
        {
            return Result.BadRequest($"SMTP command failed: {ex.Message}");
        }
        catch (Exception ex) when (ex is SocketException)
        {
            return Result.ServiceUnavailable("Failed to connect to SMTP server");
        }
        catch (ParseException ex)
        {
            return Result.BadRequest($"Invalid email address: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.InternalServerError($"Failed to send email: {ex.Message}");
        }
    }
}