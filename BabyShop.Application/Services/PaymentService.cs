using BabyShop.Application.Dtos;
using BabyShop.Application.Interfaces.Services;
using BabyShop.Core.Entities;
using BabyShop.Core.Interfaces;
using BabyShop.Core.Exceptions;
using System.Threading.Tasks;
using System;

namespace BabyShop.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IPaymentRepository _paymentRepository;

        public PaymentService(IOrderRepository orderRepository, IPaymentRepository paymentRepository)
        {
            _orderRepository = orderRepository;
            _paymentRepository = paymentRepository;
        }

        // ۱. ایجاد تراکنش پرداخت جدید (متصل به درگاه)
        public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentDto dto, int currentUserId, bool isStaff)
        {
            var order = await _orderRepository.GetByIdAsync(dto.OrderId);
            if (order == null) throw new NotFoundException("سفارش مورد نظر یافت نشد.");

            // سفارش باید متعلق به کاربر جاری باشد (مگر کارمند)
            if (!isStaff && order.UserId != currentUserId)
                throw new BusinessRuleException("شما دسترسی به این سفارش ندارید.");

            // جلوگیری از پرداختِ سفارشی که قبلاً پرداخت شده
            if (order.Status == "Paid")
                throw new BusinessRuleException("این سفارش قبلاً پرداخت شده است.");

            if (order.Status is "Cancelled" or "Refunded")
                throw new BusinessRuleException("این سفارش قابل پرداخت نیست.");

            // مبلغ همیشه سمت سرور از روی کل سفارش محاسبه می‌شود
            var payment = new Payment(order.Id, order.TotalAmount, dto.PaymentMethod);

            // تولید یه کد آتوریتی رندوم (شبیه سازی بازگشت از درگاه شاپرک)
            payment.SetAuthority($"ZIIZII-{Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper()}");

            var createdPayment = await _paymentRepository.AddAsync(payment);
            return MapToDto(createdPayment);
        }

        // ۲. تایید نهایی پرداخت (هنگام برگشت از درگاه بانک)
        public async Task<PaymentDto> VerifyPaymentAsync(int paymentId, string referenceNumber, int currentUserId, bool isStaff)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null) throw new NotFoundException("تراکنش یافت نشد.", paymentId);

            var order = await _orderRepository.GetByIdAsync(payment.OrderId);
            if (order == null) throw new NotFoundException("سفارش", payment.OrderId);

            // تراکنش باید متعلق به سفارشِ کاربر جاری باشد (مگر کارمند)
            if (!isStaff && order.UserId != currentUserId)
                throw new BusinessRuleException("شما دسترسی به این تراکنش ندارید.");

            // اگر قبلا‌ً تایید شده، همان نتیجه را برمی‌گردانیم (idempotent)
            if (payment.Status == "Paid")
                return MapToDto(payment);

            if (order.Status is "Cancelled" or "Refunded")
                throw new BusinessRuleException("این سفارش قابل تایید پرداخت نیست.");

            // تایید پرداخت و ثبت شماره پیگیری بانک
            payment.Verify(referenceNumber);
            await _paymentRepository.UpdateAsync(payment);

            // حالا باید وضعیت سفارش اصلی رو هم بکنیم 'پرداخت شده'
            order.MarkAsPaid();
            await _orderRepository.UpdateAsync(order);

            return MapToDto(payment);
        }

        public async Task<PaymentDto> RefundPaymentAsync(int paymentId, string reason)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null) throw new NotFoundException("Payment", paymentId);

            payment.Refund(reason);
            await _paymentRepository.UpdateAsync(payment);

            var order = await _orderRepository.GetByIdAsync(payment.OrderId);
            if (order != null)
            {
                order.MarkAsRefunded();
                await _orderRepository.UpdateAsync(order);
            }

            return MapToDto(payment);
        }

        public async Task<decimal> GetOrderTotalAmountAsync(int orderId) =>
            (await _orderRepository.GetByIdAsync(orderId))?.TotalAmount ?? 0;

        public async Task<PaymentDto?> GetPaymentByIdAsync(int id) =>
            MapToDto(await _paymentRepository.GetByIdAsync(id));

        public async Task<PaymentDto?> GetPaymentByAuthorityAsync(string auth) =>
            MapToDto(await _paymentRepository.GetPaymentByAuthorityAsync(auth));

        public async Task<PaymentDto?> GetPaymentByOrderIdAsync(int id) =>
            MapToDto(await _paymentRepository.GetPaymentByOrderIdAsync(id));

        private PaymentDto MapToDto(Payment? p)
        {
            if (p == null) return null!;
            return new PaymentDto
            {
                Id = p.Id,
                OrderId = p.OrderId,
                Status = p.Status,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod,
                Authority = p.Authority,
                ReferenceNumber = p.ReferenceNumber
            };
        }
    }
}