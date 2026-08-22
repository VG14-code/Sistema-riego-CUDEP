using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Services;

public interface IEmailSender
{
    Task SendPasswordRecoveryAsync(string recipient, string displayName, string resetLink, DateTime expiresAtUtc, CancellationToken ct);
}

public sealed class FileEmailSender(IOptions<EmailOptions> options, IHostEnvironment environment, ILogger<FileEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions settings = options.Value;

    public async Task SendPasswordRecoveryAsync(string recipient, string displayName, string resetLink, DateTime expiresAtUtc, CancellationToken ct)
    {
        var directory = ResolveDirectory(settings.FileDirectory, environment.ContentRootPath);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName());
        await File.WriteAllTextAsync(path, BuildBody(displayName, resetLink, expiresAtUtc), ct);
        logger.LogInformation("Correo de recuperación escrito en el buzón local {MailboxFile} para {Recipient}", path, recipient);
    }

    internal static string BuildBody(string displayName, string resetLink, DateTime expiresAtUtc)
    {
        var encoder = HtmlEncoder.Default;
        return $"""<!doctype html><html lang="es"><meta charset="utf-8"><title>Restablecer contraseña</title><body><h1>Sistema de Riego CUDEP</h1><p>Hola {encoder.Encode(displayName)},</p><p>Recibimos una solicitud para restablecer tu contraseña.</p><p><a href="{encoder.Encode(resetLink)}">Definir una nueva contraseña</a></p><p>El enlace vence a las {expiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC y solo puede utilizarse una vez.</p><p>Si no solicitaste este cambio, ignora este correo.</p></body></html>""";
    }

    internal static string ResolveDirectory(string configured, string contentRoot) => Path.IsPathRooted(configured) ? configured : Path.Combine(contentRoot, configured);
    internal static string FileName() => $"password-recovery-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.html";
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, IHostEnvironment environment, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions settings = options.Value;

    public async Task SendPasswordRecoveryAsync(string recipient, string displayName, string resetLink, DateTime expiresAtUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpPassword)) throw new InvalidOperationException("Falta configurar Email:SmtpPassword en el almacén de secretos.");
        var body = FileEmailSender.BuildBody(displayName, resetLink, expiresAtUtc);
        using var message = new MailMessage { From = new MailAddress(settings.FromAddress, settings.FromName), Subject = "Restablece tu contraseña · Sistema de Riego CUDEP", Body = body, IsBodyHtml = true };
        message.To.Add(recipient);
        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword),
            Timeout = Math.Max(1, settings.SmtpTimeoutSeconds) * 1000
        };
        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, ct);
        logger.LogInformation("Servidor SMTP {SmtpHost}:{SmtpPort} aceptó el correo de recuperación desde {Sender} para {Recipient}", settings.SmtpHost, settings.SmtpPort, settings.FromAddress, recipient);

        if (environment.IsDevelopment() && settings.ArchiveSentMessages)
        {
            var directory = FileEmailSender.ResolveDirectory(settings.FileDirectory, environment.ContentRootPath);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FileEmailSender.FileName());
            await File.WriteAllTextAsync(path, body, ct);
            logger.LogInformation("Copia de verificación del correo SMTP guardada en {MailboxFile}", path);
        }
    }
}
