using Microsoft.Data.SqlClient;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

public sealed record DashboardSummary(
    decimal TodayRevenue,
    int TodayInvoiceCount,
    int ActiveMedicineCount,
    int LowStockMedicineCount,
    int ExpiringBatchCount,
    int ExpiredBatchCount);

public sealed class DashboardRepository
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH StockByMedicine AS
(
    SELECT m.MedicineId, m.MinimumStock,
           COALESCE(SUM(b.PhysicalStock), 0) AS PhysicalStock
    FROM dbo.Medicine m
    LEFT JOIN dbo.MedicineBatch b
      ON b.MedicineId = m.MedicineId AND b.BatchStatus = 'ACTIVE'
    GROUP BY m.MedicineId, m.MinimumStock
)
SELECT
    CAST(COALESCE((
        SELECT SUM(i.TotalAmount)
        FROM dbo.Invoice i
        WHERE i.Status IN ('PAID','RETURNED_PARTIAL')
          AND i.CreatedAt >= CONVERT(date, SYSDATETIME())
          AND i.CreatedAt < DATEADD(day, 1, CONVERT(date, SYSDATETIME()))
    ), 0) AS decimal(18,2)) AS TodayRevenue,
    (SELECT COUNT(*) FROM dbo.Invoice i
        WHERE i.CreatedAt >= CONVERT(date, SYSDATETIME())
          AND i.CreatedAt < DATEADD(day, 1, CONVERT(date, SYSDATETIME()))
          AND i.Status <> 'CANCELLED') AS TodayInvoiceCount,
    (SELECT COUNT(*) FROM dbo.Medicine WHERE BusinessStatus = 'ACTIVE') AS ActiveMedicineCount,
    (SELECT COUNT(*) FROM StockByMedicine WHERE PhysicalStock < MinimumStock) AS LowStockMedicineCount,
    (SELECT COUNT(*) FROM dbo.MedicineBatch
        WHERE BatchStatus = 'ACTIVE' AND PhysicalStock > 0
          AND ExpiryDate >= CONVERT(date, SYSDATETIME())
          AND ExpiryDate <= DATEADD(day, 30, CONVERT(date, SYSDATETIME()))) AS ExpiringBatchCount,
    (SELECT COUNT(*) FROM dbo.MedicineBatch
        WHERE BatchStatus = 'ACTIVE' AND PhysicalStock > 0
          AND ExpiryDate < CONVERT(date, SYSDATETIME())) AS ExpiredBatchCount;";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new DashboardSummary(0, 0, 0, 0, 0, 0);

        return new DashboardSummary(
            reader.GetDecimal(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }
}
