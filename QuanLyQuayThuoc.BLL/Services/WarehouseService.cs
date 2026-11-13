
using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

/// <summary>
/// Xử lý nghiệp vụ quản lý kho thuốc.
/// </summary>
public sealed class WarehouseService
{
    private readonly WarehouseRepository _repository;

    public WarehouseService()
    {
        _repository = new WarehouseRepository();
    }

    public WarehouseService(WarehouseRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    // =====================================================
    // 1. DANH SÁCH TỒN KHO
    // =====================================================

    public async Task<List<WarehouseRepository.InventoryRow>>
        GetInventoryAsync(
            string? keyword = null,
            CancellationToken ct = default)
    {
        keyword = NormalizeKeyword(keyword);

        return await _repository.GetInventoryAsync(keyword, ct);
    }

    // =====================================================
    // 2. DANH SÁCH LÔ THUỐC
    // =====================================================

    public async Task<List<WarehouseRepository.BatchRow>>
        GetBatchesAsync(
            string? medicineId = null,
            CancellationToken ct = default)
    {
        medicineId = string.IsNullOrWhiteSpace(medicineId)
            ? null
            : medicineId.Trim();

        return await _repository.GetBatchesAsync(medicineId, ct);
    }

    // =====================================================
    // 3. THUỐC SẮP HẾT HẠN
    // =====================================================

    public async Task<List<WarehouseRepository.ExpiringBatchRow>>
        GetExpiringBatchesAsync(
            int daysAhead = 30,
            CancellationToken ct = default)
    {
        if (daysAhead < 0 || daysAhead > 3650)
        {
            throw new ArgumentOutOfRangeException(
                nameof(daysAhead),
                "Số ngày kiểm tra phải từ 0 đến 3650.");
        }

        return await _repository.GetExpiringBatchesAsync(
            daysAhead,
            ct);
    }

    // =====================================================
    // 4. LỊCH SỬ GIAO DỊCH KHO
    // =====================================================

    public async Task<List<WarehouseRepository.StockTransactionRow>>
        GetStockTransactionsAsync(
            string? keyword = null,
            CancellationToken ct = default)
    {
        keyword = NormalizeKeyword(keyword);

        return await _repository.GetStockTransactionsAsync(
            keyword,
            ct);
    }

    // =====================================================
    // 5. DANH SÁCH PHIẾU NHẬP
    // =====================================================

    public async Task<List<WarehouseRepository.GoodsReceiptRow>>
        GetGoodsReceiptsAsync(
            string? keyword = null,
            CancellationToken ct = default)
    {
        keyword = NormalizeKeyword(keyword);

        return await _repository.GetGoodsReceiptsAsync(
            keyword,
            ct);
    }

    // =====================================================
    // 6. CHI TIẾT PHIẾU NHẬP
    // =====================================================

    public async Task<List<WarehouseRepository.GoodsReceiptDetailRow>>
        GetGoodsReceiptDetailsAsync(
            string receiptId,
            CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(receiptId))
        {
            throw new ArgumentException(
                "Mã phiếu nhập không được để trống.",
                nameof(receiptId));
        }

        return await _repository.GetGoodsReceiptDetailsAsync(
            receiptId.Trim(),
            ct);
    }

    // =====================================================
    // HELPER
    // =====================================================

    private static string NormalizeKeyword(string? keyword)
    {
        return (keyword ?? string.Empty).Trim();
    }
}
