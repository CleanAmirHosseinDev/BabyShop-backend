using BabyShop.Application.Dtos;
using BabyShop.Application.Dtos.Auth;
using BabyShop.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BabyShop.API.Controllers;

// مسیرهای احراز هویت سقف سخت‌گیرانهٔ درخواست دارند تا کد ۶ رقمی قابل brute-force نباشد.
[EnableRateLimiting("auth")]
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<object>>> Register(RegisterDto dto)
    {
        try
        {
            var result = await _authService.RegisterAsync(dto);

            if (!result.Success)
                return BadRequest(new ApiResponse<object> { Success = false, Message = result.Message });

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = result.Message,
                Data = new { result.VerificationCode }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در ثبت‌نام کاربر");
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "خطای سرور" });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<TokenDto>>> Login(LoginDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto);

            if (!result.Success)
                return BadRequest(new ApiResponse<object> { Success = false, Message = result.Message });

            return Ok(new ApiResponse<TokenDto>
            {
                Success = true,
                Message = result.Message,
                Data = result.Token
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در ورود کاربر");
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "خطای سرور" });
        }
    }

    [HttpPost("verify-phone")]
    public async Task<ActionResult<ApiResponse<TokenDto>>> VerifyPhone(VerifyPhoneDto dto)
    {
        try
        {
            var result = await _authService.VerifyPhoneAsync(dto);

            if (!result.Success)
                return BadRequest(new ApiResponse<object> { Success = false, Message = result.Message });

            return Ok(new ApiResponse<TokenDto>
            {
                Success = true,
                Message = result.Message,
                Data = result.Token
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در تأیید شماره موبایل");
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "خطای سرور" });
        }
    }

    [HttpGet("check-phone/{phoneNumber}")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckPhoneExists(string phoneNumber)
    {
        var exists = await _authService.CheckPhoneExistsAsync(phoneNumber);
        return Ok(new ApiResponse<bool> { Success = true, Data = exists });
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<string>>> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var result = await _authService.ForgotPasswordAsync(dto);

        if (!result.Success)
            return BadRequest(new ApiResponse<object> { Success = false, Message = result.Message });

        return Ok(new ApiResponse<string> { Success = true, Message = result.Message, Data = result.VerificationCode });
    }

    [HttpPost("verify-reset-code")]
    public async Task<ActionResult<ApiResponse<bool>>> VerifyResetCode([FromBody] VerifyResetCodeDto dto)
    {
        var result = await _authService.VerifyResetCodeAsync(dto);

        if (!result.Success)
            return BadRequest(new ApiResponse<object> { Success = false, Message = result.Message });

        return Ok(new ApiResponse<bool> { Success = true, Message = result.Message, Data = true });
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var result = await _authService.ResetPasswordAsync(dto);

        if (!result.Success)
            return BadRequest(new ApiResponse<object> { Success = false, Message = result.Message });

        return Ok(new ApiResponse<bool> { Success = true, Message = result.Message, Data = true });
    }
}