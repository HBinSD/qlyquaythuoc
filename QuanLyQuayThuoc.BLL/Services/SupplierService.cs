
using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

public sealed class SupplierService
{
    private readonly SupplierRepository _repository;

    public SupplierService()
    {
        _repository = new SupplierRepository();
    }

    public SupplierService(SupplierRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<List<SupplierRepository.SupplierRow>> GetAllAsync(
        string? keyword = null,
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        return _repository.GetAllAsync(
            (keyword ?? "").Trim(),
            includeInactive,
            ct);
    }

    public Task<SupplierRepository.SupplierRow?> GetByIdAsync(
        string supplierId,
        CancellationToken ct = default)
    {
        ValidateId(supplierId);

        return _repository.GetByIdAsync(supplierId.Trim(), ct);
    }

    public async Task AddAsync(
        SupplierRepository.SupplierRow supplier,
        CancellationToken ct = default)
    {
        ValidateSupplier(supplier);

        supplier.SupplierId = supplier.SupplierId.Trim();

        if (await _repository.ExistsAsync(supplier.SupplierId, ct))
            throw new InvalidOperationException("Mã nhà cung cấp đã tồn tại.");

        await _repository.AddAsync(supplier, ct);
    }

    public async Task UpdateAsync(
        SupplierRepository.SupplierRow supplier,
        CancellationToken ct = default)
    {
        ValidateSupplier(supplier);

        supplier.SupplierId = supplier.SupplierId.Trim();

        if (!await _repository.ExistsAsync(supplier.SupplierId, ct))
            throw new InvalidOperationException("Nhà cung cấp không tồn tại.");

        await _repository.UpdateAsync(supplier, ct);
    }

    public async Task SetActiveAsync(
        string supplierId,
        bool isActive,
        CancellationToken ct = default)
    {
        ValidateId(supplierId);

        if (!await _repository.ExistsAsync(supplierId.Trim(), ct))
            throw new InvalidOperationException("Nhà cung cấp không tồn tại.");

        await _repository.SetActiveAsync(supplierId.Trim(), isActive, ct);
    }

    private static void ValidateId(string supplierId)
    {
        if (string.IsNullOrWhiteSpace(supplierId))
            throw new ArgumentException("Mã nhà cung cấp không được để trống.");

        if (supplierId.Trim().Length > 20)
            throw new ArgumentException("Mã nhà cung cấp tối đa 20 ký tự.");
    }

    private static void ValidateSupplier(
        SupplierRepository.SupplierRow supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        ValidateId(supplier.SupplierId);

        if (string.IsNullOrWhiteSpace(supplier.SupplierName))
            throw new ArgumentException("Tên nhà cung cấp không được để trống.");

        if (supplier.SupplierName.Trim().Length > 200)
            throw new ArgumentException("Tên nhà cung cấp tối đa 200 ký tự.");

        if (supplier.Phone?.Length > 20)
            throw new ArgumentException("Số điện thoại tối đa 20 ký tự.");

        if (supplier.Email?.Length > 150)
            throw new ArgumentException("Email tối đa 150 ký tự.");

        if (supplier.Address?.Length > 300)
            throw new ArgumentException("Địa chỉ tối đa 300 ký tự.");

        if (supplier.TaxCode?.Length > 30)
            throw new ArgumentException("Mã số thuế tối đa 30 ký tự.");
    }
}
