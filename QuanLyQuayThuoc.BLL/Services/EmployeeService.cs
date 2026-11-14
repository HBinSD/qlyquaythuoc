using System.Text.RegularExpressions;
using QuanLyQuayThuoc.BLL.Security;
using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services;

public sealed class EmployeeService
{
    private static readonly HashSet<string> AllowedRoles =
        ["ADMIN", "PHARMACIST", "WAREHOUSE"];

    private readonly EmployeeRepository _repository;

    public EmployeeService()
        : this(new EmployeeRepository())
    {
    }

    public EmployeeService(EmployeeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<List<EmployeeListRow>> GetEmployeesAsync(
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.GetEmployeesAsync(ct);
    }

    public async Task<string> SaveEmployeeAsync(
        EmployeeSaveModel model,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        EnsureManager(actorId, actorRole);

        string fullName = (model.FullName ?? "").Trim();
        string username = (model.Username ?? "").Trim();
        string roleId = (model.RoleId ?? "").Trim().ToUpperInvariant();
        bool isNew = string.IsNullOrWhiteSpace(model.EmployeeId);
        string employeeId = isNew
            ? $"NV{Guid.NewGuid():N}"[..20].ToUpperInvariant()
            : model.EmployeeId!.Trim();

        if (fullName.Length is 0 or > 150)
            throw new ArgumentException("Họ tên phải có từ 1 đến 150 ký tự.");
        if (username.Length is 0 or > 50 || username.Any(char.IsWhiteSpace))
            throw new ArgumentException("Tên đăng nhập phải có tối đa 50 ký tự và không chứa khoảng trắng.");
        if (!AllowedRoles.Contains(roleId))
            throw new ArgumentException("Vai trò tài khoản không hợp lệ.");
        if (employeeId.Length > 20)
            throw new ArgumentException("Mã nhân viên tối đa 20 ký tự.");

        string? passwordHash = null;
        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            if (model.Password.Length < 8)
                throw new ArgumentException("Mật khẩu phải có ít nhất 8 ký tự.");
            passwordHash = PasswordHasher.Hash(model.Password);
        }
        else if (isNew)
        {
            throw new ArgumentException("Tài khoản mới cần có mật khẩu ban đầu.");
        }

        if (employeeId.Equals(actorId, StringComparison.OrdinalIgnoreCase)
            && (!model.IsActive || roleId != "ADMIN"))
        {
            throw new InvalidOperationException("Không thể khóa hoặc đổi vai trò của tài khoản quản lý đang đăng nhập.");
        }

        await _repository.SaveEmployeeAsync(
            employeeId,
            fullName,
            username,
            roleId,
            model.IsActive,
            passwordHash,
            actorId.Trim(),
            isNew,
            ct);

        return employeeId;
    }

    public Task<LoyaltySettingsRow> GetLoyaltySettingsAsync(
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.GetLoyaltySettingsAsync(ct);
    }

    public Task SaveLoyaltySettingsAsync(
        decimal earnPercent,
        decimal moneyPerPoint,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        if (earnPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(earnPercent), "Tỷ lệ tích điểm phải từ 0 đến 100%.");
        if (moneyPerPoint <= 0)
            throw new ArgumentOutOfRangeException(nameof(moneyPerPoint), "Giá trị quy đổi mỗi điểm phải lớn hơn 0.");

        return _repository.SaveLoyaltySettingsAsync(
            earnPercent,
            moneyPerPoint,
            actorId.Trim(),
            ct);
    }

    public Task<List<VoucherRow>> GetVouchersAsync(
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        return _repository.GetVouchersAsync(ct);
    }

    public Task<string> AddVoucherAsync(
        VoucherSaveModel model,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        EnsureManager(actorId, actorRole);

        string code = (model.VoucherCode ?? "").Trim().ToUpperInvariant();
        string type = (model.DiscountType ?? "").Trim().ToUpperInvariant();
        if (!Regex.IsMatch(code, @"^[A-Z0-9_-]{3,40}$"))
            throw new ArgumentException("Mã voucher chỉ gồm chữ in hoa, số, gạch ngang hoặc gạch dưới (3–40 ký tự).");
        if (type is not ("AMOUNT" or "PERCENT"))
            throw new ArgumentException("Loại giảm giá không hợp lệ.");
        if (model.DiscountValue <= 0 || (type == "PERCENT" && model.DiscountValue > 100))
            throw new ArgumentOutOfRangeException(nameof(model.DiscountValue), "Giá trị giảm giá không hợp lệ.");
        if (model.MinimumOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(model.MinimumOrder), "Đơn tối thiểu không được âm.");
        if (model.MaximumDiscount < 0)
            throw new ArgumentOutOfRangeException(nameof(model.MaximumDiscount), "Mức giảm tối đa không được âm.");
        if (model.UsageLimit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(model.UsageLimit), "Giới hạn lượt dùng phải lớn hơn 0.");
        if (model.EndDate <= model.StartDate)
            throw new ArgumentException("Ngày kết thúc phải sau ngày bắt đầu.");

        string voucherId = $"VC{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var voucher = new VoucherRow(
            voucherId,
            code,
            string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            type,
            model.DiscountValue,
            model.MinimumOrder,
            model.MaximumDiscount,
            model.UsageLimit,
            0,
            model.StartDate,
            model.EndDate,
            true);

        return AddVoucherCoreAsync(voucher, actorId.Trim(), ct);
    }

    public Task SetVoucherActiveAsync(
        string voucherId,
        bool isActive,
        string actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        EnsureManager(actorId, actorRole);
        if (string.IsNullOrWhiteSpace(voucherId))
            throw new ArgumentException("Mã voucher không hợp lệ.", nameof(voucherId));

        return _repository.SetVoucherActiveAsync(
            voucherId.Trim(),
            isActive,
            actorId.Trim(),
            ct);
    }

    private async Task<string> AddVoucherCoreAsync(
        VoucherRow voucher,
        string actorId,
        CancellationToken ct)
    {
        await _repository.AddVoucherAsync(voucher, actorId, ct);
        return voucher.VoucherId;
    }

    private static void EnsureManager(string actorId, string actorRole)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Không xác định được tài khoản đang thao tác.", nameof(actorId));
        if (!string.Equals(actorRole, "ADMIN", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Chỉ Quản lý mới được sử dụng chức năng này.");
    }
}

public sealed class EmployeeSaveModel
{
    public string? EmployeeId { get; set; }
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string RoleId { get; set; } = "";
    public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class VoucherSaveModel
{
    public string VoucherCode { get; set; } = "";
    public string? Description { get; set; }
    public string DiscountType { get; set; } = "AMOUNT";
    public decimal DiscountValue { get; set; }
    public decimal MinimumOrder { get; set; }
    public decimal? MaximumDiscount { get; set; }
    public int? UsageLimit { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
