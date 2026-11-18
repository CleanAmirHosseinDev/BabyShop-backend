using BabyShop.Application.Dtos;
using BabyShop.Application.Interfaces.Services;
using BabyShop.Core.Entities;
using BabyShop.Core.Exceptions;
using BabyShop.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BabyShop.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IInventoryService _inventoryService;
        private readonly IBasketRepository _basketRepository;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            IInventoryService inventoryService,
            IBasketRepository basketRepository,
            ILogger<OrderService> logger)
        {
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _inventoryService = inventoryService;
            _basketRepository = basketRepository;
            _logger = logger;
        }

        public async Task<List<OrderSummaryDto>> GetAllOrdersForAdminAsync()
        {
            var orders = await _orderRepository.GetAllAsync();
            return orders.Select(o => new OrderSummaryDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                UserName = o.UserName ?? "مشتری مهمان",
                PhoneNumber = o.PhoneNumber,
                Status = o.Status,
                TotalAmount = o.TotalAmount,
                CreatedAt = o.CreatedAt
            }).ToList();
        }

        public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto)
        {
            var order = new Order(dto.UserId, dto.ShippingAddress, dto.PhoneNumber, dto.Email ?? "");

            if (!string.IsNullOrEmpty(dto.CustomerNote))
                order.CustomerNote = dto.CustomerNote;

            foreach (var item in dto.Items)
            {
                if (item.Quantity <= 0)
                    throw new BusinessRuleException("تعداد اقلام باید بیشتر از صفر باشد.");

                var product = await _productRepository.GetByIdAsync(item.ProductId)
                    ?? throw new NotFoundException("محصول", item.ProductId);

                // قیمت همیشه سمت سرور از روی قیمت فعلی محصول محاسبه می‌شود
                order.AddItem(item.ProductId, product.Name, item.Quantity, product.Price);

                // کسر از انبار
                await _inventoryService.DecreaseStockAsync(item.ProductId, item.Quantity);
            }

            var createdOrder = await _orderRepository.AddAsync(order);
            return MapToDto(createdOrder);
        }

        public async Task<OrderDto> UpdateOrderStatusAsync(UpdateOrderStatusDto dto)
        {
            var order = await _orderRepository.GetByIdAsync(dto.OrderId)
                ?? throw new NotFoundException("سفارش", dto.OrderId);

            // ماشین وضعیت سفارش: هر انتقال فقط از وضعیت‌های مجاز
            switch (dto.Status.ToLower())
            {
                case "paid":
                    if (order.Status != "Pending")
                        throw new BusinessRuleException("فقط سفارش‌های در انتظار، قابل پرداخت هستند.");
                    order.MarkAsPaid();
                    break;
                case "shipped":
                    if (order.Status != "Paid")
                        throw new BusinessRuleException("سفارش قبل از ارسال باید پرداخت شده باشد.");
                    order.MarkAsShipped();
                    break;
                case "delivered":
                    if (order.Status != "Shipped")
                        throw new BusinessRuleException("سفارش قبل از تحویل باید ارسال شده باشد.");
                    order.MarkAsDelivered();
                    break;
                case "cancelled":
                    if (order.Status is "Delivered" or "Refunded")
                        throw new BusinessRuleException("این سفارش قابل لغو نیست.");
                    order.Cancel(dto.Reason ?? "No reason");
                    break;
                default:
                    throw new BusinessRuleException("وضعیت نامعتبر است.");
            }

            await _orderRepository.UpdateAsync(order);
            return MapToDto(order);
        }

        public async Task<OrderDto?> GetOrderByIdAsync(int id)
        {
            var order = await _orderRepository.GetOrderWithItemsAsync(id);
            return order == null ? null : MapToDto(order);
        }

        public async Task<List<OrderSummaryDto>> GetUserOrderSummariesAsync(int userId)
        {
            var orders = await _orderRepository.GetOrdersByUserAsync(userId);
            return orders.Select(o => new OrderSummaryDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                Status = o.Status,
                TotalAmount = o.TotalAmount,
                CreatedAt = o.CreatedAt
            }).ToList();
        }

        public async Task<OrderDto> CreateOrderFromBasketAsync(CreateOrderFromBasketDto dto)
        {
            var basket = await _basketRepository.GetBasketWithItemsAsync(dto.BasketId)
                ?? throw new NotFoundException("سبد", dto.BasketId);

            // سبد باید متعلق به همان کاربری باشد که سفارش را ثبت می‌کند
            if (basket.UserId != dto.UserId)
                throw new BusinessRuleException("شما دسترسی به این سبد خرید ندارید.");

            var order = new Order(basket.UserId, dto.ShippingAddress, dto.PhoneNumber, dto.Email ?? "");
            foreach (var item in basket.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId)
                    ?? throw new NotFoundException("محصول", item.ProductId);

                // قیمت همیشه سمت سرور از روی قیمت فعلی محصول محاسبه می‌شود
                order.AddItem(item.ProductId, product.Name, item.Quantity, product.Price);
                await _inventoryService.DecreaseStockAsync(item.ProductId, item.Quantity);
            }

            var createdOrder = await _orderRepository.AddAsync(order);
            await _basketRepository.ClearBasketAsync(basket.Id);
            return MapToDto(createdOrder);
        }

        public async Task<OrderDto> CancelOrderAsync(int orderId, string reason)
        {
            var order = await _orderRepository.GetByIdAsync(orderId)
                ?? throw new NotFoundException("سفارش", orderId);

            order.Cancel(reason);
            await _orderRepository.UpdateAsync(order);
            return MapToDto(order);
        }

        private OrderDto MapToDto(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                TotalAmount = order.TotalAmount,
                SubTotal = order.SubTotal,
                ShippingCost = order.ShippingCost,
                Tax = order.Tax,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UserName = order.UserName ?? "",
                PhoneNumber = order.PhoneNumber,
                ShippingAddress = order.ShippingAddress,
                CustomerNote = order.CustomerNote,

                // این خط دقیقاً همونیه که لیست لباس‌ها رو می‌فرسته تو فاکتور!
                Items = order.Items?.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList() ?? new List<OrderItemDto>()
            };
        }

        public Task<List<OrderDto>> GetOrdersByUserIdAsync(int userId) => throw new NotImplementedException();
        public Task<OrderSummaryDto> GetOrderSummaryAsync(int orderId) => throw new NotImplementedException();
    }
}