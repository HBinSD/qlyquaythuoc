using System.Data;
using System.Numerics;
using Microsoft.Data.SqlClient;
using QuanLyQuayThuoc.DAL.Database;

namespace QuanLyQuayThuoc.DAL.Repositories;

public sealed record SalesCustomerRow(string CustomerId, string FullName, string Phone);

public sealed record PrescriptionLineInput(
    string MedicineId,
    string PrescribedUnit,
    decimal PrescribedQuantity,
    string? DosageInstruction = null);

public sealed record CreatePrescriptionRequest(
    string CreatedBy,
    string PatientName,
    string? PatientPhone,
    DateOnly PrescribedDate,
    string? DoctorName,
    string? MedicalFacility,
    IReadOnlyCollection<PrescriptionLineInput> Lines);

public sealed record CreateCustomerRequest(
    string FullName,
    string Phone,
    string? Email = null,
    string? Address = null);

public sealed record SalesLineInput(string MedicineId, int MedicineUnitId, decimal Quantity);

public sealed record CreateInvoiceDraftRequest(
    string EmployeeId,
    string ShiftId,
    string? CustomerId,
    string? PrescriptionId,
    string? VoucherCode,
    int PointsToRedeem,
    IReadOnlyCollection<SalesLineInput> Lines);

public sealed record DiscountQuote(
    decimal Subtotal,
    decimal PointDiscount,
    decimal VoucherDiscount,
    decimal TotalAmount,
    int PointsToRedeem,
    int AvailablePoints,
    string? VoucherId,
    string? VoucherCode);

public sealed record InvoiceDraftResult(
    string InvoiceId,
    decimal Subtotal,
    decimal PointDiscount,
    decimal VoucherDiscount,
    decimal TotalAmount,
    DateTime ExpiresAt);

public sealed record PendingInvoiceDraftRow(
    string InvoiceId,
    decimal TotalAmount,
    DateTime ExpiresAt);

public sealed record SettleInvoiceDraftRequest(
    string InvoiceId,
    string EmployeeId,
    string ShiftId,
    decimal Amount,
    string PaymentMethod,
    string? TransactionReference = null);

public sealed record InvoiceSettlementResult(
    string InvoiceId,
    decimal AmountPaid,
    int PointsEarned);

public sealed class SalesWorkflowRepository
{
    private const int ReservationMinutes = 30;
    private const string DraftPointReservationNote = "DRAFT_RESERVATION";

    private sealed record PreparedLine(
        string MedicineId,
        int MedicineUnitId,
        string UnitName,
        decimal ConversionFactor,
        decimal UnitPrice,
        decimal Quantity,
        decimal QuantityBase,
        decimal GrossTotal,
        bool RequiresPrescription);

    private sealed record DraftDetail(
        long DetailId,
        string MedicineId,
        int MedicineUnitId,
        string BatchId,
        string UnitName,
        decimal ConversionFactor,
        decimal Quantity,
        decimal QuantityBase,
        decimal UnitPrice,
        decimal LineTotal);

    private sealed record VoucherInfo(
        string VoucherId,
        string VoucherCode,
        string DiscountType,
        decimal DiscountValue,
        decimal MinimumOrder,
        decimal? MaximumDiscount,
        int? UsageLimit,
        int UsedCount,
        DateTime StartDate,
        DateTime EndDate,
        bool IsActive);

    private sealed record DiscountContext(
        decimal PointDiscount,
        decimal VoucherDiscount,
        int AvailablePoints,
        int PointsToRedeem,
        VoucherInfo? Voucher);

    public async Task<List<SalesCustomerRow>> SearchCustomersByPhoneAsync(
        string phonePrefix,
        CancellationToken ct = default)
    {
        string prefix = NormalizePhone(phonePrefix);
        if (prefix.Length == 0)
            throw new ArgumentException("Vui lòng nhập số điện thoại cần tìm.", nameof(phonePrefix));

        const string sql = """
            SELECT TOP (50) CustomerId, FullName, Phone
            FROM dbo.Customer
            WHERE IsActive = 1 AND Phone LIKE @Pattern
            ORDER BY Phone;
            """;

        var rows = new List<SalesCustomerRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Pattern", SqlDbType.VarChar, 100).Value = EscapeLike(prefix) + "%";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new SalesCustomerRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return rows;
    }

    public async Task<SalesCustomerRow> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string name = Required(request.FullName, 150, "Tên khách hàng");
        string phone = NormalizePhone(request.Phone);
        if (phone.Length is < 7 or > 20)
            throw new ArgumentException("Số điện thoại phải có từ 7 đến 20 ký tự.", nameof(request));

        string? email = Optional(request.Email, 150);
        string? address = Optional(request.Address, 300);
        string customerId = NewId("CUS", 17);

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        try
        {
            const string existingSql = """
                SELECT CustomerId, FullName, Phone, IsActive
                FROM dbo.Customer WITH (UPDLOCK, HOLDLOCK)
                WHERE Phone = @Phone;
                """;
            await using (var command = new SqlCommand(existingSql, connection, transaction))
            {
                command.Parameters.Add("@Phone", SqlDbType.VarChar, 20).Value = phone;
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    if (!reader.GetBoolean(3))
                        throw new InvalidOperationException("Số điện thoại đã thuộc khách hàng ngừng hoạt động.");
                    var existing = new SalesCustomerRow(reader.GetString(0), reader.GetString(1), reader.GetString(2));
                    await reader.CloseAsync();
                    await transaction.CommitAsync(ct);
                    return existing;
                }
            }

