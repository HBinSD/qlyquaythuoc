
using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

public sealed class SaleService
{
    private readonly SaleRepository _repository;

    public SaleService()
    {
        _repository = new SaleRepository();
    }

    public SaleService(SaleRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<SaleRepository.OpenShiftRow?> GetOpenShiftAsync(
        string employeeId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Mã dược sĩ không hợp lệ.");

        return _repository.GetOpenShiftAsync(employeeId.Trim(), ct);
    }

    public Task<List<SaleRepository.SaleMedicineRow>> SearchMedicinesAsync(
        string? keyword = null,
        CancellationToken ct = default)
    {
        return _repository.SearchMedicinesAsync(
            (keyword ?? "").Trim(), ct);
    }

    public Task<List<SaleRepository.CustomerRow>> SearchCustomersAsync(
        string? keyword = null,
        CancellationToken ct = default)
    {
        return _repository.SearchCustomersAsync(
            (keyword ?? "").Trim(), ct);
    }

    public async Task<string> CheckoutAsync(
        SaleRepository.CheckoutRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.EmployeeId))
            throw new ArgumentException("Không xác định được dược sĩ đang bán.");

        if (string.IsNullOrWhiteSpace(request.ShiftId))
            throw new ArgumentException("Không xác định được ca làm việc.");

        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("Vui lòng thêm thuốc vào hóa đơn.");

        if (request.Lines.Any(x =>
            string.IsNullOrWhiteSpace(x.MedicineId)
            || x.MedicineUnitId <= 0
            || x.Quantity <= 0))
        {
            throw new ArgumentException("Thông tin thuốc hoặc số lượng không hợp lệ.");
        }

        if (request.Lines.Any(x => x.Quantity > 100000))
            throw new ArgumentException("Số lượng bán vượt giới hạn cho phép.");

        string method = (request.PaymentMethod ?? "")
            .Trim()
            .ToUpperInvariant();

        if (method is not ("CASH" or "TRANSFER" or "QR"))
            throw new ArgumentException("Phương thức thanh toán không hợp lệ.");

        request.EmployeeId = request.EmployeeId.Trim();
        request.ShiftId = request.ShiftId.Trim();
        request.CustomerId = Normalize(request.CustomerId);
        request.PrescriptionId = Normalize(request.PrescriptionId);
        request.PaymentMethod = method;
        request.TransactionReference = Normalize(request.TransactionReference);

        return await _repository.CheckoutAsync(request, ct);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
