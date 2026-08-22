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

public sealed class FileEmailSender(IOptions<EmailOptions> options, IHostEnvironment environment) : IEmailSender
{
    private readonly EmailOptions settings = options.Value;

    public async Task SendPasswordRecoveryAsync(string recipient, string displayName, string resetLink, DateTime expiresAtUtc, CancellationToken ct)
    {
        var directory = Path.IsPathRooted(settings.FileDirectory) ? settings.FileDirectory : Path.Combine(environment.ContentRootPath, settings.FileDirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"password-recovery-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(path, BuildBody(displayName, resetLink, expiresAtUtc), ct);
    }

    internal static string BuildBody(string displayName, string resetLink, DateTime expiresAtUtc)
    {
        var encoder = HtmlEncoder.Default;
        return $"""<!doctype html><html lang="es"><meta charset="utf-8"><title>Restablecer contraseña</title><body><h1>Sistema de Riego CUDEP</h1><p>Hola {encoder.Encode(displayName)},</p><p>Recibimos una solicitud para restablecer tu contraseña.</p><p><a href="{encoder.Encode(resetLink)}">Definir una nueva contraseña</a></p><p>El enlace vence a las {expiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC y solo puede utilizarse una vez.</p><p>Si no solicitaste este cambio, ignora este correo.</p></body></html>""";
    }
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions settings = options.Value;

    public async Task SendPasswordRecoveryAsync(string recipient, string displayName, string resetLink, DateTime expiresAtUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpPassword)) throw new InvalidOperationException("Falta configurar Email:SmtpPassword en el almacén de secretos.");
        using var message = new MailMessage { From = new MailAddress(settings.FromAddress, settings.FromName), Subject = "Restablece tu contraseña · Sistema de Riego CUDEP", Body = FileEmailSender.BuildBody(displayName, resetLink, expiresAtUtc), IsBodyHtml = true };
        message.To.Add(recipient);
        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort) { EnableSsl = settings.EnableSsl, UseDefaultCredentials = false, Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword) };
        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, ct);
    }
}