            const string insertSql = """
                INSERT dbo.Customer(CustomerId, FullName, Phone, Email, Address, LoyaltyPoints, IsActive)
                VALUES (@CustomerId, @FullName, @Phone, @Email, @Address, 0, 1);
                """;
            await using (var command = new SqlCommand(insertSql, connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
                command.Parameters.Add("@FullName", SqlDbType.NVarChar, 150).Value = name;
                command.Parameters.Add("@Phone", SqlDbType.VarChar, 20).Value = phone;
                command.Parameters.Add("@Email", SqlDbType.VarChar, 150).Value = Db(email);
                command.Parameters.Add("@Address", SqlDbType.NVarChar, 300).Value = Db(address);
                await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return new SalesCustomerRow(customerId, name, phone);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<string> CreatePrescriptionAsync(
        CreatePrescriptionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string employeeId = Required(request.CreatedBy, 20, "Mã nhân viên");
        string patientName = Required(request.PatientName, 150, "Tên bệnh nhân");
        string? phone = Optional(request.PatientPhone, 20);
        string? doctor = Optional(request.DoctorName, 150);
        string? facility = Optional(request.MedicalFacility, 200);
        if (request.PrescribedDate > DateOnly.FromDateTime(DateTime.Today))
            throw new ArgumentException("Ngày kê đơn không được nằm trong tương lai.", nameof(request));
        if (request.Lines is null || request.Lines.Count == 0)
            throw new ArgumentException("Đơn thuốc phải có ít nhất một thuốc.", nameof(request));
        if (request.Lines.Count > 100)
            throw new ArgumentException("Đơn thuốc vượt quá số dòng cho phép.", nameof(request));

        var lines = request.Lines.Select(line =>
        {
            ArgumentNullException.ThrowIfNull(line);
            string medicineId = Required(line.MedicineId, 20, "Mã thuốc");
            string unit = Required(line.PrescribedUnit, 30, "Đơn vị kê");
            ValidateQuantity(line.PrescribedQuantity, "Số lượng kê");
            return line with
            {
                MedicineId = medicineId,
                PrescribedUnit = unit,
                DosageInstruction = Optional(line.DosageInstruction, 500)
            };
        }).ToList();

        string prescriptionId = NewId("RX", 18);
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        try
        {
            const string employeeSql = """
                SELECT COUNT(1) FROM dbo.Employee WITH (UPDLOCK, HOLDLOCK)
                WHERE EmployeeId = @EmployeeId AND IsActive = 1;
                """;
            await using (var command = new SqlCommand(employeeSql, connection, transaction))
            {
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
                    throw new InvalidOperationException("Nhân viên không tồn tại hoặc đã ngừng hoạt động.");
            }

            const string unitSql = """
                SELECT COUNT(1)
                FROM dbo.Medicine m WITH (HOLDLOCK)
                INNER JOIN dbo.MedicineUnit u WITH (HOLDLOCK) ON u.MedicineId = m.MedicineId
                WHERE m.MedicineId = @MedicineId AND m.BusinessStatus = 'ACTIVE'
                  AND u.UnitName = @UnitName AND u.IsActive = 1;
                """;
            foreach (var line in lines)
            {
                await using var command = new SqlCommand(unitSql, connection, transaction);
                command.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
                command.Parameters.Add("@UnitName", SqlDbType.NVarChar, 30).Value = line.PrescribedUnit;
                if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
                    throw new InvalidOperationException(
                        $"Thuốc hoặc đơn vị kê '{line.MedicineId} - {line.PrescribedUnit}' không còn hợp lệ.");
            }

            const string headerSql = """
                INSERT dbo.Prescription
                    (PrescriptionId, PatientName, PatientPhone, PrescribedDate,
                     DoctorName, MedicalFacility, CreatedBy, Status)
                VALUES
                    (@PrescriptionId, @PatientName, @PatientPhone, @PrescribedDate,
                     @DoctorName, @MedicalFacility, @CreatedBy, 'UNSOLD');
                """;
            await using (var command = new SqlCommand(headerSql, connection, transaction))
            {
                command.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
                command.Parameters.Add("@PatientName", SqlDbType.NVarChar, 150).Value = patientName;
                command.Parameters.Add("@PatientPhone", SqlDbType.VarChar, 20).Value = Db(phone);
                command.Parameters.Add("@PrescribedDate", SqlDbType.Date).Value =
                    request.PrescribedDate.ToDateTime(TimeOnly.MinValue);
                command.Parameters.Add("@DoctorName", SqlDbType.NVarChar, 150).Value = Db(doctor);
                command.Parameters.Add("@MedicalFacility", SqlDbType.NVarChar, 200).Value = Db(facility);
                command.Parameters.Add("@CreatedBy", SqlDbType.VarChar, 20).Value = employeeId;
                await command.ExecuteNonQueryAsync(ct);
            }

            const string detailSql = """
                INSERT dbo.PrescriptionDetail
                    (PrescriptionId, MedicineId, PrescribedUnit, PrescribedQuantity, DosageInstruction)
                VALUES
                    (@PrescriptionId, @MedicineId, @UnitName, @Quantity, @Dosage);
                """;
            foreach (var line in lines)
            {
                await using var command = new SqlCommand(detailSql, connection, transaction);
                command.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
                command.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
                command.Parameters.Add("@UnitName", SqlDbType.NVarChar, 30).Value = line.PrescribedUnit;
                AddQuantity(command, "@Quantity", line.PrescribedQuantity);
                command.Parameters.Add("@Dosage", SqlDbType.NVarChar, 500).Value = Db(line.DosageInstruction);
                await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return prescriptionId;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<DiscountQuote> CalculateDiscountsAsync(
        string? customerId,
        decimal subtotal,
        string? voucherCode,
        int pointsToRedeem,
        CancellationToken ct = default)
    {
        ValidateMoney(subtotal, nameof(subtotal), allowZero: true);
        if (pointsToRedeem < 0)
            throw new ArgumentOutOfRangeException(nameof(pointsToRedeem), "Số điểm đổi không được âm.");

        string? normalizedCustomerId = Optional(customerId, 20);
        string? normalizedVoucherCode = Optional(voucherCode, 40);
        if (pointsToRedeem > 0 && normalizedVoucherCode is not null)
            throw new ArgumentException("Mỗi hóa đơn chỉ áp dụng điểm tích lũy hoặc voucher, không áp dụng đồng thời.");

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        try
        {
            DiscountContext context = await CalculateDiscountContextAsync(
                connection, transaction, normalizedCustomerId, subtotal,
                normalizedVoucherCode, pointsToRedeem, invoiceIdToExclude: null, ct);
            await transaction.CommitAsync(ct);
            decimal total = subtotal - context.PointDiscount - context.VoucherDiscount;
            return new DiscountQuote(
                subtotal,
                context.PointDiscount,
                context.VoucherDiscount,
                total,
                context.PointsToRedeem,
                context.AvailablePoints,
                context.Voucher?.VoucherId,
                context.Voucher?.VoucherCode);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<PendingInvoiceDraftRow>> GetPendingInvoiceDraftsAsync(
        string shiftId,
        CancellationToken ct = default)
    {
        string normalizedShiftId = Required(shiftId, 20, "Mã ca");
        const string sql = """
            SELECT i.InvoiceId, i.TotalAmount, MIN(ir.ExpiresAt) AS ExpiresAt
            FROM dbo.Invoice i
            INNER JOIN dbo.InventoryReservation ir ON ir.InvoiceId = i.InvoiceId
            WHERE i.ShiftId = @ShiftId
              AND i.Status = 'PENDING'
              AND ir.Status = 'ACTIVE'
            GROUP BY i.InvoiceId, i.TotalAmount
            HAVING MIN(ir.ExpiresAt) > SYSDATETIME()
            ORDER BY MIN(ir.ExpiresAt), i.InvoiceId;
            """;

        var drafts = new List<PendingInvoiceDraftRow>();
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = normalizedShiftId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            drafts.Add(new PendingInvoiceDraftRow(
                reader.GetString(0),
                reader.GetDecimal(1),
                reader.GetDateTime(2)));
        }
        return drafts;
    }

    public async Task<InvoiceDraftResult> CreateInvoiceDraftAsync(
        CreateInvoiceDraftRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string employeeId = Required(request.EmployeeId, 20, "Mã nhân viên");
        string shiftId = Required(request.ShiftId, 20, "Mã ca");
        string? customerId = Optional(request.CustomerId, 20);
        string? prescriptionId = Optional(request.PrescriptionId, 20);
        string? voucherCode = Optional(request.VoucherCode, 40);
        if (request.PointsToRedeem < 0)
            throw new ArgumentOutOfRangeException(nameof(request.PointsToRedeem), "Số điểm đổi không được âm.");
        if (request.PointsToRedeem > 0 && voucherCode is not null)
            throw new ArgumentException("Mỗi hóa đơn chỉ áp dụng điểm tích lũy hoặc voucher, không áp dụng đồng thời.", nameof(request));
        if (request.Lines is null || request.Lines.Count == 0)
            throw new ArgumentException("Hóa đơn chưa có thuốc.", nameof(request));
        if (request.Lines.Count > 200)
            throw new ArgumentException("Hóa đơn vượt quá số dòng cho phép.", nameof(request));

        var groupedLines = request.Lines
            .Select(line =>
            {
                ArgumentNullException.ThrowIfNull(line);
                string medicineId = Required(line.MedicineId, 20, "Mã thuốc");
                if (line.MedicineUnitId <= 0)
                    throw new ArgumentException("Đơn vị bán không hợp lệ.", nameof(request));
                ValidateQuantity(line.Quantity, "Số lượng bán");
                if (line.Quantity > 100000m)
                    throw new ArgumentException("Số lượng bán vượt giới hạn cho phép.", nameof(request));
                return new SalesLineInput(medicineId, line.MedicineUnitId, line.Quantity);
            })
            .GroupBy(line => new { line.MedicineId, line.MedicineUnitId })
            .Select(group => new SalesLineInput(
                group.Key.MedicineId,
                group.Key.MedicineUnitId,
                group.Sum(line => line.Quantity)))
            .ToList();
        foreach (var line in groupedLines)
        {
            ValidateQuantity(line.Quantity, "Tổng số lượng bán");
            if (line.Quantity > 100000m)
                throw new ArgumentException("Tổng số lượng bán vượt giới hạn cho phép.", nameof(request));
        }

        string invoiceId = NewId("INV", 17);
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        try
        {
            await ExpirePendingDraftsAsync(connection, transaction, ct);
            await ValidateOpenShiftAsync(connection, transaction, shiftId, employeeId, ct);
            await ValidateCustomerAsync(connection, transaction, customerId, ct);

            var preparedLines = new List<PreparedLine>(groupedLines.Count);
            foreach (var line in groupedLines)
                preparedLines.Add(await PrepareLineAsync(connection, transaction, line, ct));

            decimal subtotal = preparedLines.Sum(line => line.GrossTotal);
            ValidateMoney(subtotal, "Tổng tiền", allowZero: false);

            bool requiresPrescription = preparedLines.Any(line => line.RequiresPrescription);
            if (requiresPrescription && prescriptionId is null)
                throw new InvalidOperationException("Giỏ hàng có thuốc kê đơn. Vui lòng chọn đơn thuốc hợp lệ.");
            if (prescriptionId is not null)
                await ValidatePrescriptionQuantitiesAsync(
                    connection, transaction, prescriptionId, preparedLines,
                    invoiceIdToExclude: null, ct);

            DiscountContext discounts = await CalculateDiscountContextAsync(
                connection, transaction, customerId, subtotal, voucherCode,
                request.PointsToRedeem, invoiceIdToExclude: null, ct);
            decimal total = subtotal - discounts.PointDiscount - discounts.VoucherDiscount;
            if (total <= 0)
                throw new InvalidOperationException("Số tiền phải thanh toán phải lớn hơn 0.");

            const string invoiceSql = """
                INSERT dbo.Invoice
                    (InvoiceId, ShiftId, EmployeeId, CustomerId, PrescriptionId,
                     Subtotal, PointDiscount, VoucherDiscount, TotalAmount, AmountPaid, Status)
                VALUES
                    (@InvoiceId, @ShiftId, @EmployeeId, @CustomerId, @PrescriptionId,
                     @Subtotal, @PointDiscount, @VoucherDiscount, @TotalAmount, 0, 'PENDING');
                """;
            await using (var command = new SqlCommand(invoiceSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = Db(customerId);
                command.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = Db(prescriptionId);
                AddMoney(command, "@Subtotal", subtotal);
                AddMoney(command, "@PointDiscount", discounts.PointDiscount);
                AddMoney(command, "@VoucherDiscount", discounts.VoucherDiscount);
                AddMoney(command, "@TotalAmount", total);
                await command.ExecuteNonQueryAsync(ct);
            }

            var details = new List<DraftDetail>();
            foreach (PreparedLine line in preparedLines)
            {
                List<(string BatchId, decimal Quantity, decimal QuantityBase)> allocations =
                    await ReserveLineInFefoAsync(connection, transaction, invoiceId, line, ct);
                long[] lineCents = AllocateCents(
                    ToCents(line.GrossTotal),
                    allocations.Select(item => ToThousandths(item.Quantity)).ToArray(),
                    allocations.Select(_ => ToCents(line.GrossTotal)).ToArray());

                for (int index = 0; index < allocations.Count; index++)
                {
                    var allocation = allocations[index];
                    decimal lineTotal = FromCents(lineCents[index]);
                    long detailId = await InsertInvoiceDetailAsync(
                        connection, transaction, invoiceId, line, allocation, lineTotal, ct);
                    await InsertReservationAsync(
                        connection, transaction, invoiceId, detailId, allocation.BatchId,
                        allocation.QuantityBase, ct);
                    details.Add(new DraftDetail(
                        detailId, line.MedicineId, line.MedicineUnitId, allocation.BatchId,
                        line.UnitName, line.ConversionFactor, allocation.Quantity,
                        allocation.QuantityBase, line.UnitPrice, lineTotal));
                }
            }

            await AllocateInvoiceDiscountsAsync(
                connection, transaction, details, subtotal,
                discounts.PointDiscount + discounts.VoucherDiscount, ct);
            if (discounts.Voucher is not null)
            {
                const string usageSql = """
                    INSERT dbo.VoucherUsage(VoucherId, InvoiceId, CustomerId, DiscountAmount)
                    VALUES (@VoucherId, @InvoiceId, @CustomerId, @DiscountAmount);
                    """;
                await using var command = new SqlCommand(usageSql, connection, transaction);
                command.Parameters.Add("@VoucherId", SqlDbType.VarChar, 20).Value = discounts.Voucher.VoucherId;
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = Db(customerId);
                AddMoney(command, "@DiscountAmount", discounts.VoucherDiscount);
                await command.ExecuteNonQueryAsync(ct);
            }

            if (discounts.PointsToRedeem > 0)
            {
                const string pointsReservationSql = """
                    INSERT dbo.LoyaltyTransaction
                        (CustomerId, TransactionType, Points, MoneyValue, ReferenceCode, Note)
                    VALUES
                        (@CustomerId, 'REDEEM', @Points, @MoneyValue, @InvoiceId, @Note);
                    """;
                await using var command = new SqlCommand(pointsReservationSql, connection, transaction);
                command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId!;
                command.Parameters.Add("@Points", SqlDbType.Int).Value = discounts.PointsToRedeem;
                AddMoney(command, "@MoneyValue", discounts.PointDiscount);
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                command.Parameters.Add("@Note", SqlDbType.NVarChar, 300).Value = DraftPointReservationNote;
                await command.ExecuteNonQueryAsync(ct);
            }

            const string expirySql = """
                SELECT MIN(ExpiresAt) FROM dbo.InventoryReservation WHERE InvoiceId = @InvoiceId;
                """;
            DateTime expiresAt;
            await using (var command = new SqlCommand(expirySql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                object? value = await command.ExecuteScalarAsync(ct);
                if (value is not DateTime expiry)
                    throw new InvalidOperationException("Không thể xác định thời hạn giữ hàng của hóa đơn.");
                expiresAt = expiry;
            }

            await transaction.CommitAsync(ct);
            return new InvoiceDraftResult(
                invoiceId, subtotal, discounts.PointDiscount, discounts.VoucherDiscount, total, expiresAt);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<InvoiceSettlementResult> SettleInvoiceDraftAsync(
        SettleInvoiceDraftRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string invoiceId = Required(request.InvoiceId, 20, "Mã hóa đơn");
        string employeeId = Required(request.EmployeeId, 20, "Mã nhân viên");
        string shiftId = Required(request.ShiftId, 20, "Mã ca");
        string paymentMethod = (request.PaymentMethod ?? "").Trim().ToUpperInvariant();
        if (paymentMethod is not ("CASH" or "TRANSFER" or "QR"))
            throw new ArgumentException("Phương thức thanh toán chỉ được là CASH, TRANSFER hoặc QR.", nameof(request));
        ValidateMoney(request.Amount, nameof(request.Amount), allowZero: false);
        string? reference = Optional(request.TransactionReference, 100);

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        bool committed = false;
        bool expired = false;
        InvoiceSettlementResult? result = null;
        try
        {
            const string invoiceSql = """
                SELECT i.ShiftId, i.EmployeeId, i.CustomerId, i.PrescriptionId,
                       i.Subtotal, i.PointDiscount, i.VoucherDiscount, i.TotalAmount, i.Status
                FROM dbo.Invoice i WITH (UPDLOCK, HOLDLOCK)
                WHERE i.InvoiceId = @InvoiceId;
                """;
            string draftShift;
            string draftEmployee;
            string? customerId;
            string? prescriptionId;
            decimal subtotal;
            decimal pointDiscount;
            decimal voucherDiscount;
            decimal total;
            string status;
            await using (var command = new SqlCommand(invoiceSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Hóa đơn nháp không tồn tại.");
                draftShift = reader.GetString(0);
                draftEmployee = reader.GetString(1);
                customerId = reader.IsDBNull(2) ? null : reader.GetString(2);
                prescriptionId = reader.IsDBNull(3) ? null : reader.GetString(3);
                subtotal = reader.GetDecimal(4);
                pointDiscount = reader.GetDecimal(5);
                voucherDiscount = reader.GetDecimal(6);
                total = reader.GetDecimal(7);
                status = reader.GetString(8);
            }
            if (status != "PENDING")
                throw new InvalidOperationException("Chỉ hóa đơn đang chờ thanh toán mới có thể tất toán.");
            if (draftShift != shiftId || draftEmployee != employeeId)
                throw new InvalidOperationException("Hóa đơn nháp không thuộc ca hoặc nhân viên đang đăng nhập.");

            DateTime expiry = await GetInvoiceExpiryAsync(connection, transaction, invoiceId, ct);
            if (expiry <= await GetDatabaseTimeAsync(connection, transaction, ct))
            {
                await ExpireInvoiceAsync(connection, transaction, invoiceId, employeeId, ct);
                await transaction.CommitAsync(ct);
                committed = true;
                expired = true;
            }
            else
            {
                if (request.Amount != total)
                    throw new InvalidOperationException(
                        $"Thanh toán phải đúng toàn bộ số tiền phải trả ({total:0.00}); không hỗ trợ thanh toán thiếu hoặc thừa.");
                await ValidateOpenShiftAsync(connection, transaction, shiftId, employeeId, ct);
                if (prescriptionId is not null)
                    await ValidateDraftPrescriptionAtSettlementAsync(
                        connection, transaction, invoiceId, prescriptionId, ct);
                await ValidateVoucherReservationAtSettlementAsync(
                    connection, transaction, invoiceId, voucherDiscount, ct);
                await ConsumeReservedStockAsync(connection, transaction, invoiceId, employeeId, ct);
                await SettleVoucherUsageAsync(connection, transaction, invoiceId, voucherDiscount, ct);
                await SettleRedeemedPointsAsync(
                    connection, transaction, invoiceId, customerId, pointDiscount, ct);
                int earnedPoints = await EarnLoyaltyPointsAsync(
                    connection, transaction, invoiceId, customerId, total, ct);

                const string paymentSql = """
                    INSERT dbo.InvoicePayment(InvoiceId, PaymentMethod, Amount, TransactionReference)
                    VALUES (@InvoiceId, @PaymentMethod, @Amount, @Reference);

                    UPDATE dbo.Invoice
                    SET Status = 'PAID', PaidAt = SYSDATETIME(), AmountPaid = @Amount
                    WHERE InvoiceId = @InvoiceId AND Status = 'PENDING';
                    """;
                await using (var command = new SqlCommand(paymentSql, connection, transaction))
                {
                    command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                    command.Parameters.Add("@PaymentMethod", SqlDbType.VarChar, 20).Value = paymentMethod;
                    AddMoney(command, "@Amount", total);
                    command.Parameters.Add("@Reference", SqlDbType.VarChar, 100).Value = Db(reference);
                    if (await command.ExecuteNonQueryAsync(ct) != 2)
                        throw new InvalidOperationException("Không thể ghi nhận thanh toán; hãy tải lại hóa đơn.");
                }

                string salesColumn = paymentMethod == "CASH" ? "CashSales" : "TransferSales";
                string shiftSql = $"""
                    UPDATE dbo.WorkShift
                    SET {salesColumn} = {salesColumn} + @Amount
                    WHERE ShiftId = @ShiftId AND EmployeeId = @EmployeeId AND Status = 'OPEN';
                    """;
                await using (var command = new SqlCommand(shiftSql, connection, transaction))
                {
                    AddMoney(command, "@Amount", total);
                    command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
                    command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                    if (await command.ExecuteNonQueryAsync(ct) != 1)
                        throw new InvalidOperationException("Không thể cập nhật doanh thu ca đang mở.");
                }

                if (prescriptionId is not null)
                    await UpdatePrescriptionStatusAsync(connection, transaction, prescriptionId, ct);

                await transaction.CommitAsync(ct);
                committed = true;
                result = new InvoiceSettlementResult(invoiceId, total, earnedPoints);
            }
        }
        catch
        {
            if (!committed)
                await transaction.RollbackAsync(ct);
            throw;
        }

        if (expired)
            throw new InvalidOperationException("Thời hạn giữ hàng đã hết; hóa đơn đã bị hủy và giữ hàng được giải phóng.");
        return result!;
    }

    public async Task CancelInvoiceDraftAsync(
        string invoiceId,
        string cancelledBy,
        string? reason = null,
        CancellationToken ct = default)
    {
        string id = Required(invoiceId, 20, "Mã hóa đơn");
        string actorId = Required(cancelledBy, 20, "Mã nhân viên");
        string cancelReason = Optional(reason, 500) ?? "Hủy hóa đơn nháp";

        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        try
        {
            const string invoiceLockSql = """
                SELECT Status FROM dbo.Invoice WITH (UPDLOCK, HOLDLOCK)
                WHERE InvoiceId = @InvoiceId;
                """;
            string? status;
            await using (var command = new SqlCommand(invoiceLockSql, connection, transaction))
            {
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = id;
                status = await command.ExecuteScalarAsync(ct) as string;
            }
            if (status is null)
                throw new InvalidOperationException("Hóa đơn nháp không tồn tại.");
            if (status != "PENDING")
                throw new InvalidOperationException("Chỉ hóa đơn đang chờ thanh toán mới có thể bị hủy.");
            await ValidateActiveEmployeeAsync(connection, transaction, actorId, ct);
            await CancelInvoiceAsync(connection, transaction, id, actorId, cancelReason, ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> ExpirePendingDraftsAsync(CancellationToken ct = default)
    {
        await using var connection = DbConnectionFactory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        try
        {
            int count = await ExpirePendingDraftsAsync(connection, transaction, ct);
            await transaction.CommitAsync(ct);
            return count;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<DiscountContext> CalculateDiscountContextAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string? customerId,
        decimal subtotal,
        string? voucherCode,
        int pointsToRedeem,
        string? invoiceIdToExclude,
        CancellationToken ct)
    {
        int availablePoints = 0;
        decimal pointDiscount = 0;
        if (customerId is not null)
        {
            const string customerSql = """
                SELECT LoyaltyPoints
                FROM dbo.Customer WITH (UPDLOCK, HOLDLOCK)
                WHERE CustomerId = @CustomerId AND IsActive = 1;
                """;
            int currentPoints;
            await using (var command = new SqlCommand(customerSql, connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
                object? value = await command.ExecuteScalarAsync(ct);
                if (value is null || value is DBNull)
                    throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã ngừng hoạt động.");
                currentPoints = Convert.ToInt32(value);
            }

            const string reservedPointsSql = """
                SELECT                 COALESCE(SUM(CONVERT(bigint, lt.Points)), 0)
                FROM dbo.LoyaltyTransaction lt WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.Invoice i WITH (UPDLOCK, HOLDLOCK)
                    ON i.InvoiceId = lt.ReferenceCode
                WHERE lt.CustomerId = @CustomerId
                  AND lt.TransactionType = 'REDEEM'
                  AND lt.Note = @ReservationNote
                  AND i.Status = 'PENDING'
                  AND (@ExcludedInvoiceId IS NULL OR i.InvoiceId <> @ExcludedInvoiceId)
                  AND EXISTS
                  (
                      SELECT 1 FROM dbo.InventoryReservation ir WITH (HOLDLOCK)
                      WHERE ir.InvoiceId = i.InvoiceId
                        AND ir.Status = 'ACTIVE'
                        AND ir.ExpiresAt > SYSDATETIME()
                  );
                """;
            long reservedPoints;
            await using (var command = new SqlCommand(reservedPointsSql, connection, transaction))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
                command.Parameters.Add("@ReservationNote", SqlDbType.NVarChar, 300).Value = DraftPointReservationNote;
                command.Parameters.Add("@ExcludedInvoiceId", SqlDbType.VarChar, 20).Value = Db(invoiceIdToExclude);
                reservedPoints = Convert.ToInt64(await command.ExecuteScalarAsync(ct));
            }
            availablePoints = (int)Math.Max(0L, currentPoints - Math.Min(reservedPoints, currentPoints));
        }
        else if (pointsToRedeem > 0)
        {
            throw new InvalidOperationException("Đổi điểm yêu cầu chọn khách hàng.");
        }

        if (pointsToRedeem > availablePoints)
            throw new InvalidOperationException(
                $"Khách hàng chỉ còn {availablePoints} điểm có thể sử dụng (đã trừ điểm đang giữ ở hóa đơn nháp khác).");

        if (pointsToRedeem > 0)
        {
            const string settingsSql = """
                SELECT MoneyPerPoint FROM dbo.LoyaltySettings WITH (UPDLOCK, HOLDLOCK)
                WHERE SettingsId = 1;
                """;
            object? value;
            await using (var command = new SqlCommand(settingsSql, connection, transaction))
                value = await command.ExecuteScalarAsync(ct);
            if (value is null || value is DBNull)
                throw new InvalidOperationException("Chưa cấu hình quy đổi điểm thành tiền.");

            decimal moneyPerPoint = Convert.ToDecimal(value);
            pointDiscount = moneyPerPoint * pointsToRedeem;
            ValidateMoney(pointDiscount, "Giảm giá điểm", allowZero: false);
            if (pointDiscount > subtotal)
                throw new InvalidOperationException(
                    $"Số điểm đổi tạo giảm giá {pointDiscount:0.00}, vượt quá tổng tiền hàng {subtotal:0.00}.");
        }

        VoucherInfo? voucher = null;
        decimal voucherDiscount = 0;
        decimal amountRemaining = subtotal - pointDiscount;
        if (voucherCode is not null)
        {
            const string voucherSql = """
                SELECT VoucherId, VoucherCode, DiscountType, DiscountValue, MinimumOrder,
                       MaximumDiscount, UsageLimit, UsedCount, StartDate, EndDate, IsActive
                FROM dbo.Voucher WITH (UPDLOCK, HOLDLOCK)
                WHERE VoucherCode = @VoucherCode;
                """;
            await using (var command = new SqlCommand(voucherSql, connection, transaction))
            {
                command.Parameters.Add("@VoucherCode", SqlDbType.VarChar, 40).Value = voucherCode;
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Mã voucher không tồn tại.");
                voucher = new VoucherInfo(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDecimal(3),
                    reader.GetDecimal(4),
                    reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    reader.GetInt32(7),
                    reader.GetDateTime(8),
                    reader.GetDateTime(9),
                    reader.GetBoolean(10));
            }

            DateTime now = await GetDatabaseTimeAsync(connection, transaction, ct);
            if (!voucher.IsActive || now < voucher.StartDate || now >= voucher.EndDate)
                throw new InvalidOperationException("Voucher chưa có hiệu lực, đã hết hạn hoặc đang bị khóa.");
            if (subtotal < voucher.MinimumOrder)
                throw new InvalidOperationException(
                    $"Đơn hàng chưa đạt giá trị tối thiểu {voucher.MinimumOrder:0.00} của voucher.");

            int reservedVoucherUses = 0;
            const string reservedVoucherSql = """
                SELECT COUNT(1)
                FROM dbo.VoucherUsage vu WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.Invoice i WITH (UPDLOCK, HOLDLOCK) ON i.InvoiceId = vu.InvoiceId
                WHERE vu.VoucherId = @VoucherId
                  AND vu.CancelledAt IS NULL
                  AND i.Status = 'PENDING'
                  AND (@ExcludedInvoiceId IS NULL OR i.InvoiceId <> @ExcludedInvoiceId)
                  AND EXISTS
                  (
                      SELECT 1 FROM dbo.InventoryReservation ir WITH (HOLDLOCK)
                      WHERE ir.InvoiceId = i.InvoiceId
                        AND ir.Status = 'ACTIVE'
                        AND ir.ExpiresAt > SYSDATETIME()
                  );
                """;
            await using (var command = new SqlCommand(reservedVoucherSql, connection, transaction))
            {
                command.Parameters.Add("@VoucherId", SqlDbType.VarChar, 20).Value = voucher.VoucherId;
                command.Parameters.Add("@ExcludedInvoiceId", SqlDbType.VarChar, 20).Value = Db(invoiceIdToExclude);
                reservedVoucherUses = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            }
            if (voucher.UsageLimit.HasValue
                && (long)voucher.UsedCount + reservedVoucherUses >= voucher.UsageLimit.Value)
                throw new InvalidOperationException("Voucher đã hết lượt sử dụng.");

            decimal rawDiscount = voucher.DiscountType switch
            {
                "AMOUNT" => voucher.DiscountValue,
                "PERCENT" => decimal.Round(
                    subtotal * voucher.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero),
                _ => throw new InvalidOperationException("Loại giảm giá của voucher không hợp lệ.")
            };
            if (voucher.MaximumDiscount.HasValue)
                rawDiscount = Math.Min(rawDiscount, voucher.MaximumDiscount.Value);
            voucherDiscount = Math.Min(rawDiscount, amountRemaining);
            ValidateMoney(voucherDiscount, "Giảm giá voucher", allowZero: true);
            if (voucherDiscount <= 0)
                throw new InvalidOperationException("Voucher không thể áp dụng cho số tiền còn phải trả.");
        }

        return new DiscountContext(
            pointDiscount, voucherDiscount, availablePoints, pointsToRedeem, voucher);
    }

    private static async Task<PreparedLine> PrepareLineAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SalesLineInput input,
        CancellationToken ct)
    {
        const string sql = """
            SELECT u.UnitName, u.ConversionFactor, u.SalePrice, m.BusinessStatus,
                   m.RequiresPrescription
            FROM dbo.MedicineUnit u WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Medicine m WITH (UPDLOCK, HOLDLOCK) ON m.MedicineId = u.MedicineId
            WHERE u.MedicineUnitId = @UnitId AND u.MedicineId = @MedicineId
              AND u.IsActive = 1;
            """;
        string unitName;
        decimal factor;
        decimal price;
        bool requiresPrescription;
        await using (var command = new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("@UnitId", SqlDbType.Int).Value = input.MedicineUnitId;
            command.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = input.MedicineId;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException($"Đơn vị bán của thuốc {input.MedicineId} không còn hợp lệ.");
            unitName = reader.GetString(0);
            factor = reader.GetDecimal(1);
            price = reader.GetDecimal(2);
            if (reader.GetString(3) != "ACTIVE")
                throw new InvalidOperationException($"Thuốc {input.MedicineId} đã ngừng kinh doanh.");
            requiresPrescription = reader.GetBoolean(4);
        }

        decimal quantityBase = input.Quantity * factor;
        if (decimal.Round(quantityBase, 3, MidpointRounding.AwayFromZero) != quantityBase)
            throw new InvalidOperationException(
                $"Số lượng theo đơn vị cơ bản của thuốc {input.MedicineId} vượt quá 3 chữ số thập phân.");
        ValidateQuantity(quantityBase, "Số lượng theo đơn vị cơ bản");
        decimal gross = decimal.Round(input.Quantity * price, 2, MidpointRounding.AwayFromZero);
        ValidateMoney(gross, "Thành tiền dòng thuốc", allowZero: true);
        return new PreparedLine(
            input.MedicineId, input.MedicineUnitId, unitName, factor,
            price, input.Quantity, quantityBase, gross, requiresPrescription);
    }

    private static async Task ValidateOpenShiftAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string shiftId,
        string employeeId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.WorkShift ws WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Employee e WITH (UPDLOCK, HOLDLOCK) ON e.EmployeeId = ws.EmployeeId
            WHERE ws.ShiftId = @ShiftId AND ws.EmployeeId = @EmployeeId
              AND ws.Status = 'OPEN' AND e.IsActive = 1;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@ShiftId", SqlDbType.VarChar, 20).Value = shiftId;
        command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
            throw new InvalidOperationException("Ca làm việc không tồn tại, đã đóng hoặc không thuộc nhân viên này.");
    }

    private static async Task ValidateCustomerAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string? customerId,
        CancellationToken ct)
    {
        if (customerId is null)
            return;
        const string sql = """
            SELECT COUNT(1) FROM dbo.Customer WITH (UPDLOCK, HOLDLOCK)
            WHERE CustomerId = @CustomerId AND IsActive = 1;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
            throw new InvalidOperationException("Khách hàng không tồn tại hoặc đã ngừng hoạt động.");
    }

    private static async Task ValidatePrescriptionQuantitiesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string prescriptionId,
        IReadOnlyCollection<PreparedLine> lines,
        string? invoiceIdToExclude,
        CancellationToken ct)
    {
        await LockValidPrescriptionAsync(connection, transaction, prescriptionId, ct);
        foreach (PreparedLine line in lines)
        {
            const string prescribedSql = """
                SELECT COALESCE(SUM(pd.PrescribedQuantity), 0)
                FROM dbo.PrescriptionDetail pd WITH (UPDLOCK, HOLDLOCK)
                WHERE pd.PrescriptionId = @PrescriptionId
                  AND pd.MedicineId = @MedicineId
                  AND pd.PrescribedUnit = @UnitName;
                """;
            decimal prescribed;
            await using (var command = new SqlCommand(prescribedSql, connection, transaction))
            {
                command.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
                command.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
                command.Parameters.Add("@UnitName", SqlDbType.NVarChar, 30).Value = line.UnitName;
                prescribed = Convert.ToDecimal(await command.ExecuteScalarAsync(ct));
            }
            if (prescribed == 0)
            {
                if (line.RequiresPrescription)
                    throw new InvalidOperationException(
                        $"Thuốc kê đơn {line.MedicineId} không có trong đơn thuốc đã chọn.");
                continue;
            }

            const string quantitiesSql = """
                SELECT
                    COALESCE(SUM(CASE
                        WHEN i.Status IN ('PAID','RETURNED_PARTIAL','RETURNED_FULL')
                        THEN d.Quantity ELSE 0 END), 0),
                    COALESCE(SUM(CASE
                        WHEN i.Status = 'PENDING'
                         AND (@ExcludedInvoiceId IS NULL OR i.InvoiceId <> @ExcludedInvoiceId)
                         AND EXISTS
                         (
                             SELECT 1 FROM dbo.InventoryReservation ir WITH (HOLDLOCK)
                             WHERE ir.InvoiceId = i.InvoiceId
                               AND ir.Status = 'ACTIVE'
                               AND ir.ExpiresAt > SYSDATETIME()
                         )
                        THEN d.Quantity ELSE 0 END), 0)
                FROM dbo.Invoice i WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.InvoiceDetail d WITH (UPDLOCK, HOLDLOCK) ON d.InvoiceId = i.InvoiceId
                WHERE i.PrescriptionId = @PrescriptionId
                  AND d.MedicineId = @MedicineId
                  AND d.UnitNameSnapshot = @UnitName;
                """;
            decimal sold;
            decimal pending;
            await using (var command = new SqlCommand(quantitiesSql, connection, transaction))
            {
                command.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
                command.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
                command.Parameters.Add("@UnitName", SqlDbType.NVarChar, 30).Value = line.UnitName;
                command.Parameters.Add("@ExcludedInvoiceId", SqlDbType.VarChar, 20).Value = Db(invoiceIdToExclude);
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Không thể kiểm tra số lượng thuốc đã cấp theo đơn.");
                sold = reader.GetDecimal(0);
                pending = reader.GetDecimal(1);
            }

            if (sold + pending + line.Quantity > prescribed)
                throw new InvalidOperationException(
                    $"Số lượng {line.UnitName} của thuốc {line.MedicineId} vượt số lượng còn lại trong đơn thuốc ({Math.Max(0, prescribed - sold - pending):0.###}).");
        }
    }

    private static async Task LockValidPrescriptionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string prescriptionId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT p.Status, p.PrescribedDate,
                   CASE WHEN EXISTS
                   (
                       SELECT 1 FROM dbo.PrescriptionDetail pd WITH (HOLDLOCK)
                       WHERE pd.PrescriptionId = p.PrescriptionId
                   ) THEN 1 ELSE 0 END
            FROM dbo.Prescription p WITH (UPDLOCK, HOLDLOCK)
            WHERE p.PrescriptionId = @PrescriptionId;
            """;
        string? status;
        DateTime prescribedDate;
        bool hasDetails;
        await using (var command = new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("Đơn thuốc không tồn tại.");
            status = reader.GetString(0);
            prescribedDate = reader.GetDateTime(1);
            hasDetails = reader.GetInt32(2) == 1;
        }
        if (status is not ("UNSOLD" or "PARTIAL"))
            throw new InvalidOperationException("Đơn thuốc không tồn tại, đã bán hết hoặc không còn hợp lệ.");
        if (prescribedDate.Date > DateTime.Today || !hasDetails)
            throw new InvalidOperationException("Đơn thuốc chưa có hiệu lực hoặc không có thuốc kê.");
    }

    private static async Task ValidateDraftPrescriptionAtSettlementAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        string prescriptionId,
        CancellationToken ct)
    {
        await LockValidPrescriptionAsync(connection, transaction, prescriptionId, ct);
        const string linesSql = """
            SELECT d.MedicineId, d.UnitNameSnapshot, SUM(d.Quantity), m.RequiresPrescription
            FROM dbo.InvoiceDetail d WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Medicine m WITH (HOLDLOCK) ON m.MedicineId = d.MedicineId
            WHERE d.InvoiceId = @InvoiceId
            GROUP BY d.MedicineId, d.UnitNameSnapshot, m.RequiresPrescription;
            """;
        var lines = new List<(string MedicineId, string UnitName, decimal Quantity, bool RequiresPrescription)>();
        await using (var command = new SqlCommand(linesSql, connection, transaction))
        {
            command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                lines.Add((reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetBoolean(3)));
        }

        foreach (var line in lines)
        {
            const string prescribedSql = """
                SELECT COALESCE(SUM(PrescribedQuantity), 0)
                FROM dbo.PrescriptionDetail WITH (UPDLOCK, HOLDLOCK)
                WHERE PrescriptionId = @PrescriptionId
                  AND MedicineId = @MedicineId AND PrescribedUnit = @UnitName;
                """;
            decimal prescribed;
            await using (var command = new SqlCommand(prescribedSql, connection, transaction))
            {
                command.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
                command.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
                command.Parameters.Add("@UnitName", SqlDbType.NVarChar, 30).Value = line.UnitName;
                prescribed = Convert.ToDecimal(await command.ExecuteScalarAsync(ct));
            }
            if (prescribed == 0)
            {
                if (line.RequiresPrescription)
                    throw new InvalidOperationException(
                        $"Thuốc kê đơn {line.MedicineId} không có trong đơn thuốc đã chọn.");
                continue;
            }

            const string soldSql = """
                SELECT COALESCE(SUM(d.Quantity), 0)
                FROM dbo.Invoice i WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.InvoiceDetail d WITH (UPDLOCK, HOLDLOCK) ON d.InvoiceId = i.InvoiceId
                WHERE i.PrescriptionId = @PrescriptionId
                  AND i.InvoiceId <> @CurrentInvoiceId
                  AND i.Status IN ('PAID','RETURNED_PARTIAL','RETURNED_FULL')
                  AND d.MedicineId = @MedicineId
                  AND d.UnitNameSnapshot = @UnitName;
                """;
            decimal sold;
            await using (var command = new SqlCommand(soldSql, connection, transaction))
            {
                command.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
                command.Parameters.Add("@CurrentInvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                command.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
                command.Parameters.Add("@UnitName", SqlDbType.NVarChar, 30).Value = line.UnitName;
                sold = Convert.ToDecimal(await command.ExecuteScalarAsync(ct));
            }
            if (sold + line.Quantity > prescribed)
                throw new InvalidOperationException(
                    $"Số lượng {line.UnitName} của thuốc {line.MedicineId} đã vượt số lượng còn lại trong đơn thuốc.");
        }
    }

    private static async Task<List<(string BatchId, decimal Quantity, decimal QuantityBase)>>
        ReserveLineInFefoAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string invoiceId,
            PreparedLine line,
            CancellationToken ct)
    {
        const string batchSql = """
            SELECT b.BatchId, b.PhysicalStock,
                   COALESCE((
                       SELECT SUM(ir.ReservedQuantity)
                       FROM dbo.InventoryReservation ir WITH (UPDLOCK, HOLDLOCK)
                       WHERE ir.BatchId = b.BatchId
                         AND ir.Status = 'ACTIVE'
                         AND ir.ExpiresAt > SYSDATETIME()
                   ), 0) AS ReservedQuantity
            FROM dbo.MedicineBatch b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            WHERE b.MedicineId = @MedicineId
              AND b.BatchStatus = 'ACTIVE'
              AND b.ExpiryDate >= CONVERT(date, SYSDATETIME())
              AND b.PhysicalStock > 0
            ORDER BY b.ExpiryDate ASC, b.CreatedAt ASC, b.BatchId ASC;
            """;
        var batches = new List<(string Id, decimal Available)>();
        await using (var command = new SqlCommand(batchSql, connection, transaction))
        {
            command.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                decimal available = reader.GetDecimal(1) - reader.GetDecimal(2);
                if (available > 0)
                    batches.Add((reader.GetString(0), available));
            }
        }

        decimal remainingQuantity = line.Quantity;
        var allocations = new List<(string BatchId, decimal Quantity, decimal QuantityBase)>();
        foreach (var batch in batches)
        {
            if (remainingQuantity <= 0)
                break;

            decimal candidate = decimal.Truncate(Math.Min(remainingQuantity, batch.Available / line.ConversionFactor) * 1000m) / 1000m;
            int adjustments = 0;
            while (candidate > 0 && adjustments <= 1000)
            {
                decimal rawBase = candidate * line.ConversionFactor;
                decimal exactBase = decimal.Round(rawBase, 3, MidpointRounding.AwayFromZero);
                if (rawBase == exactBase && exactBase <= batch.Available)
                    break;
                candidate -= 0.001m;
                adjustments++;
            }
            if (candidate <= 0 || adjustments > 1000)
                continue;

            decimal quantityBase = candidate * line.ConversionFactor;
            allocations.Add((batch.Id, candidate, quantityBase));
            remainingQuantity -= candidate;
        }

        if (remainingQuantity > 0)
        {
            decimal totalAvailable = batches.Sum(batch => batch.Available);
            throw new InvalidOperationException(
                $"Thuốc {line.MedicineId} không đủ tồn kho khả dụng theo lô FEFO. " +
                $"Cần {line.QuantityBase:0.###}, còn {totalAvailable:0.###} đơn vị cơ bản.");
        }

        return allocations;
    }

    private static async Task<long> InsertInvoiceDetailAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        PreparedLine line,
        (string BatchId, decimal Quantity, decimal QuantityBase) allocation,
        decimal lineTotal,
        CancellationToken ct)
    {
        const string sql = """
            INSERT dbo.InvoiceDetail
                (InvoiceId, MedicineId, MedicineUnitId, BatchId, UnitNameSnapshot,
                 ConversionFactorSnapshot, Quantity, QuantityBase, UnitPrice,
                 DiscountAllocated, LineTotal)
            VALUES
                (@InvoiceId, @MedicineId, @UnitId, @BatchId, @UnitName,
                 @Factor, @Quantity, @QuantityBase, @UnitPrice, 0, @LineTotal);
            SELECT CONVERT(bigint, SCOPE_IDENTITY());
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
        command.Parameters.Add("@MedicineId", SqlDbType.VarChar, 20).Value = line.MedicineId;
        command.Parameters.Add("@UnitId", SqlDbType.Int).Value = line.MedicineUnitId;
        command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = allocation.BatchId;
        command.Parameters.Add("@UnitName", SqlDbType.NVarChar, 30).Value = line.UnitName;
        AddQuantity(command, "@Factor", line.ConversionFactor);
        AddQuantity(command, "@Quantity", allocation.Quantity);
        AddQuantity(command, "@QuantityBase", allocation.QuantityBase);
        AddMoney(command, "@UnitPrice", line.UnitPrice);
        AddMoney(command, "@LineTotal", lineTotal);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private static async Task InsertReservationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        long detailId,
        string batchId,
        decimal quantityBase,
        CancellationToken ct)
    {
        const string sql = """
            INSERT dbo.InventoryReservation
                (InvoiceId, InvoiceDetailId, BatchId, ReservedQuantity, ExpiresAt, Status)
            VALUES
                (@InvoiceId, @DetailId, @BatchId, @Quantity,
                 DATEADD(MINUTE, @ReservationMinutes, SYSDATETIME()), 'ACTIVE');
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
        command.Parameters.Add("@DetailId", SqlDbType.BigInt).Value = detailId;
        command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = batchId;
        AddQuantity(command, "@Quantity", quantityBase);
        command.Parameters.Add("@ReservationMinutes", SqlDbType.Int).Value = ReservationMinutes;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task AllocateInvoiceDiscountsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyList<DraftDetail> details,
        decimal subtotal,
        decimal totalDiscount,
        CancellationToken ct)
    {
        if (details.Count == 0 || totalDiscount == 0)
            return;
        if (totalDiscount < 0 || totalDiscount > subtotal)
            throw new InvalidOperationException("Tổng giảm giá vượt quá giá trị hàng hóa.");

        long[] grossCents = details.Select(detail => ToCents(detail.LineTotal)).ToArray();
        long[] discounts = AllocateCents(
            ToCents(totalDiscount), grossCents, grossCents);
        for (int index = 0; index < details.Count; index++)
        {
            const string sql = """
                UPDATE dbo.InvoiceDetail SET DiscountAllocated = @Discount
                WHERE InvoiceDetailId = @DetailId;
                """;
            await using var command = new SqlCommand(sql, connection, transaction);
            AddMoney(command, "@Discount", FromCents(discounts[index]));
            command.Parameters.Add("@DetailId", SqlDbType.BigInt).Value = details[index].DetailId;
            if (await command.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Không thể phân bổ giảm giá cho chi tiết hóa đơn.");
        }
    }

    private static async Task ConsumeReservedStockAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        string employeeId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT d.InvoiceDetailId, d.BatchId, d.QuantityBase,
                   r.ReservationId, r.BatchId, r.ReservedQuantity, r.Status, r.ExpiresAt,
                   b.PhysicalStock, b.BatchStatus, b.ExpiryDate
            FROM dbo.InvoiceDetail d WITH (UPDLOCK, HOLDLOCK)
            LEFT JOIN dbo.InventoryReservation r WITH (UPDLOCK, HOLDLOCK)
                ON r.InvoiceDetailId = d.InvoiceDetailId AND r.InvoiceId = d.InvoiceId
            INNER JOIN dbo.MedicineBatch b WITH (UPDLOCK, HOLDLOCK)
                ON b.BatchId = d.BatchId
            WHERE d.InvoiceId = @InvoiceId
            ORDER BY b.ExpiryDate, b.BatchId, d.InvoiceDetailId;
            """;
        var details = new List<(
            long DetailId, string BatchId, decimal QuantityBase,
            long? ReservationId, string? ReservationBatchId, decimal? ReservedQuantity,
            string? ReservationStatus, DateTime? ExpiresAt,
            decimal PhysicalStock, string BatchStatus, DateTime ExpiryDate)>();
        await using (var command = new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                details.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetDecimal(2),
                    reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    reader.GetDecimal(8),
                    reader.GetString(9),
                    reader.GetDateTime(10)));
            }
        }
        if (details.Count == 0)
            throw new InvalidOperationException("Hóa đơn nháp không có chi tiết thuốc.");
        if (details.Any(row => row.ReservationId is null))
            throw new InvalidOperationException("Hóa đơn nháp thiếu dòng giữ hàng; không thể tất toán an toàn.");
        if (details.GroupBy(row => row.DetailId).Any(group => group.Count() != 1))
            throw new InvalidOperationException("Dữ liệu giữ hàng của hóa đơn bị trùng; cần kiểm tra lại.");

        DateTime now = await GetDatabaseTimeAsync(connection, transaction, ct);
        foreach (var detail in details)
        {
            if (detail.ReservationStatus != "ACTIVE"
                || detail.ExpiresAt is null
                || detail.ExpiresAt.Value <= now
                || detail.ReservationBatchId != detail.BatchId
                || detail.ReservedQuantity != detail.QuantityBase)
                throw new InvalidOperationException(
                    "Dữ liệu giữ hàng đã hết hạn hoặc không khớp chi tiết hóa đơn.");
            if (detail.BatchStatus != "ACTIVE" || detail.ExpiryDate.Date < now.Date)
                throw new InvalidOperationException(
                    $"Lô {detail.BatchId} đã hết hạn hoặc ngừng hoạt động; không thể tất toán.");

            const string stockSql = """
                UPDATE dbo.MedicineBatch
                SET PhysicalStock = PhysicalStock - @Quantity
                WHERE BatchId = @BatchId AND BatchStatus = 'ACTIVE'
                  AND ExpiryDate >= CONVERT(date, SYSDATETIME())
                  AND PhysicalStock >= @Quantity;
                """;
            await using (var command = new SqlCommand(stockSql, connection, transaction))
            {
                AddQuantity(command, "@Quantity", detail.QuantityBase);
                command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = detail.BatchId;
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException(
                        $"Tồn kho lô {detail.BatchId} đã thay đổi; không thể tất toán.");
            }

            const string stockTransactionSql = """
                INSERT dbo.StockTransaction
                    (BatchId, TransactionType, QuantityChange, ReferenceType,
                     ReferenceCode, CreatedBy, Note)
                VALUES
                    (@BatchId, 'SALE', @QuantityChange, 'INVOICE',
                     @InvoiceId, @EmployeeId, N'Xuất kho bán thuốc');
                """;
            await using (var command = new SqlCommand(stockTransactionSql, connection, transaction))
            {
                command.Parameters.Add("@BatchId", SqlDbType.VarChar, 25).Value = detail.BatchId;
                AddQuantity(command, "@QuantityChange", -detail.QuantityBase);
                command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
                command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
                await command.ExecuteNonQueryAsync(ct);
            }

            const string consumeSql = """
                UPDATE dbo.InventoryReservation
                SET Status = 'CONSUMED', ReleasedAt = SYSDATETIME()
                WHERE ReservationId = @ReservationId AND Status = 'ACTIVE'
                  AND ExpiresAt > SYSDATETIME();
                """;
            await using (var command = new SqlCommand(consumeSql, connection, transaction))
            {
                command.Parameters.Add("@ReservationId", SqlDbType.BigInt).Value = detail.ReservationId!.Value;
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Không thể tiêu thụ dòng giữ hàng; hãy tải lại hóa đơn.");
            }
        }

        const string allReservationsSql = """
            SELECT COUNT(1) FROM dbo.InventoryReservation WITH (UPDLOCK, HOLDLOCK)
            WHERE InvoiceId = @InvoiceId;
            """;
        await using (var command = new SqlCommand(allReservationsSql, connection, transaction))
        {
            command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != details.Count)
                throw new InvalidOperationException("Số dòng giữ hàng không khớp chi tiết hóa đơn.");
        }
    }

    private static async Task ValidateVoucherReservationAtSettlementAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        decimal voucherDiscount,
        CancellationToken ct)
    {
        const string sql = """
            SELECT vu.DiscountAmount, vu.CancelledAt,
                   v.IsActive, v.StartDate, v.EndDate, v.UsageLimit, v.UsedCount,
                   v.DiscountType, v.DiscountValue, v.MinimumOrder, v.MaximumDiscount,
                   i.Subtotal, i.PointDiscount
            FROM dbo.VoucherUsage vu WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Voucher v WITH (UPDLOCK, HOLDLOCK) ON v.VoucherId = vu.VoucherId
            INNER JOIN dbo.Invoice i WITH (UPDLOCK, HOLDLOCK) ON i.InvoiceId = vu.InvoiceId
            WHERE vu.InvoiceId = @InvoiceId;
            """;
        bool found = false;
        decimal reservedDiscount = 0;
        bool cancelled = false;
        bool active = false;
        DateTime start = default;
        DateTime end = default;
        int? usageLimit = null;
        int usedCount = 0;
        string? discountType = null;
        decimal discountValue = 0;
        decimal minimumOrder = 0;
        decimal? maximumDiscount = null;
        decimal subtotal = 0;
        decimal pointDiscount = 0;
        await using (var command = new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                found = true;
                reservedDiscount = reader.GetDecimal(0);
                cancelled = !reader.IsDBNull(1);
                active = reader.GetBoolean(2);
                start = reader.GetDateTime(3);
                end = reader.GetDateTime(4);
                usageLimit = reader.IsDBNull(5) ? null : reader.GetInt32(5);
                usedCount = reader.GetInt32(6);
                discountType = reader.GetString(7);
                discountValue = reader.GetDecimal(8);
                minimumOrder = reader.GetDecimal(9);
                maximumDiscount = reader.IsDBNull(10) ? null : reader.GetDecimal(10);
                subtotal = reader.GetDecimal(11);
                pointDiscount = reader.GetDecimal(12);
                if (await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Hóa đơn có nhiều hơn một lượt sử dụng voucher.");
            }
        }

        if (voucherDiscount == 0)
        {
            if (found)
                throw new InvalidOperationException("Hóa đơn không nhất quán: có lượt voucher nhưng không có giảm giá.");
            return;
        }
        if (!found || cancelled || reservedDiscount != voucherDiscount)
            throw new InvalidOperationException("Lượt giữ voucher không còn hợp lệ cho hóa đơn này.");
        DateTime now = await GetDatabaseTimeAsync(connection, transaction, ct);
        if (!active || now < start || now >= end)
            throw new InvalidOperationException("Voucher đã hết hiệu lực trước khi thanh toán.");
        if (usageLimit.HasValue && usedCount >= usageLimit.Value)
            throw new InvalidOperationException("Voucher đã hết lượt sử dụng trước khi thanh toán.");
        if (subtotal < minimumOrder)
            throw new InvalidOperationException("Hóa đơn không còn đạt giá trị tối thiểu của voucher.");
        decimal remaining = subtotal - pointDiscount;
        decimal recalculatedDiscount = discountType switch
        {
            "AMOUNT" => discountValue,
            "PERCENT" => decimal.Round(subtotal * discountValue / 100m, 2, MidpointRounding.AwayFromZero),
            _ => throw new InvalidOperationException("Loại giảm giá của voucher không hợp lệ.")
        };
        if (maximumDiscount.HasValue)
            recalculatedDiscount = Math.Min(recalculatedDiscount, maximumDiscount.Value);
        recalculatedDiscount = Math.Min(recalculatedDiscount, remaining);
        if (recalculatedDiscount != voucherDiscount)
            throw new InvalidOperationException(
                "Điều kiện hoặc giá trị voucher đã thay đổi sau khi tạo hóa đơn; hãy tạo hóa đơn nháp mới.");
    }

    private static async Task SettleVoucherUsageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        decimal voucherDiscount,
        CancellationToken ct)
    {
        if (voucherDiscount == 0)
            return;

        const string updateVoucherSql = """
            UPDATE dbo.Voucher
            SET UsedCount = UsedCount + 1
            WHERE VoucherId = (SELECT VoucherId FROM dbo.VoucherUsage WITH (UPDLOCK, HOLDLOCK)
                               WHERE InvoiceId = @InvoiceId AND CancelledAt IS NULL)
              AND (UsageLimit IS NULL OR UsedCount < UsageLimit);
            """;
        await using (var command = new SqlCommand(updateVoucherSql, connection, transaction))
        {
            command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
            if (await command.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Không thể xác nhận lượt sử dụng voucher.");
        }

        const string usageSql = """
            UPDATE dbo.VoucherUsage
            SET UsedAt = SYSDATETIME()
            WHERE InvoiceId = @InvoiceId AND CancelledAt IS NULL
              AND DiscountAmount = @DiscountAmount;
            """;
        await using (var command = new SqlCommand(usageSql, connection, transaction))
        {
            command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
            AddMoney(command, "@DiscountAmount", voucherDiscount);
            if (await command.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Không thể hoàn tất ghi nhận voucher.");
        }
    }

    private static async Task<int> SettleRedeemedPointsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        string? customerId,
        decimal pointDiscount,
        CancellationToken ct)
    {
        const string sql = """
            SELECT LoyaltyTransactionId, CustomerId, Points, MoneyValue
            FROM dbo.LoyaltyTransaction WITH (UPDLOCK, HOLDLOCK)
            WHERE ReferenceCode = @InvoiceId
              AND TransactionType = 'REDEEM'
              AND Note = @ReservationNote;
            """;
        long? transactionId = null;
        string? reservedCustomerId = null;
        int points = 0;
        decimal? moneyValue = null;
        await using (var command = new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
            command.Parameters.Add("@ReservationNote", SqlDbType.NVarChar, 300).Value = DraftPointReservationNote;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                transactionId = reader.GetInt64(0);
                reservedCustomerId = reader.GetString(1);
                points = reader.GetInt32(2);
                moneyValue = reader.IsDBNull(3) ? null : reader.GetDecimal(3);
                if (await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Hóa đơn có nhiều hơn một yêu cầu đổi điểm.");
            }
        }

        if (pointDiscount == 0)
        {
            if (transactionId.HasValue)
                throw new InvalidOperationException("Hóa đơn không nhất quán: có điểm giữ nhưng không có giảm giá.");
            return 0;
        }
        if (!transactionId.HasValue
            || customerId is null
            || reservedCustomerId != customerId
            || moneyValue != pointDiscount
            || points <= 0)
            throw new InvalidOperationException("Thông tin điểm giữ của hóa đơn không còn hợp lệ.");

        const string customerSql = """
            UPDATE dbo.Customer
            SET LoyaltyPoints = LoyaltyPoints - @Points
            WHERE CustomerId = @CustomerId AND IsActive = 1
              AND LoyaltyPoints >= @Points;
            """;
        await using (var command = new SqlCommand(customerSql, connection, transaction))
        {
            command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
            command.Parameters.Add("@Points", SqlDbType.Int).Value = points;
            if (await command.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Khách hàng không còn đủ điểm để tất toán hóa đơn.");
        }

        const string finalizeSql = """
            UPDATE dbo.LoyaltyTransaction
            SET Note = N'Đổi điểm thanh toán hóa đơn'
            WHERE LoyaltyTransactionId = @TransactionId
              AND TransactionType = 'REDEEM' AND Note = @ReservationNote;
            """;
        await using (var command = new SqlCommand(finalizeSql, connection, transaction))
        {
            command.Parameters.Add("@TransactionId", SqlDbType.BigInt).Value = transactionId.Value;
            command.Parameters.Add("@ReservationNote", SqlDbType.NVarChar, 300).Value = DraftPointReservationNote;
            if (await command.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Không thể hoàn tất ghi nhận điểm đã đổi.");
        }
        return points;
    }

    private static async Task<int> EarnLoyaltyPointsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        string? customerId,
        decimal amountPaid,
        CancellationToken ct)
    {
        if (customerId is null)
            return 0;
        const string settingsSql = """
            SELECT EarnPercent, MoneyPerPoint
            FROM dbo.LoyaltySettings WITH (UPDLOCK, HOLDLOCK)
            WHERE SettingsId = 1;
            """;
        decimal earnPercent;
        decimal moneyPerPoint;
        await using (var command = new SqlCommand(settingsSql, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("Chưa cấu hình tích điểm cho khách hàng.");
            earnPercent = reader.GetDecimal(0);
            moneyPerPoint = reader.GetDecimal(1);
        }
        if (moneyPerPoint <= 0)
            throw new InvalidOperationException("Cấu hình quy đổi điểm không hợp lệ.");

        decimal rewardValue = amountPaid * earnPercent / 100m;
        decimal pointsDecimal = decimal.Floor(rewardValue / moneyPerPoint);
        if (pointsDecimal <= 0)
            return 0;
        if (pointsDecimal > int.MaxValue)
            throw new InvalidOperationException("Số điểm tích lũy vượt giới hạn dữ liệu.");
        int earnedPoints = decimal.ToInt32(pointsDecimal);

        const string customerSql = """
            UPDATE dbo.Customer
            SET LoyaltyPoints = LoyaltyPoints + @Points
            WHERE CustomerId = @CustomerId AND IsActive = 1
              AND LoyaltyPoints <= 2147483647 - @Points;
            """;
        await using (var command = new SqlCommand(customerSql, connection, transaction))
        {
            command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
            command.Parameters.Add("@Points", SqlDbType.Int).Value = earnedPoints;
            if (await command.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Không thể cộng điểm tích lũy cho khách hàng.");
        }

        const string transactionSql = """
            INSERT dbo.LoyaltyTransaction
                (CustomerId, TransactionType, Points, MoneyValue, ReferenceCode, Note)
            VALUES
                (@CustomerId, 'EARN', @Points, @MoneyValue, @InvoiceId, N'Tích điểm hóa đơn');
            """;
        await using (var command = new SqlCommand(transactionSql, connection, transaction))
        {
            command.Parameters.Add("@CustomerId", SqlDbType.VarChar, 20).Value = customerId;
            command.Parameters.Add("@Points", SqlDbType.Int).Value = earnedPoints;
            AddMoney(command, "@MoneyValue", amountPaid);
            command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
            await command.ExecuteNonQueryAsync(ct);
        }
        return earnedPoints;
    }

    private static async Task UpdatePrescriptionStatusAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string prescriptionId,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE p
            SET Status = CASE WHEN NOT EXISTS
            (
                SELECT 1
                FROM dbo.PrescriptionDetail pd
                OUTER APPLY
                (
                    SELECT SUM(d.Quantity) AS SoldQuantity
                    FROM dbo.Invoice i
                    INNER JOIN dbo.InvoiceDetail d ON d.InvoiceId = i.InvoiceId
                    WHERE i.PrescriptionId = p.PrescriptionId
                      AND i.Status IN ('PAID','RETURNED_PARTIAL','RETURNED_FULL')
                      AND d.MedicineId = pd.MedicineId
                      AND d.UnitNameSnapshot = pd.PrescribedUnit
                ) sold
                WHERE pd.PrescriptionId = p.PrescriptionId
                  AND COALESCE(sold.SoldQuantity, 0) < pd.PrescribedQuantity
            )
            THEN 'SOLD' ELSE 'PARTIAL' END
            FROM dbo.Prescription p
            WHERE p.PrescriptionId = @PrescriptionId;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@PrescriptionId", SqlDbType.VarChar, 20).Value = prescriptionId;
        if (await command.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("Không thể cập nhật trạng thái đơn thuốc.");
    }

    private static async Task<DateTime> GetInvoiceExpiryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT MIN(ExpiresAt) FROM dbo.InventoryReservation WITH (UPDLOCK, HOLDLOCK)
            WHERE InvoiceId = @InvoiceId;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
        object? value = await command.ExecuteScalarAsync(ct);
        if (value is not DateTime expiry)
            throw new InvalidOperationException("Hóa đơn nháp không có thời hạn giữ hàng.");
        return expiry;
    }

    private static async Task<DateTime> GetDatabaseTimeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken ct)
    {
        await using var command = new SqlCommand("SELECT SYSDATETIME();", connection, transaction);
        object? value = await command.ExecuteScalarAsync(ct);
        if (value is not DateTime now)
            throw new InvalidOperationException("Không thể xác định thời gian máy chủ cơ sở dữ liệu.");
        return now;
    }

    private static async Task<int> ExpirePendingDraftsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken ct)
    {
        const string sql = """
            SELECT DISTINCT i.InvoiceId
            FROM dbo.Invoice i WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.InventoryReservation ir WITH (UPDLOCK, HOLDLOCK)
                ON ir.InvoiceId = i.InvoiceId
            WHERE i.Status = 'PENDING'
              AND ir.Status = 'ACTIVE'
              AND ir.ExpiresAt <= SYSDATETIME();
            """;
        var invoiceIds = new List<string>();
        await using (var command = new SqlCommand(sql, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                invoiceIds.Add(reader.GetString(0));
        }
        foreach (string invoiceId in invoiceIds)
            await ExpireInvoiceAsync(connection, transaction, invoiceId, cancelledBy: null, ct);
        return invoiceIds.Count;
    }

    private static async Task ExpireInvoiceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        string? cancelledBy,
        CancellationToken ct)
    {
        const string reservationSql = """
            UPDATE dbo.InventoryReservation
            SET Status = CASE WHEN ExpiresAt <= SYSDATETIME() THEN 'EXPIRED' ELSE 'RELEASED' END,
                ReleasedAt = SYSDATETIME()
            WHERE InvoiceId = @InvoiceId AND Status = 'ACTIVE';

            UPDATE dbo.VoucherUsage
            SET CancelledAt = SYSDATETIME(), CancelledBy = @CancelledBy
            WHERE InvoiceId = @InvoiceId AND CancelledAt IS NULL;

            DELETE dbo.LoyaltyTransaction
            WHERE ReferenceCode = @InvoiceId
              AND TransactionType = 'REDEEM'
              AND Note = @ReservationNote;

            UPDATE dbo.Invoice
            SET Status = 'CANCELLED',
                CancelledBy = @CancelledBy,
                CancelledAt = SYSDATETIME(),
                CancelReason = N'Hết thời hạn giữ hàng (30 phút)'
            WHERE InvoiceId = @InvoiceId AND Status = 'PENDING';
            """;
        await using var command = new SqlCommand(reservationSql, connection, transaction);
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
        command.Parameters.Add("@CancelledBy", SqlDbType.VarChar, 20).Value = Db(cancelledBy);
        command.Parameters.Add("@ReservationNote", SqlDbType.NVarChar, 300).Value = DraftPointReservationNote;
        int affected = await command.ExecuteNonQueryAsync(ct);
        if (affected < 1)
            throw new InvalidOperationException("Không thể giải phóng hóa đơn nháp đã hết hạn.");
    }

    private static async Task CancelInvoiceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string invoiceId,
        string cancelledBy,
        string reason,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.InventoryReservation
            SET Status = CASE WHEN ExpiresAt <= SYSDATETIME() THEN 'EXPIRED' ELSE 'RELEASED' END,
                ReleasedAt = SYSDATETIME()
            WHERE InvoiceId = @InvoiceId AND Status = 'ACTIVE';

            UPDATE dbo.VoucherUsage
            SET CancelledAt = SYSDATETIME(), CancelledBy = @CancelledBy
            WHERE InvoiceId = @InvoiceId AND CancelledAt IS NULL;

            DELETE dbo.LoyaltyTransaction
            WHERE ReferenceCode = @InvoiceId
              AND TransactionType = 'REDEEM'
              AND Note = @ReservationNote;

            UPDATE dbo.Invoice
            SET Status = 'CANCELLED',
                CancelledBy = @CancelledBy,
                CancelledAt = SYSDATETIME(),
                CancelReason = @Reason
            WHERE InvoiceId = @InvoiceId AND Status = 'PENDING';
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@InvoiceId", SqlDbType.VarChar, 20).Value = invoiceId;
        command.Parameters.Add("@CancelledBy", SqlDbType.VarChar, 20).Value = cancelledBy;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value = reason;
        command.Parameters.Add("@ReservationNote", SqlDbType.NVarChar, 300).Value = DraftPointReservationNote;
        int affected = await command.ExecuteNonQueryAsync(ct);
        if (affected < 1)
            throw new InvalidOperationException("Trạng thái hóa đơn đã thay đổi; hãy tải lại dữ liệu.");
    }

    private static async Task ValidateActiveEmployeeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string employeeId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(1) FROM dbo.Employee WITH (UPDLOCK, HOLDLOCK)
            WHERE EmployeeId = @EmployeeId AND IsActive = 1;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 20).Value = employeeId;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
            throw new InvalidOperationException("Nhân viên không tồn tại hoặc đã ngừng hoạt động.");
    }

    private static long[] AllocateCents(long totalCents, long[] weights, long[] caps)
    {
        if (totalCents < 0 || weights.Length == 0 || weights.Length != caps.Length)
            throw new InvalidOperationException("Không thể phân bổ số tiền cho chi tiết hóa đơn.");
        if (totalCents == 0)
            return new long[weights.Length];

        BigInteger weightTotal = weights.Aggregate(BigInteger.Zero, (sum, value) => sum + value);
        if (weightTotal <= 0)
            throw new InvalidOperationException("Không thể phân bổ tiền cho các dòng có giá trị bằng không.");

        var allocations = new long[weights.Length];
        var remainders = new BigInteger[weights.Length];
        long allocated = 0;
        for (int index = 0; index < weights.Length; index++)
        {
            if (weights[index] < 0 || caps[index] < 0)
                throw new InvalidOperationException("Dữ liệu phân bổ tiền không hợp lệ.");
            BigInteger numerator = (BigInteger)totalCents * weights[index];
            BigInteger share = BigInteger.DivRem(numerator, weightTotal, out BigInteger remainder);
            long amount = (long)BigInteger.Min(share, caps[index]);
            allocations[index] = amount;
            remainders[index] = remainder;
            allocated = checked(allocated + amount);
        }
        long remaining = totalCents - allocated;
        while (remaining > 0)
        {
            bool madeProgress = false;
            foreach (int index in Enumerable.Range(0, weights.Length)
                         .OrderByDescending(index => remainders[index])
                         .ThenBy(index => index))
            {
                if (allocations[index] >= caps[index])
                    continue;
                allocations[index]++;
                remaining--;
                madeProgress = true;
                if (remaining == 0)
                    break;
            }
            if (!madeProgress)
                throw new InvalidOperationException("Giảm giá không thể được phân bổ chính xác.");
        }
        return allocations;
    }

    private static long ToCents(decimal amount)
    {
        decimal cents = amount * 100m;
        if (cents != decimal.Truncate(cents) || cents > long.MaxValue || cents < long.MinValue)
            throw new InvalidOperationException("Số tiền phải có tối đa hai chữ số thập phân.");
        return decimal.ToInt64(cents);
    }

    private static decimal FromCents(long cents) => cents / 100m;

    private static long ToThousandths(decimal quantity)
    {
        decimal units = quantity * 1000m;
        if (units != decimal.Truncate(units) || units < 0 || units > long.MaxValue)
            throw new InvalidOperationException("Số lượng phải có tối đa ba chữ số thập phân.");
        return decimal.ToInt64(units);
    }

    private static void ValidateQuantity(decimal quantity, string label)
    {
        if (quantity <= 0 || decimal.Round(quantity, 3, MidpointRounding.AwayFromZero) != quantity)
            throw new ArgumentException($"{label} phải lớn hơn 0 và có tối đa ba chữ số thập phân.");
        if (quantity > 999999999999999.999m)
            throw new ArgumentException($"{label} vượt giới hạn lưu trữ.");
    }

    private static void ValidateMoney(decimal amount, string label, bool allowZero)
    {
        decimal max = 9999999999999999.99m;
        if (amount < 0 || (!allowZero && amount == 0)
            || amount > max
            || decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
            throw new ArgumentException($"{label} phải {(allowZero ? "không âm" : "lớn hơn 0")} và có tối đa hai chữ số thập phân.");
    }

    private static string Required(string? value, int maxLength, string label)
    {
        string normalized = (value ?? "").Trim();
        if (normalized.Length == 0)
            throw new ArgumentException($"{label} không được để trống.");
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{label} vượt quá {maxLength} ký tự.");
        return normalized;
    }

    private static string? Optional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"Giá trị không được vượt quá {maxLength} ký tự.");
        return normalized;
    }

    private static string NormalizePhone(string? phone)
    {
        string normalized = (phone ?? "").Trim();
        if (normalized.Any(char.IsControl))
            throw new ArgumentException("Số điện thoại chứa ký tự không hợp lệ.");
        return normalized;
    }

    private static string EscapeLike(string value)
        => value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    private static string NewId(string prefix, int randomLength)
        => prefix + Guid.NewGuid().ToString("N")[..randomLength].ToUpperInvariant();

    private static object Db(string? value)
        => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value;
    }

    private static void AddQuantity(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 3;
        parameter.Value = value;
    }
}
