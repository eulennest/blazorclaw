using BlazorClaw.Channels.Services;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace BlazorClaw.Server.Controllers.Admin;

[ApiController]
[Route("api/admin/whatsapp")]
public class WhatsAppAdminController : ControllerBase
{
    private readonly WhatsAppBotHostedService _whatsappService;
    private readonly ILogger<WhatsAppAdminController> _logger;

    public WhatsAppAdminController(
        WhatsAppBotHostedService whatsappService,
        ILogger<WhatsAppAdminController> logger)
    {
        _whatsappService = whatsappService;
        _logger = logger;
    }

    /// <summary>
    /// Get all current QR codes
    /// </summary>
    [HttpGet("qrcodes")]
    public IActionResult GetQRCodes()
    {
        try
        {
            var qrCodes = _whatsappService.GetCurrentQRCodes();
            return Ok(qrCodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get QR codes");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get QR code image for specific account
    /// </summary>
    [HttpGet("qrcode/{accountId}")]
    public IActionResult GetQRCodeImage(string accountId, [FromQuery] int size = 300)
    {
        try
        {
            var qrData = _whatsappService.GetQRCode(accountId);
            if (qrData == null)
            {
                return NotFound(new { error = $"No QR code found for account '{accountId}'" });
            }

            // Generate QR code image
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrData.QRCode, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(20);

            return File(qrCodeImage, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate QR code for {AccountId}", accountId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
