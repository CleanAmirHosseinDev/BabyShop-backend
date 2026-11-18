using BabyShop.Application.Dtos;

namespace BabyShop.Application.Interfaces.Services;

public interface IPaymentService
{
    Task<PaymentDto?> GetPaymentByIdAsync(int id);
    Task<PaymentDto?> GetPaymentByOrderIdAsync(int orderId);
    Task<PaymentDto?> GetPaymentByAuthorityAsync(string authority);
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentDto dto, int currentUserId, bool isStaff);
    Task<PaymentDto> VerifyPaymentAsync(int paymentId, string referenceNumber, int currentUserId, bool isStaff);
    Task<PaymentDto> RefundPaymentAsync(int paymentId, string reason);
    Task<decimal> GetOrderTotalAmountAsync(int orderId);
}