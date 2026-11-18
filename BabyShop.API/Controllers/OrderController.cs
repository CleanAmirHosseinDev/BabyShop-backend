using BabyShop.Application.Dtos;
using BabyShop.Application.Interfaces.Services;
using BabyShop.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BabyShop.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderService orderService, ILogger<OrderController> logger)
        {
            _orderService = orderService;
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

        private bool IsUserAdmin()
        {
            return User.IsInRole("Admin") ||
                   User.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == "Admin") ||
                   User.HasClaim(c => c.Type == "role" && c.Value == "Admin");
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<ApiResponse<List<OrderSummaryDto>>>> GetAll()
        {
            try
            {
                var orders = await _orderService.GetAllOrdersForAdminAsync();
                return Ok(new ApiResponse<List<OrderSummaryDto>> { Success = true, Data = orders });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all orders");
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "خطای سرور" });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ApiResponse<OrderDto>>> Create([FromBody] CreateOrderDto dto)
        {
            try
            {
                dto.UserId = GetCurrentUserId();
                var order = await _orderService.CreateOrderAsync(dto);
                return Ok(new ApiResponse<OrderDto> { Success = true, Data = order, Message = "سفارش با موفقیت ثبت شد" });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<OrderDto>>> Get(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);

            // فقط صاحب سفارش یا ادمین می‌تواند جزئیات را ببیند
            if (order == null || (!IsUserAdmin() && order.UserId != GetCurrentUserId()))
            {
                return NotFound(new ApiResponse<object> { Success = false, Message = "سفارش یافت نشد" });
            }

            return Ok(new ApiResponse<OrderDto> { Success = true, Data = order });
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<OrderSummaryDto>>>> GetMyOrders()
        {
            var userId = GetCurrentUserId();
            var orders = await _orderService.GetUserOrderSummariesAsync(userId);
            return Ok(new ApiResponse<List<OrderSummaryDto>> { Success = true, Data = orders });
        }

        [HttpPut("status")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateStatus([FromBody] UpdateOrderStatusDto dto)
        {
            try
            {
                var order = await _orderService.UpdateOrderStatusAsync(dto);
                return Ok(new ApiResponse<OrderDto> { Success = true, Data = order, Message = "وضعیت سفارش تغییر کرد" });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("from-basket")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<OrderDto>>> CreateOrderFromBasket([FromBody] CreateOrderFromBasketDto dto)
        {
            try
            {
                dto.UserId = GetCurrentUserId();
                var order = await _orderService.CreateOrderFromBasketAsync(dto);
                return Ok(new ApiResponse<OrderDto> { Success = true, Data = order });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }
    }
}