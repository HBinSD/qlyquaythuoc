using Microsoft.Data.SqlClient;
using System.Data;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

/// <summary>
/// Repository cho danh mục thuốc theo schema QuanLyQuayThuoc.
/// Bảng sử dụng: Medicine, MedicineCategory, MedicineUnit, MedicinePriceHistory.
/// </summary>
public sealed class MedicineRepository
{
    public sealed record MedicineRow(
        string MedicineId, string MedicineName, string? CategoryId,
        string? CategoryName, string? ActiveIngredient, string? Strength,
        string? DosageForm, string BaseUnit, decimal MinimumStock,
        bool RequiresPrescription, string? Barcode, string BusinessStatus,
        decimal BaseSalePrice);

    public sealed record CategoryRow(string CategoryId, string CategoryName);

    public async Task<List<MedicineRow>> SearchAsync(
        string? keyword = null, bool includeInactive = true,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT m.MedicineId, m.MedicineName, m.CategoryId, c.CategoryName,
                   m.ActiveIngredient, m.Strength, m.DosageForm, m.BaseUnit,
                   m.MinimumStock, m.RequiresPrescription, m.Barcode, m.BusinessStatus,
                   COALESCE(u.SalePrice, 0) AS BaseSalePrice
            FROM dbo.Medicine AS m
            LEFT JOIN dbo.MedicineCategory AS c ON c.CategoryId = m.CategoryId
            OUTER APPLY
            (
                SELECT TOP (1) mu.SalePrice
                FROM dbo.MedicineUnit AS mu
                WHERE mu.MedicineId = m.MedicineId
                  AND mu.IsBaseUnit = 1 AND mu.IsActive = 1
                ORDER BY mu.MedicineUnitId
            ) AS u
            WHERE (@IncludeInactive = 1 OR m.BusinessStatus = 'ACTIVE')
              AND (@Keyword = N'' OR m.MedicineId LIKE @Pattern
                   OR m.MedicineName LIKE @Pattern
                   OR m.ActiveIngredient LIKE @Pattern OR m.Barcode LIKE @Pattern)
            ORDER BY m.MedicineName, m.MedicineId;
            """;

        var rows = new List<MedicineRow>();
        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn);
        string key = (keyword ?? "").Trim();
        cmd.Parameters.Add("@IncludeInactive", SqlDbType.Bit).Value = includeInactive;
        cmd.Parameters.Add("@Keyword", SqlDbType.NVarChar, 200).Value = key;
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 205).Value = $"%{key}%";

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            rows.Add(new MedicineRow(
                rd.GetString(0), rd.GetString(1),
                rd.IsDBNull(2) ? null : rd.GetString(2),
                rd.IsDBNull(3) ? null : rd.GetString(3),
                rd.IsDBNull(4) ? null : rd.GetString(4),
                rd.IsDBNull(5) ? null : rd.GetString(5),
                rd.IsDBNull(6) ? null : rd.GetString(6),
                rd.GetString(7), rd.GetDecimal(8), rd.GetBoolean(9),
                rd.IsDBNull(10) ? null : rd.GetString(10),
                rd.GetString(11), rd.GetDecimal(12)));
        }
        return rows;
    }

    public async Task<List<CategoryRow>> GetCategoriesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT CategoryId, CategoryName
            FROM dbo.MedicineCategory
            WHERE IsActive = 1
            ORDER BY CategoryName;
            """;

        var rows = new List<CategoryRow>();
        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            rows.Add(new CategoryRow(rd.GetString(0), rd.GetString(1)));
        return rows;
    }

    public async Task SaveAsync(
        string? medicineId, string medicineName, string? categoryId,
        string? activeIngredient, string? strength, string? dosageForm,
        string baseUnit, decimal minimumStock, bool requiresPrescription,
        string? barcode, string? description, decimal baseSalePrice,
        string changedBy, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(medicineName))
            throw new ArgumentException("Tên thuốc không được để trống.");
        if (string.IsNullOrWhiteSpace(baseUnit))
            throw new ArgumentException("Đơn vị cơ bản không được để trống.");
        if (minimumStock < 0 || baseSalePrice < 0)
            throw new ArgumentException("Tồn tối thiểu và giá bán không được âm.");

        bool isNew = string.IsNullOrWhiteSpace(medicineId);
        string id = isNew ? $"MED{Guid.NewGuid():N}"[..20].ToUpperInvariant() : medicineId!.Trim();

        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

