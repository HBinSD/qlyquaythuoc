using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

public sealed class ShiftService
{
    private const decimal MaximumOpeningCash = 9999999999999999.99M;
    private readonly ShiftRepository _repository;

    public ShiftService()
        : this(new ShiftRepository())
    {
    }

    public ShiftService(ShiftRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<OpenShiftRow?> GetOpenShiftAsync(
        string employeeId,
        CancellationToken ct = default)
    {
        string id = ValidateEmployeeId(employeeId);
        return _repository.GetOpenShiftAsync(id, ct);
    }

    public Task<decimal> GetSuggestedOpeningCashAsync(
        string employeeId,
        CancellationToken ct = default)
    {
        string id = ValidateEmployeeId(employeeId);
        return _repository.GetSuggestedOpeningCashAsync(id, ct);
    }

    public Task<ShiftCloseSummaryRow> GetCloseSummaryAsync(
        string employeeId,
        string shiftId,
        CancellationToken ct = default)
    {
        string employee = ValidateEmployeeId(employeeId);
        string shift = ValidateEmployeeId(shiftId);
        return _repository.GetCloseSummaryAsync(employee, shift, ct);
    }

    public Task<ClosedShiftRow> CloseShiftAsync(
        string employeeId,
        string shiftId,
        decimal actualCash,
        string? differenceNote = null,
        CancellationToken ct = default)
    {
        string employee = ValidateEmployeeId(employeeId);
        string shift = ValidateEmployeeId(shiftId);
        if (actualCash < 0 || actualCash > MaximumOpeningCash)
            throw new ArgumentOutOfRangeException(nameof(actualCash), "Tiền thực tế phải từ 0 đến giới hạn lưu trữ.");
        if (decimal.Round(actualCash, 2) != actualCash)
            throw new ArgumentOutOfRangeException(nameof(actualCash), "Tiền thực tế tối đa 2 chữ số thập phân.");
        if (differenceNote?.Trim().Length > 500)
            throw new ArgumentException("Ghi chú chốt ca tối đa 500 ký tự.", nameof(differenceNote));

        string? note = string.IsNullOrWhiteSpace(differenceNote) ? null : differenceNote.Trim();
        return _repository.CloseShiftAsync(employee, shift, actualCash, note, ct);
    }

    public Task<OpenShiftRow> OpenShiftAsync(
        string employeeId,
        decimal openingCash,
        string? openingNote = null,
        CancellationToken ct = default)
    {
        string id = ValidateEmployeeId(employeeId);
        if (openingCash < 0 || openingCash > MaximumOpeningCash)
            throw new ArgumentOutOfRangeException(nameof(openingCash), "Tiền đầu ca phải từ 0 đến giới hạn lưu trữ.");
        if (decimal.Round(openingCash, 2) != openingCash)
            throw new ArgumentOutOfRangeException(nameof(openingCash), "Tiền đầu ca tối đa 2 chữ số thập phân.");
        if (openingNote?.Trim().Length > 500)
            throw new ArgumentException("Ghi chú đầu ca tối đa 500 ký tự.", nameof(openingNote));

        string? note = string.IsNullOrWhiteSpace(openingNote) ? null : openingNote.Trim();
        return _repository.OpenShiftAsync(id, openingCash, note, ct);
    }

    private static string ValidateEmployeeId(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Mã nhân viên không hợp lệ.", nameof(employeeId));

        string id = employeeId.Trim();
        if (id.Length > 20)
            throw new ArgumentException("Mã nhân viên tối đa 20 ký tự.", nameof(employeeId));
        return id;
    }
}
