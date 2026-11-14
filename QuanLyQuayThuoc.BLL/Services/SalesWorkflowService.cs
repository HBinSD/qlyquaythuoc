using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

public sealed class SalesWorkflowService
{
    private readonly SalesWorkflowRepository _repository;

    public SalesWorkflowService()
        : this(new SalesWorkflowRepository())
    {
    }

    public SalesWorkflowService(SalesWorkflowRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<List<SalesCustomerRow>> SearchCustomersByPhoneAsync(
        string phonePrefix,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phonePrefix))
            throw new ArgumentException("Vui lòng nhập số điện thoại cần tìm.", nameof(phonePrefix));
        return _repository.SearchCustomersByPhoneAsync(phonePrefix.Trim(), ct);
    }

    public Task<SalesCustomerRow> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.CreateCustomerAsync(request, ct);
    }

    public Task<string> CreatePrescriptionAsync(
        CreatePrescriptionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.CreatePrescriptionAsync(request, ct);
    }

    public Task<DiscountQuote> CalculateDiscountsAsync(
        string? customerId,
        decimal subtotal,
        string? voucherCode = null,
        int pointsToRedeem = 0,
        CancellationToken ct = default)
    {
        return _repository.CalculateDiscountsAsync(
            customerId, subtotal, voucherCode, pointsToRedeem, ct);
    }

    public Task<InvoiceDraftResult> CreateInvoiceDraftAsync(
        CreateInvoiceDraftRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.CreateInvoiceDraftAsync(request, ct);
    }

    public Task<List<PendingInvoiceDraftRow>> GetPendingInvoiceDraftsAsync(
        string shiftId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(shiftId))
            throw new ArgumentException("Mã ca không hợp lệ.", nameof(shiftId));
        return _repository.GetPendingInvoiceDraftsAsync(shiftId.Trim(), ct);
    }

    public Task<InvoiceSettlementResult> SettleInvoiceDraftAsync(
        SettleInvoiceDraftRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.SettleInvoiceDraftAsync(request, ct);
    }

    public Task CancelInvoiceDraftAsync(
        string invoiceId,
        string cancelledBy,
        string? reason = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceId))
            throw new ArgumentException("Mã hóa đơn không hợp lệ.", nameof(invoiceId));
        if (string.IsNullOrWhiteSpace(cancelledBy))
            throw new ArgumentException("Không xác định được nhân viên đang thao tác.", nameof(cancelledBy));
        return _repository.CancelInvoiceDraftAsync(
            invoiceId.Trim(), cancelledBy.Trim(), reason, ct);
    }

    public Task<int> ExpirePendingDraftsAsync(CancellationToken ct = default)
        => _repository.ExpirePendingDraftsAsync(ct);
}
