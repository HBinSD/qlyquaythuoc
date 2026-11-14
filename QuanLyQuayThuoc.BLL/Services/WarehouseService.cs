
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

    public async Task<WarehouseRepository.ProposalResult>
        SubmitStocktakeProposalAsync(
            string batchId,
            decimal actualQuantity,
            string? differenceReason,
            string actorId,
            string actorRole,
            CancellationToken ct = default)
    {
        EnsureProposalAccess(actorId, actorRole);
        string id = RequiredValue(batchId, 25, "Mã lô thuốc");
        ValidateQuantity(actualQuantity, allowZero: true, nameof(actualQuantity));
        string? reason = NormalizeOptional(differenceReason, 300, "Lý do chênh lệch");

        return await _repository.CreateStocktakeProposalAsync(
            id, actualQuantity, reason, actorId.Trim(), ct);
    }

    public async Task<WarehouseRepository.ProposalResult>
        SubmitDisposalProposalAsync(
            string batchId,
            decimal quantity,
            string reason,
            string actorId,
            string actorRole,
            CancellationToken ct = default)
    {
        EnsureProposalAccess(actorId, actorRole);
        string id = RequiredValue(batchId, 25, "Mã lô thuốc");
        ValidateQuantity(quantity, allowZero: false, nameof(quantity));
        string note = RequiredValue(reason, 500, "Lý do xuất hủy");

        return await _repository.CreateDisposalProposalAsync(
            id, quantity, note, actorId.Trim(), ct);
    }

    // =====================================================
    // HELPER
    // =====================================================

    private static void EnsureProposalAccess(string actorId, string actorRole)
    {
        if (string.IsNullOrWhiteSpace(actorId) || actorId.Trim().Length > 20)
            throw new ArgumentException("Không xác định được tài khoản đang thao tác.", nameof(actorId));

        if (!string.Equals(actorRole, "WAREHOUSE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(actorRole, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Chỉ nhân viên kho hoặc quản lý mới được tạo đề xuất kho.");
        }
    }

    private static void ValidateQuantity(decimal quantity, bool allowZero, string parameterName)
    {
        if (quantity < 0 || (!allowZero && quantity == 0) || quantity > 999999999999999.999M)
            throw new ArgumentOutOfRangeException(parameterName, "Số lượng không hợp lệ.");
        if (decimal.Round(quantity, 3) != quantity)
            throw new ArgumentException("Số lượng tối đa 3 chữ số thập phân.", parameterName);
    }

    private static string RequiredValue(string? value, int maxLength, string label)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0)
            throw new ArgumentException($"{label} không được để trống.");
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{label} không được vượt quá {maxLength} ký tự.");
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string label)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{label} không được vượt quá {maxLength} ký tự.");
        return normalized.Length == 0 ? null : normalized;
    }

    private static string NormalizeKeyword(string? keyword)
    {
        return (keyword ?? string.Empty).Trim();
    }
}