        try
        {
            if (isNew)
            {
                const string sql = """
                    INSERT dbo.Medicine
                    (MedicineId,CategoryId,MedicineName,ActiveIngredient,Strength,DosageForm,
                     BaseUnit,MinimumStock,RequiresPrescription,Barcode,Description,BusinessStatus)
                    VALUES
                    (@Id,@CategoryId,@Name,@Ingredient,@Strength,@Form,
                     @Unit,@MinimumStock,@Prescription,@Barcode,@Description,'ACTIVE');

                    INSERT dbo.MedicineUnit
                    (MedicineId,UnitName,ConversionFactor,SalePrice,IsBaseUnit,IsActive)
                    VALUES (@Id,@Unit,1,@Price,1,1);
                    """;
                await using var cmd = new SqlCommand(sql, cn, tx);
                AddParameters(cmd, id, medicineName, categoryId, activeIngredient, strength,
                    dosageForm, baseUnit, minimumStock, requiresPrescription, barcode, description, baseSalePrice);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            else
            {
                const string sql = """
                    UPDATE dbo.Medicine
                    SET CategoryId=@CategoryId, MedicineName=@Name,
                        ActiveIngredient=@Ingredient, Strength=@Strength, DosageForm=@Form,
                        BaseUnit=@Unit, MinimumStock=@MinimumStock,
                        RequiresPrescription=@Prescription, Barcode=@Barcode, Description=@Description
                    WHERE MedicineId=@Id;
                    IF @@ROWCOUNT=0 THROW 50001,N'Không tìm thấy thuốc cần cập nhật.',1;

                    DECLARE @UnitId int, @OldPrice decimal(18,2);
                    SELECT TOP(1) @UnitId=MedicineUnitId,@OldPrice=SalePrice
                    FROM dbo.MedicineUnit
                    WHERE MedicineId=@Id AND IsBaseUnit=1 AND IsActive=1
                    ORDER BY MedicineUnitId;

                    IF @UnitId IS NULL
                        INSERT dbo.MedicineUnit
                        (MedicineId,UnitName,ConversionFactor,SalePrice,IsBaseUnit,IsActive)
                        VALUES(@Id,@Unit,1,@Price,1,1);
                    ELSE
                    BEGIN
                        IF @OldPrice<>@Price
                            INSERT dbo.MedicinePriceHistory
                            (MedicineUnitId,OldPrice,NewPrice,ChangedBy,Note)
                            VALUES(@UnitId,@OldPrice,@Price,@ChangedBy,N'Cập nhật giá từ danh mục thuốc');
                        UPDATE dbo.MedicineUnit SET UnitName=@Unit,SalePrice=@Price
                        WHERE MedicineUnitId=@UnitId;
                    END;
                    """;
                await using var cmd = new SqlCommand(sql, cn, tx);
                AddParameters(cmd, id, medicineName, categoryId, activeIngredient, strength,
                    dosageForm, baseUnit, minimumStock, requiresPrescription, barcode, description, baseSalePrice);
                cmd.Parameters.Add("@ChangedBy", SqlDbType.VarChar, 20).Value = changedBy;
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task SetStatusAsync(string medicineId, bool active, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Medicine SET BusinessStatus=@Status WHERE MedicineId=@Id;
            IF @@ROWCOUNT=0 THROW 50002,N'Không tìm thấy thuốc.',1;
            """;
        await using var cn = DbConnectionFactory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Status", SqlDbType.VarChar, 20).Value = active ? "ACTIVE" : "INACTIVE";
        cmd.Parameters.Add("@Id", SqlDbType.VarChar, 20).Value = medicineId;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddParameters(SqlCommand cmd, string id, string name, string? categoryId,
        string? ingredient, string? strength, string? form, string unit, decimal minimumStock,
        bool prescription, string? barcode, string? description, decimal price)
    {
        cmd.Parameters.Add("@Id", SqlDbType.VarChar, 20).Value = id;
        cmd.Parameters.Add("@CategoryId", SqlDbType.VarChar, 20).Value = Db(categoryId);
        cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = name.Trim();
        cmd.Parameters.Add("@Ingredient", SqlDbType.NVarChar, 200).Value = Db(ingredient);
        cmd.Parameters.Add("@Strength", SqlDbType.NVarChar, 100).Value = Db(strength);
        cmd.Parameters.Add("@Form", SqlDbType.NVarChar, 100).Value = Db(form);
        cmd.Parameters.Add("@Unit", SqlDbType.NVarChar, 30).Value = unit.Trim();
        var min = cmd.Parameters.Add("@MinimumStock", SqlDbType.Decimal);
        min.Precision = 18; min.Scale = 3; min.Value = minimumStock;
        cmd.Parameters.Add("@Prescription", SqlDbType.Bit).Value = prescription;
        cmd.Parameters.Add("@Barcode", SqlDbType.VarChar, 50).Value = Db(barcode);
        cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 1000).Value = Db(description);
        var p = cmd.Parameters.Add("@Price", SqlDbType.Decimal);
        p.Precision = 18; p.Scale = 2; p.Value = price;
    }

    private static object Db(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
}
