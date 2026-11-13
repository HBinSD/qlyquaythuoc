using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

public sealed class DashboardService
{
    private readonly DashboardRepository _repository = new();
    public Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
        => _repository.GetSummaryAsync(cancellationToken);
}
