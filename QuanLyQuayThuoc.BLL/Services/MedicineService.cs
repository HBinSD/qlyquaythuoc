using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.BLL.Services
{
    /// <summary>
    /// Xử lý nghiệp vụ quản lý thuốc và kiểm tra dữ liệu trước khi gọi Repository.
    /// </summary>
    public sealed class MedicineService
    {
        private readonly MedicineRepository _repository;

        public MedicineService()
            : this(new MedicineRepository())
        {
        }

        public MedicineService(MedicineRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<IReadOnlyList<MedicineListItem>> SearchAsync(
            string? keyword = null,
            string? categoryId = null,
            string? businessStatus = null,
            CancellationToken cancellationToken = default)
        {
            // Repository hiện hỗ trợ tìm theo từ khóa và includeInactive.
            // Lọc nhóm thuốc/trạng thái bổ sung tại Service để không cần sửa Repository.
            bool includeInactive = string.IsNullOrWhiteSpace(businessStatus)
                || businessStatus.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                || businessStatus.Equals("INACTIVE", StringComparison.OrdinalIgnoreCase)
                || businessStatus.Equals("Ngừng kinh doanh", StringComparison.OrdinalIgnoreCase);

            var rows = await _repository.SearchAsync(keyword, includeInactive, cancellationToken);

            IEnumerable<MedicineRepository.MedicineRow> filtered = rows;

            if (!string.IsNullOrWhiteSpace(categoryId))
                filtered = filtered.Where(x =>
                    string.Equals(x.CategoryId, categoryId.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(businessStatus)
                && !businessStatus.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                bool wantActive = businessStatus.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)
                    || businessStatus.Equals("Đang kinh doanh", StringComparison.OrdinalIgnoreCase);

                filtered = filtered.Where(x =>
                    string.Equals(x.BusinessStatus, wantActive ? "ACTIVE" : "INACTIVE",
                        StringComparison.OrdinalIgnoreCase));
            }

            return filtered.Select(x => new MedicineListItem
            {
                MedicineId = x.MedicineId,
                MedicineName = x.MedicineName,
                CategoryId = x.CategoryId,
                CategoryName = x.CategoryName,
                ActiveIngredient = x.ActiveIngredient,
                Strength = x.Strength,
                DosageForm = x.DosageForm,
                BaseUnit = x.BaseUnit,
                MinimumStock = x.MinimumStock,
                RequiresPrescription = x.RequiresPrescription,
                Barcode = x.Barcode,
                BusinessStatus = x.BusinessStatus,
                SalePrice = x.BaseSalePrice
            }).ToList();
        }

        public async Task<IReadOnlyList<MedicineCategoryItem>> GetCategoriesAsync(
            CancellationToken cancellationToken = default)
        {
            var categories = await _repository.GetCategoriesAsync(cancellationToken);

            return categories.Select(x => new MedicineCategoryItem
            {
                CategoryId = x.CategoryId,
                CategoryName = x.CategoryName
            }).ToList();
        }

        public async Task SaveAsync(
            MedicineSaveModel model,
            string changedBy,
            CancellationToken cancellationToken = default)
        {
            ValidateMedicine(model);

            if (string.IsNullOrWhiteSpace(changedBy))
                throw new ArgumentException("Mã nhân viên thực hiện không được để trống.", nameof(changedBy));

            await _repository.SaveAsync(
                model.MedicineId,
                model.MedicineName.Trim(),
                model.CategoryId,
                NormalizeOptional(model.ActiveIngredient),
                NormalizeOptional(model.Strength),
                NormalizeOptional(model.DosageForm),
                model.BaseUnit.Trim(),
                model.MinimumStock,
                model.RequiresPrescription,
                NormalizeOptional(model.Barcode),
                NormalizeOptional(model.Description),
                model.SalePrice,
                changedBy.Trim(),
                cancellationToken);
        }

        public Task SetStatusAsync(
            string medicineId,
            string businessStatus,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(medicineId))
                throw new ArgumentException("Mã thuốc không được để trống.", nameof(medicineId));

            if (string.IsNullOrWhiteSpace(businessStatus))
                throw new ArgumentException("Trạng thái thuốc không được để trống.", nameof(businessStatus));

            bool active;
            if (businessStatus.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)
                || businessStatus.Equals("Đang kinh doanh", StringComparison.OrdinalIgnoreCase))
            {
                active = true;
            }
            else if (businessStatus.Equals("INACTIVE", StringComparison.OrdinalIgnoreCase)
                || businessStatus.Equals("Ngừng kinh doanh", StringComparison.OrdinalIgnoreCase))
            {
                active = false;
            }
            else
            {
                throw new ArgumentException(
                    "Trạng thái phải là ACTIVE/INACTIVE hoặc Đang kinh doanh/Ngừng kinh doanh.",
                    nameof(businessStatus));
            }

            return _repository.SetStatusAsync(medicineId.Trim(), active, cancellationToken);
        }

        private static void ValidateMedicine(MedicineSaveModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            if (string.IsNullOrWhiteSpace(model.MedicineName))
                throw new ArgumentException("Tên thuốc không được để trống.");

            if (string.IsNullOrWhiteSpace(model.CategoryId))
                throw new ArgumentException("Vui lòng chọn nhóm thuốc.");

            if (string.IsNullOrWhiteSpace(model.BaseUnit))
                throw new ArgumentException("Đơn vị tính cơ bản không được để trống.");

            if (model.MinimumStock < 0)
                throw new ArgumentOutOfRangeException(nameof(model.MinimumStock),
                    "Tồn kho tối thiểu không được nhỏ hơn 0.");

            if (model.SalePrice < 0)
                throw new ArgumentOutOfRangeException(nameof(model.SalePrice),
                    "Giá bán không được nhỏ hơn 0.");
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    public sealed class MedicineListItem
    {
        public string MedicineId { get; set; } = string.Empty;
        public string MedicineName { get; set; } = string.Empty;
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? ActiveIngredient { get; set; }
        public string? Strength { get; set; }
        public string? DosageForm { get; set; }
        public string BaseUnit { get; set; } = string.Empty;
        public decimal MinimumStock { get; set; }
        public bool RequiresPrescription { get; set; }
        public string? Barcode { get; set; }
        public string? BusinessStatus { get; set; }
        public decimal SalePrice { get; set; }
    }

    public sealed class MedicineCategoryItem
    {
        public string CategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }

    public sealed class MedicineSaveModel
    {
        public string? MedicineId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public string? ActiveIngredient { get; set; }
        public string? Strength { get; set; }
        public string? DosageForm { get; set; }
        public string BaseUnit { get; set; } = string.Empty;
        public decimal MinimumStock { get; set; }
        public bool RequiresPrescription { get; set; }
        public string? Barcode { get; set; }
        public string? Description { get; set; }
        public decimal SalePrice { get; set; }
    }
}