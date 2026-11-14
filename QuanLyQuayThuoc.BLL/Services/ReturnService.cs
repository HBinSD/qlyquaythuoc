using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

public sealed class ReturnService
{
    private readonly ReturnRepository _repository;

    public ReturnService()
        : this(new ReturnRepository())
    {
    }

    public ReturnService(ReturnRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<List<ReturnInvoiceRow>> SearchInvoicesAsync(
        string? keyword = null,
        CancellationToken ct = default)
    {
        string value = (keyword ?? "").Trim();
        if (value.Length > 100)
            value = value[..100];
        return _repository.SearchInvoicesAsync(value, ct);
    }

    public Task<List<ReturnLineRow>> GetRemainingLinesAsync(
        string invoiceId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceId) || invoiceId.Trim().Length > 20)
            throw new ArgumentException("Mã hóa đơn không hợp lệ.", nameof(invoiceId));
        return _repository.GetRemainingLinesAsync(invoiceId.Trim(), ct);
    }

    public Task<ReturnProcessResult> ProcessReturnAsync(
        ReturnProcessRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.InvoiceId) || request.InvoiceId.Trim().Length > 20)
            throw new ArgumentException("Mã hóa đơn không hợp lệ.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.EmployeeId) || request.EmployeeId.Trim().Length > 20)
            throw new ArgumentException("Không xác định được nhân viên đang đăng nhập.", nameof(request));
        string refundMethod = (request.RefundMethod ?? "").Trim().ToUpperInvariant();
        if (refundMethod is not ("CASH" or "TRANSFER"))
            throw new ArgumentException("Chọn phương thức hoàn tiền CASH hoặc TRANSFER.", nameof(request));

        string reason = (request.Reason ?? "").Trim();
        if (reason.Length is 0 or > 500)
            throw new ArgumentException("Lý do trả hàng là bắt buộc và tối đa 500 ký tự.", nameof(request));
        if (!request.SealedConfirmed || !request.UndamagedConfirmed || !request.WithinExpiryConfirmed)
            throw new ArgumentException("Cần xác nhận thuốc còn niêm phong, nguyên vẹn và trong hạn dùng.", nameof(request));
        if (request.Lines is null || request.Lines.Count == 0)
            throw new ArgumentException("Hãy chọn ít nhất một dòng thuốc trả.", nameof(request));
        if (request.Lines.Any(line =>
                line.InvoiceDetailId <= 0
                || line.Quantity <= 0
                || line.Quantity > 100000
                || line.Quantity != decimal.Round(line.Quantity, 3)))
        {
            throw new ArgumentException("Số lượng trả phải lớn hơn 0 và tối đa 3 chữ số thập phân.", nameof(request));
        }
        if (request.Lines.Select(line => line.InvoiceDetailId).Distinct().Count() != request.Lines.Count)
            throw new ArgumentException("Không được chọn lặp lại cùng một dòng hóa đơn.", nameof(request));

        request.InvoiceId = request.InvoiceId.Trim();
        request.EmployeeId = request.EmployeeId.Trim();
        request.RefundMethod = refundMethod;
        request.Reason = reason;

        return _repository.ProcessReturnAsync(request, ct);
    }
}
