using BabyShop.Application.Dtos;
using BabyShop.Application.Interfaces.Services;
using BabyShop.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;

namespace BabyShop.API.Controllers;

[Authorize(Policy = "UserOnly")]
[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

        public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value
                        ?? User.FindFirst("userId")?.Value;

            if (int.TryParse(claim, out var userId) && userId > 0)
                return userId;

            throw new UnauthorizedAccessException("User ID not found in token");
        }

        private bool IsStaff() => User.IsInRole("Admin") || User.IsInRole("Manager");

        // ۱. ایجاد تراکنش پرداخت جدید
        [HttpPost]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> Create([FromBody] CreatePaymentDto dto)
        {
            try
            {
                var payment = await _paymentService.CreatePaymentAsync(dto, GetCurrentUserId(), IsStaff());
            return Ok(new ApiResponse<PaymentDto> { Success = true, Data = payment });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

        // ۲. دریافت اطلاعات پرداخت بر اساس شماره سفارش
        [HttpGet("order/{orderId}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> GetByOrder(int orderId)
    {
        var payment = await _paymentService.GetPaymentByOrderIdAsync(orderId);
        if (payment == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = $"Payment for order {orderId} not found" });

        return Ok(new ApiResponse<PaymentDto> { Success = true, Data = payment });
    }

    // ۳. تایید نهایی پرداخت (Verify) - اصلاح شده برای هماهنگی با فرانت
        [HttpPost("verify")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> Verify(int paymentId, string referenceNumber)
        {
            try
            {
                var payment = await _paymentService.VerifyPaymentAsync(paymentId, referenceNumber, GetCurrentUserId(), IsStaff());
            return Ok(new ApiResponse<PaymentDto> { Success = true, Data = payment, Message = "Payment verified" });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment");
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "خطای سرور در تایید پرداخت" });
        }
    }

    // ۴. مرجوع کردن وجه (مخصوص ادمین)
    [HttpPost("{paymentId}/refund")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Refund(int paymentId, string reason)
    {
        try
        {
            var payment = await _paymentService.RefundPaymentAsync(paymentId, reason);
            return Ok(new ApiResponse<PaymentDto> { Success = true, Data = payment, Message = "Payment refunded" });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }
}