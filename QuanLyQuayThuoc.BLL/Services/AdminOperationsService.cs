using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

public sealed class AdminOperationsService
{
    private readonly AdminOperationsRepository _repository;

    public AdminOperationsService()
        : this(new AdminOperationsRepository())
    {
    }

    public AdminOperationsService(AdminOperationsRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<List<InvoiceAdminRow>> SearchInvoicesAsync(
        string? keyword,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.SearchInvoicesAsync(keyword, ct);
    }

    public async Task<InvoiceCancellationResult> CancelPaidInvoiceAsync(
        string invoiceId,
        string reason,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        string id = RequiredValue(invoiceId, 20, "Mã hóa đơn");
        string note = RequiredValue(reason, 500, "Lý do hủy");
        return await _repository.CancelPaidInvoiceAsync(id, note, actorId.Trim(), ct);
    }

    public Task<List<InvoicePaymentRow>> GetInvoicePaymentsAsync(
        string invoiceId,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.GetInvoicePaymentsAsync(RequiredValue(invoiceId, 20, "Mã hóa đơn"), ct);
    }

    public Task<List<InvoiceDetailRow>> GetInvoiceDetailsAsync(
        string invoiceId,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.GetInvoiceDetailsAsync(RequiredValue(invoiceId, 20, "Mã hóa đơn"), ct);
    }

    public Task<List<PendingStocktakeRow>> GetPendingStocktakesAsync(
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.GetPendingStocktakesAsync(ct);
    }

    public Task<List<StocktakeDetailRow>> GetStocktakeDetailsAsync(
        string stocktakeId,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.GetStocktakeDetailsAsync(RequiredValue(stocktakeId, 20, "Mã phiếu kiểm kê"), ct);
    }

    public Task ApproveStocktakeAsync(
        string stocktakeId,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.ApproveStocktakeAsync(
            RequiredValue(stocktakeId, 20, "Mã phiếu kiểm kê"),
            actorId.Trim(),
            ct);
    }

    public Task RejectStocktakeAsync(
        string stocktakeId,
        string reason,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.RejectStocktakeAsync(
            RequiredValue(stocktakeId, 20, "Mã phiếu kiểm kê"),
            RequiredValue(reason, 500, "Lý do từ chối"),
            actorId.Trim(),
            ct);
    }

    public Task<List<PendingDisposalRow>> GetPendingDisposalsAsync(
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.GetPendingDisposalsAsync(ct);
    }

    public Task<List<DisposalDetailRow>> GetDisposalDetailsAsync(
        string disposalId,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.GetDisposalDetailsAsync(RequiredValue(disposalId, 20, "Mã phiếu xuất hủy"), ct);
    }

    public Task ApproveDisposalAsync(
        string disposalId,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.ApproveDisposalAsync(
            RequiredValue(disposalId, 20, "Mã phiếu xuất hủy"),
            actorId.Trim(),
            ct);
    }

    public Task RejectDisposalAsync(
        string disposalId,
        string reason,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.RejectDisposalAsync(
            RequiredValue(disposalId, 20, "Mã phiếu xuất hủy"),
            RequiredValue(reason, 500, "Lý do từ chối"),
            actorId.Trim(),
            ct);
    }

    private static void EnsureManager(string actorId, string actorRole)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Không xác định được tài khoản đang thao tác.", nameof(actorId));
        if (!string.Equals(actorRole, "ADMIN", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Chỉ Quản lý mới được sử dụng chức năng này.");
    }

    private static string RequiredValue(string value, int maxLength, string fieldName)
    {
        string result = (value ?? "").Trim();
        if (result.Length == 0)
            throw new ArgumentException($"{fieldName} không được để trống.");
        if (result.Length > maxLength)
            throw new ArgumentException($"{fieldName} tối đa {maxLength} ký tự.");
        return result;
    }
}
