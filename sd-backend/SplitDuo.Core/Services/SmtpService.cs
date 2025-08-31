using System.Net;
using MailKit.Net.Smtp;
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
        catch (Exception ex) when (ex is MailKit.Security.AuthenticationException)
        {
            return Result.Failure("SMTP authentication failed", HttpStatusCode.Unauthorized);
        }
        catch (Exception ex) when (ex is MailKit.Net.Smtp.SmtpCommandException)
        {
            return Result.Failure($"SMTP command failed: {ex.Message}", HttpStatusCode.BadRequest);
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException)
        {
            return Result.Failure("Failed to connect to SMTP server", HttpStatusCode.ServiceUnavailable);
        }
        catch (ParseException ex)
        {
            return Result.Failure($"Invalid email address: {ex.Message}", HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to send email: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }
}