using BabyShop.Application.Dtos;
using BabyShop.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace BabyShop.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var result = await _inventoryService.GetAllInventoriesAsync();
            return Ok(new { success = true, data = result });
        }

        [HttpPut("increase")]
        [Authorize(Policy = "ManagerOnly")]
        public async Task<IActionResult> Increase([FromQuery] int productId, [FromQuery] string? ageRange, [FromQuery] int quantity)
        {
            try
            {
                var safeAgeRange = string.IsNullOrEmpty(ageRange) ? "0-3" : ageRange;
                var result = await _inventoryService.IncreaseStockAsync(productId, safeAgeRange, quantity);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("decrease")]
        [Authorize(Policy = "ManagerOnly")]
        public async Task<IActionResult> Decrease([FromQuery] int productId, [FromQuery] string? ageRange, [FromQuery] int quantity)
        {
            try
            {
                // حالا بک‌اند سایز را دریافت می‌کند و از همان سایز کم می‌کند!
                var safeAgeRange = string.IsNullOrEmpty(ageRange) ? "0-3" : ageRange;
                var result = await _inventoryService.DecreaseStockAsync(productId, safeAgeRange, quantity);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                // رفع ارور 500 در صورت کمبود موجودی
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "ManagerOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _inventoryService.DeleteInventoryAsync(id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}