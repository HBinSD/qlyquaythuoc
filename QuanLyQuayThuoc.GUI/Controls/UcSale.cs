
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyQuayThuoc.BLL.Models;
using QuanLyQuayThuoc.BLL.Services;
using QuanLyQuayThuoc.DAL.Repositories;
using QuanLyQuayThuoc.GUI.Forms;
using QuanLyQuayThuoc.GUI.Session;

namespace QuanLyQuayThuoc.GUI.Controls;

public sealed class UcSale : UserControl
{
    private readonly SaleService _service = new();
    private readonly SalesWorkflowService _workflow = new();
    private readonly ShiftService _shiftService = new();

    // Mã nhân viên lấy từ tài khoản đăng nhập.
    private readonly string _employeeId;

    private string? _shiftId;

    private readonly List<CartItem> _cart = new();

    private readonly TextBox txtSearch = new();
    private readonly TextBox txtCustomerId = new();
    private readonly TextBox txtPrescriptionId = new();
    private readonly TextBox txtVoucherCode = new();

    private readonly NumericUpDown numQuantity = new();
    private readonly NumericUpDown numPoints = new();

    private readonly ComboBox cboPayment = new();
    private readonly ComboBox cboPendingDrafts = new();

    private readonly DataGridView dgvMedicine = new();
    private readonly DataGridView dgvCart = new();

    private readonly Label lblEmployee = new();
    private readonly Label lblTotal = new();
    private readonly Label lblCartCount = new();
    private readonly Label lblDiscountSummary = new();
    private readonly Label lblShiftStatus = new();
    private readonly Button btnShiftAction = new();
    private readonly Panel shiftStatusBar = new();

    private readonly Button btnSearch = new();
    private readonly Button btnAdd = new();
    private readonly Button btnRemove = new();
    private readonly Button btnCheckout = new();
    private readonly Button btnCustomerLookup = new();
    private readonly Button btnCustomerCreate = new();
    private readonly Button btnPrescriptionCreate = new();
    private readonly Button btnApplyDiscount = new();
    private readonly Button btnHoldDraft = new();
    private readonly Button btnSettleDraft = new();
    private readonly Button btnCancelDraft = new();

    private bool _loading;
    private bool _shiftCheckFailed;

    public UcSale(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Mã nhân viên không hợp lệ.", nameof(employeeId));

        _employeeId = employeeId.Trim();

        InitializeComponent();

        Load += async (_, _) => await InitializeSaleAsync();
    }

    // ==========================================
    // KHỞI TẠO GIAO DIỆN
    // ==========================================

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 9F);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.White,
            Padding = new Padding(18, 10, 15, 5)
        };

        var title = new Label
        {
            Text = "BÁN THUỐC",
            AutoSize = true,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 65, 95),
            Location = new Point(18, 15)
        };

        lblEmployee.AutoSize = true;
        lblEmployee.Text = $"Dược sĩ: {_employeeId}";
        lblEmployee.ForeColor = Color.FromArgb(70, 85, 100);
        lblEmployee.Location = new Point(300, 23);

        header.Controls.Add(title);
        header.Controls.Add(lblEmployee);

        shiftStatusBar.Dock = DockStyle.Top;
        shiftStatusBar.Height = 48;
        shiftStatusBar.Padding = new Padding(12, 6, 12, 6);
        shiftStatusBar.BackColor = Color.FromArgb(255, 244, 224);

        var statusLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));

        lblShiftStatus.Dock = DockStyle.Fill;
        lblShiftStatus.TextAlign = ContentAlignment.MiddleLeft;
        lblShiftStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        lblShiftStatus.ForeColor = Color.FromArgb(160, 85, 15);

        ConfigureButton(btnShiftAction, "Mở ca làm việc",
            Color.FromArgb(225, 125, 25), 155);
        btnShiftAction.Dock = DockStyle.Fill;
        btnShiftAction.Click += async (_, _) => await ShiftActionAsync();
        statusLayout.Controls.Add(lblShiftStatus, 0, 0);
        statusLayout.Controls.Add(btnShiftAction, 1, 0);
        shiftStatusBar.Controls.Add(statusLayout);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Size = new Size(1050, 700),
            BackColor = Color.FromArgb(245, 247, 250),
            BorderStyle = BorderStyle.None
        };
        ControlLayout.SetSplitRatio(split, 0.60);

        BuildMedicinePanel(split.Panel1);
        BuildCartPanel(split.Panel2);

        Controls.Add(split);
        Controls.Add(header);
        Controls.Add(shiftStatusBar);
        ShowShiftCheckPending();
    }

    private void BuildMedicinePanel(Panel panel)
    {
        var title = new Label
        {
            Text = "DANH SÁCH THUỐC",
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(12, 12, 0, 0),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.White
        };

        var searchPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(8),
            BackColor = Color.White,
            WrapContents = false
        };

        txtSearch.Width = 330;
        txtSearch.Font = new Font("Segoe UI", 10F);
        txtSearch.PlaceholderText = "Tên thuốc, mã thuốc hoặc barcode...";

        ConfigureButton(btnSearch, "Tìm", Color.FromArgb(35, 112, 190), 75);
        btnSearch.Click += async (_, _) => await SearchMedicineAsync();

        txtSearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchMedicineAsync();
            }
        };

        searchPanel.Controls.Add(txtSearch);
        searchPanel.Controls.Add(btnSearch);

        ConfigureGrid(dgvMedicine);

        dgvMedicine.Columns.AddRange(
            new DataGridViewColumn[]
            {
                TextColumn("MedicineId", "Mã thuốc", 15),
                TextColumn("MedicineName", "Tên thuốc", 27),
                TextColumn("UnitName", "Đơn vị", 12),
                TextColumn("SalePrice", "Giá bán", 15),
                TextColumn("AvailableStock", "Tồn khả dụng", 15)
            });

        dgvMedicine.Columns["SalePrice"].DefaultCellStyle.Format = "N0";
        dgvMedicine.Columns["AvailableStock"].DefaultCellStyle.Format = "N3";

        var addPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            Padding = new Padding(10, 12, 5, 5),
            BackColor = Color.White,
            WrapContents = false
        };

        numQuantity.Minimum = 0.001M;
        numQuantity.Maximum = 100000;
        numQuantity.DecimalPlaces = 3;
        numQuantity.Increment = 1;
        numQuantity.Value = 1;
        numQuantity.Width = 100;
        numQuantity.Font = new Font("Segoe UI", 10F);

        ConfigureButton(btnAdd, "Thêm vào giỏ", Color.FromArgb(30, 145, 95), 145);
        btnAdd.Click += (_, _) => AddToCart();

        addPanel.Controls.Add(new Label
        {
            Text = "Số lượng:",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        });

        addPanel.Controls.Add(numQuantity);
        addPanel.Controls.Add(btnAdd);

        panel.Controls.Add(dgvMedicine);
        panel.Controls.Add(addPanel);
        panel.Controls.Add(searchPanel);
        panel.Controls.Add(title);
    }

    private void BuildCartPanel(Panel panel)
    {
        var title = new Label
        {
            Text = "GIỎ HÀNG",
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(12, 12, 0, 0),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.White
        };

        lblCartCount.Text = "0 sản phẩm";
        lblCartCount.Dock = DockStyle.Top;
        lblCartCount.Height = 30;
        lblCartCount.Padding = new Padding(12, 5, 0, 0);
        lblCartCount.ForeColor = Color.DimGray;
        lblCartCount.BackColor = Color.White;

        ConfigureGrid(dgvCart);

        dgvCart.Columns.AddRange(
            new DataGridViewColumn[]
            {
                TextColumn("MedicineName", "Tên thuốc", 30),
                TextColumn("UnitName", "Đơn vị", 13),
                TextColumn("Quantity", "Số lượng", 14),
                TextColumn("UnitPrice", "Đơn giá", 18),
                TextColumn("LineTotal", "Thành tiền", 25)
            });

        dgvCart.Columns["Quantity"].DefaultCellStyle.Format = "N3";
        dgvCart.Columns["UnitPrice"].DefaultCellStyle.Format = "N0";
        dgvCart.Columns["LineTotal"].DefaultCellStyle.Format = "N0";

        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 300,
            BackColor = Color.White,
            Padding = new Padding(10)
        };

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            Margin = Padding.Empty
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));

        txtCustomerId.Dock = DockStyle.Fill;
        txtCustomerId.PlaceholderText = "Để trống nếu khách lẻ";
        txtCustomerId.Margin = new Padding(2, 3, 2, 3);

        txtPrescriptionId.Dock = DockStyle.Fill;
        txtPrescriptionId.PlaceholderText = "Bắt buộc nếu có thuốc kê đơn";
        txtPrescriptionId.Margin = new Padding(2, 3, 2, 3);

        cboPayment.Dock = DockStyle.Fill;
        cboPayment.Margin = new Padding(2, 3, 2, 3);
        cboPayment.DropDownStyle = ComboBoxStyle.DropDownList;
        cboPayment.Items.AddRange(new object[]
        {
            "Tiền mặt",
            "Chuyển khoản",
            "QR"
        });
        cboPayment.SelectedIndex = 0;

        AddField(fields, 0, "Mã khách hàng", txtCustomerId);
        ConfigureButton(btnCustomerLookup, "Tra SĐT", Color.FromArgb(35, 112, 190), 70);
        ConfigureButton(btnCustomerCreate, "Tạo khách", Color.FromArgb(85, 105, 130), 70);
        btnCustomerLookup.Height = 27;
        btnCustomerCreate.Height = 27;
        btnCustomerLookup.Click += async (_, _) => await LookupCustomerAsync();
        btnCustomerCreate.Click += async (_, _) => await CreateCustomerAsync();
        fields.Controls.Add(btnCustomerLookup, 2, 0);
        fields.Controls.Add(btnCustomerCreate, 3, 0);

        AddField(fields, 1, "Mã đơn thuốc", txtPrescriptionId);
        ConfigureButton(btnPrescriptionCreate, "Tạo đơn", Color.FromArgb(85, 105, 130), 70);
        btnPrescriptionCreate.Height = 27;
        btnPrescriptionCreate.Click += async (_, _) => await CreatePrescriptionAsync();
        fields.Controls.Add(btnPrescriptionCreate, 2, 1);
        AddField(fields, 2, "Thanh toán", cboPayment);
        fields.SetColumnSpan(cboPayment, 3);

        var discountPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = new Padding(0, 3, 0, 0)
        };
        discountPanel.Controls.Add(new Label
        {
            Text = "Voucher:",
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        });
        txtVoucherCode.Width = 105;
        txtVoucherCode.PlaceholderText = "Mã voucher";
        discountPanel.Controls.Add(txtVoucherCode);
        discountPanel.Controls.Add(new Label
        {
            Text = "Đổi điểm:",
            AutoSize = true,
            Padding = new Padding(4, 6, 0, 0)
        });
        numPoints.Minimum = 0;
        numPoints.Maximum = 100000000;
        numPoints.Width = 75;
        numPoints.ThousandsSeparator = true;
        discountPanel.Controls.Add(numPoints);
        ConfigureButton(btnApplyDiscount, "Áp dụng", Color.FromArgb(35, 112, 190), 82);
        btnApplyDiscount.Height = 28;
        btnApplyDiscount.Click += async (_, _) => await ApplyDiscountsAsync();
        discountPanel.Controls.Add(btnApplyDiscount);

        lblDiscountSummary.Dock = DockStyle.Fill;
        lblDiscountSummary.Text = "Giảm điểm: 0 đ  |  Voucher: 0 đ";
        lblDiscountSummary.ForeColor = Color.FromArgb(70, 85, 100);
        lblDiscountSummary.TextAlign = ContentAlignment.MiddleLeft;

        var draftPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 0)
        };
        cboPendingDrafts.DropDownStyle = ComboBoxStyle.DropDownList;
        cboPendingDrafts.Width = 135;
        cboPendingDrafts.FormattingEnabled = true;
        cboPendingDrafts.Format += (_, e) =>
        {
            if (e.ListItem is PendingInvoiceDraftRow draft)
                e.Value = $"{draft.InvoiceId} | {draft.TotalAmount:N0} đ";
        };
        cboPendingDrafts.SelectedIndexChanged += (_, _) => SetBusy(_loading);
        draftPanel.Controls.Add(cboPendingDrafts);
        ConfigureButton(btnHoldDraft, "Treo đơn", Color.FromArgb(85, 105, 130), 78);
        ConfigureButton(btnSettleDraft, "Thanh toán nháp", Color.FromArgb(30, 145, 95), 112);
        ConfigureButton(btnCancelDraft, "Hủy nháp", Color.FromArgb(190, 70, 70), 78);
        foreach (var button in new[] { btnHoldDraft, btnSettleDraft, btnCancelDraft })
            button.Height = 28;
        btnHoldDraft.Click += async (_, _) => await HoldDraftAsync();
        btnSettleDraft.Click += async (_, _) => await SettleSelectedDraftAsync();
        btnCancelDraft.Click += async (_, _) => await CancelSelectedDraftAsync();
        draftPanel.Controls.Add(btnHoldDraft);
        draftPanel.Controls.Add(btnSettleDraft);
        draftPanel.Controls.Add(btnCancelDraft);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 3, 0, 0)
        };

        ConfigureButton(btnCheckout, "THANH TOÁN", Color.FromArgb(30, 145, 95), 145);
        ConfigureButton(btnRemove, "Xóa dòng", Color.FromArgb(190, 70, 70), 100);

        btnCheckout.Click += async (_, _) => await CheckoutAsync();
        btnRemove.Click += (_, _) => RemoveCartItem();

        actionPanel.Controls.Add(btnCheckout);
        actionPanel.Controls.Add(btnRemove);

        lblTotal.Dock = DockStyle.Fill;
        lblTotal.Text = "TỔNG TIỀN: 0 đ";
        lblTotal.TextAlign = ContentAlignment.MiddleRight;
        lblTotal.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        lblTotal.ForeColor = Color.FromArgb(190, 55, 55);

        var bottomLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Margin = Padding.Empty
        };
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 18));
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 18));
        bottomLayout.Controls.Add(fields, 0, 0);
        bottomLayout.Controls.Add(discountPanel, 0, 1);
        bottomLayout.Controls.Add(lblDiscountSummary, 0, 2);
        bottomLayout.Controls.Add(draftPanel, 0, 3);
        bottomLayout.Controls.Add(lblTotal, 0, 4);
        bottomLayout.Controls.Add(actionPanel, 0, 5);
        bottom.Controls.Add(bottomLayout);

        panel.Controls.Add(dgvCart);
        panel.Controls.Add(bottom);
        panel.Controls.Add(lblCartCount);
        panel.Controls.Add(title);
    }

    // ==========================================
    // KHỞI TẠO CA VÀ TẢI THUỐC
    // ==========================================

    private void ShowShiftCheckPending()
    {
        _shiftId = null;
        _shiftCheckFailed = false;
        shiftStatusBar.BackColor = Color.FromArgb(235, 241, 249);
        lblShiftStatus.ForeColor = Color.FromArgb(55, 85, 120);
        lblShiftStatus.Text = "Đang kiểm tra ca làm việc...";
        btnShiftAction.Visible = false;
    }

    private void ShowShiftCheckError()
    {
        _shiftId = null;
        _shiftCheckFailed = true;
        shiftStatusBar.BackColor = Color.FromArgb(255, 235, 235);
        lblShiftStatus.ForeColor = Color.Firebrick;
        lblShiftStatus.Text = "Không thể kiểm tra ca làm việc. Hãy thử lại.";
        btnShiftAction.Text = "Thử lại";
        btnShiftAction.BackColor = Color.FromArgb(170, 55, 55);
        btnShiftAction.Visible = true;
    }

    private void SetShiftStatus(OpenShiftRow? shift)
    {
        _shiftCheckFailed = false;
        _shiftId = shift?.ShiftId;

        if (shift is null)
        {
            shiftStatusBar.BackColor = Color.FromArgb(255, 244, 224);
            lblShiftStatus.ForeColor = Color.FromArgb(160, 85, 15);
            lblShiftStatus.Text = "Bạn chưa mở ca làm việc.";
            btnShiftAction.Text = "Mở ca làm việc";
            btnShiftAction.BackColor = Color.FromArgb(225, 125, 25);
            btnShiftAction.Visible = true;
            lblEmployee.Text = $"Dược sĩ: {SessionManager.CurrentUser?.FullName ?? _employeeId}";
            return;
        }

        shiftStatusBar.BackColor = Color.FromArgb(226, 246, 235);
        lblShiftStatus.ForeColor = Color.FromArgb(30, 115, 70);
        lblShiftStatus.Text =
            $"Đang trong ca: {shift.ShiftId}  |  {shift.EmployeeName}  |  " +
            $"{shift.OpenedAt:dd/MM/yyyy HH:mm}  |  Tiền đầu ca {shift.OpeningCash:N0} đ";
        btnShiftAction.Text = "Chốt ca / Kết ca";
        btnShiftAction.BackColor = Color.FromArgb(100, 110, 125);
        btnShiftAction.Visible = true;
        lblEmployee.Text = $"Dược sĩ: {shift.EmployeeName}";
    }

    private async Task ShiftActionAsync()
    {
        if (_shiftCheckFailed)
        {
            await InitializeSaleAsync();
            return;
        }

        if (!string.IsNullOrWhiteSpace(_shiftId))
        {
            await CloseShiftAsync(_shiftId);
            return;
        }

        decimal suggestedOpeningCash;
        try
        {
            suggestedOpeningCash = await _shiftService.GetSuggestedOpeningCashAsync(_employeeId);
        }
        catch (Exception ex)
        {
            ShowError("Không thể tải số tiền bàn giao từ ca trước.", ex);
            return;
        }

        using var dialog = new OpenShiftDialog(_employeeId, suggestedOpeningCash);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            SetBusy(true);
            var shift = await _shiftService.OpenShiftAsync(
                _employeeId,
                dialog.OpeningCash,
                dialog.OpeningNote);
            SetShiftStatus(shift);
            await RefreshPendingDraftsAsync();
            await SearchMedicineAsync();
        }
        catch (Exception ex)
        {
            ShowError("Không thể mở ca làm việc.", ex);
            try
            {
                SetShiftStatus(await _shiftService.GetOpenShiftAsync(_employeeId));
            }
            catch (Exception refreshError)
            {
                ShowError("Không thể tải lại trạng thái ca làm việc.", refreshError);
                ShowShiftCheckError();
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task CloseShiftAsync(string shiftId)
    {
        if (_cart.Count > 0)
        {
            MessageBox.Show(
                "Giỏ hàng còn sản phẩm chưa thanh toán. Hãy hoàn tất hoặc xóa giỏ hàng trước khi chốt ca.",
                "Chưa thể chốt ca",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            await _workflow.ExpirePendingDraftsAsync();
            var summary = await _shiftService.GetCloseSummaryAsync(_employeeId, shiftId);
            if (summary.PendingInvoiceCount > 0)
            {
                MessageBox.Show(
                    $"Còn {summary.PendingInvoiceCount} hóa đơn chờ thanh toán. Hãy thanh toán hoặc hủy trước khi chốt ca.",
                    "Chưa thể chốt ca",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new ShiftCloseDialog(summary);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            SetBusy(true);
            var closedShift = await _shiftService.CloseShiftAsync(
                _employeeId,
                shiftId,
                dialog.ActualCash,
                dialog.DifferenceNote);
            SetShiftStatus(null);
            await RefreshPendingDraftsAsync();
            await SearchMedicineAsync();

            MessageBox.Show(
                $"Đã chốt ca {closedShift.ShiftId}.\n" +
                $"Tiền lý thuyết: {closedShift.TheoreticalCash:N0} đ\n" +
                $"Tiền thực tế: {closedShift.ActualCash:N0} đ\n" +
                $"Chênh lệch: {closedShift.DifferenceAmount:N0} đ",
                "Chốt ca thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            try
            {
                ShiftCloseDialog.ShowPrintPreview(this, closedShift);
            }
            catch (Exception printError)
            {
                ShowError("Ca đã được chốt nhưng không thể mở bản xem trước phiếu kết toán.", printError);
            }
        }
        catch (Exception ex)
        {
            ShowError("Không thể chốt ca làm việc.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task InitializeSaleAsync()
    {
        try
        {
            SetBusy(true);

            ShowShiftCheckPending();
            await _workflow.ExpirePendingDraftsAsync();
            var shift = await _shiftService.GetOpenShiftAsync(_employeeId);
            SetShiftStatus(shift);
            await RefreshPendingDraftsAsync();
            await SearchMedicineAsync();
        }
        catch (Exception ex)
        {
            ShowShiftCheckError();
            ShowError("Không thể khởi tạo màn hình bán hàng.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SearchMedicineAsync()
    {
        try
        {
            btnSearch.Enabled = false;

            var data = await _service.SearchMedicinesAsync(txtSearch.Text.Trim());

            dgvMedicine.DataSource = data.Select(x => new MedicineGridRow
            {
                MedicineId = x.MedicineId,
                MedicineName = x.MedicineName,
                UnitName = x.UnitName,
                MedicineUnitId = x.MedicineUnitId,
                ConversionFactor = x.ConversionFactor,
                SalePrice = x.SalePrice,
                AvailableStock = x.AvailableStock,
                RequiresPrescription = x.RequiresPrescription
            }).ToList();
        }
        catch (Exception ex)
        {
            ShowError("Không thể tải danh sách thuốc.", ex);
        }
        finally
        {
            btnSearch.Enabled = true;
        }
    }

    // ==========================================
    // GIỎ HÀNG
    // ==========================================

    private void AddToCart()
    {
        if (string.IsNullOrWhiteSpace(_shiftId))
        {
            MessageBox.Show(
                "Vui lòng mở ca làm việc trước khi lập hóa đơn.",
                "Chưa mở ca",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (dgvMedicine.CurrentRow?.DataBoundItem is not MedicineGridRow medicine)
        {
            MessageBox.Show("Vui lòng chọn thuốc cần bán.");
            return;
        }

        decimal quantity = numQuantity.Value;
        decimal requestedBase = quantity * medicine.ConversionFactor;

        decimal currentBase = _cart
            .Where(x => x.MedicineId == medicine.MedicineId)
            .Sum(x => x.Quantity * x.ConversionFactor);

        if (currentBase + requestedBase > medicine.AvailableStock)
        {
            MessageBox.Show(
                $"Không đủ tồn kho khả dụng.\n" +
                $"Tồn khả dụng: {medicine.AvailableStock:N3} {medicine.UnitName} cơ bản.",
                "Không đủ hàng",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        var existing = _cart.FirstOrDefault(x =>
            x.MedicineId == medicine.MedicineId
            && x.MedicineUnitId == medicine.MedicineUnitId);

        if (existing != null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            _cart.Add(new CartItem
            {
                MedicineId = medicine.MedicineId,
                MedicineName = medicine.MedicineName,
                MedicineUnitId = medicine.MedicineUnitId,
                UnitName = medicine.UnitName,
                ConversionFactor = medicine.ConversionFactor,
                Quantity = quantity,
                UnitPrice = medicine.SalePrice,
                RequiresPrescription = medicine.RequiresPrescription
            });
        }

        RefreshCart();
    }

    private void RemoveCartItem()
    {
        if (dgvCart.CurrentRow?.DataBoundItem is not CartGridRow selected)
        {
            MessageBox.Show("Vui lòng chọn dòng thuốc cần xóa.");
            return;
        }

        var item = _cart.FirstOrDefault(x =>
            x.MedicineId == selected.MedicineId
            && x.MedicineUnitId == selected.MedicineUnitId);

        if (item != null)
        {
            _cart.Remove(item);
            RefreshCart();
        }
    }

    private void RefreshCart()
    {
        dgvCart.DataSource = null;

        dgvCart.DataSource = _cart.Select(x => new CartGridRow
        {
            MedicineId = x.MedicineId,
            MedicineName = x.MedicineName,
            MedicineUnitId = x.MedicineUnitId,
            UnitName = x.UnitName,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            LineTotal = x.Quantity * x.UnitPrice
        }).ToList();

        decimal total = _cart.Sum(x => x.Quantity * x.UnitPrice);

        lblTotal.Text = $"TỔNG TIỀN: {total:N0} đ";
        lblCartCount.Text = $"{_cart.Count} dòng thuốc";
        lblDiscountSummary.Text = "Ưu đãi sẽ được kiểm tra lại khi lưu hóa đơn.";
        SetBusy(_loading);
    }

    // ==========================================
    // THANH TOÁN
    // ==========================================

    private async Task ApplyDiscountsAsync()
    {
        decimal subtotal = _cart.Sum(x => x.Quantity * x.UnitPrice);
        if (subtotal <= 0)
        {
            MessageBox.Show("Hãy thêm thuốc vào giỏ trước khi áp dụng ưu đãi.");
            return;
        }

        try
        {
            SetBusy(true);
            DiscountQuote quote = await _workflow.CalculateDiscountsAsync(
                NullIfEmpty(txtCustomerId.Text),
                subtotal,
                NullIfEmpty(txtVoucherCode.Text),
                decimal.ToInt32(numPoints.Value));
            lblDiscountSummary.Text =
                $"Giảm điểm: {quote.PointDiscount:N0} đ  |  Voucher: {quote.VoucherDiscount:N0} đ  |  " +
                $"Còn trả: {quote.TotalAmount:N0} đ  |  Điểm hiện có: {quote.AvailablePoints:N0}";
        }
        catch (Exception ex)
        {
            ShowError("Không thể áp dụng ưu đãi.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private Task CheckoutAsync() => CreateDraftAsync(settleImmediately: true);

    private Task HoldDraftAsync() => CreateDraftAsync(settleImmediately: false);

    private async Task CreateDraftAsync(bool settleImmediately)
    {
        if (string.IsNullOrWhiteSpace(_shiftId))
        {
            MessageBox.Show("Vui lòng mở ca làm việc trước khi lập hóa đơn.");
            return;
        }
        if (_cart.Count == 0)
        {
            MessageBox.Show("Giỏ hàng chưa có thuốc.");
            return;
        }
        if (_cart.Any(x => x.RequiresPrescription) &&
            string.IsNullOrWhiteSpace(txtPrescriptionId.Text))
        {
            MessageBox.Show(
                "Giỏ hàng có thuốc kê đơn. Vui lòng nhập mã đơn thuốc hoặc tạo đơn từ giỏ hàng.",
                "Thiếu đơn thuốc",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            txtPrescriptionId.Focus();
            return;
        }

        string paymentMethod = SelectedPaymentMethod();
        InvoiceDraftResult? draft = null;
        try
        {
            SetBusy(true);
            DiscountQuote quote = await _workflow.CalculateDiscountsAsync(
                NullIfEmpty(txtCustomerId.Text),
                _cart.Sum(x => x.Quantity * x.UnitPrice),
                NullIfEmpty(txtVoucherCode.Text),
                decimal.ToInt32(numPoints.Value));
            if (quote.TotalAmount <= 0)
                throw new InvalidOperationException("Tổng tiền sau khi áp dụng ưu đãi phải lớn hơn 0.");

            PaymentDetails? payment = null;
            if (settleImmediately)
            {
                if (!TryCollectPayment(quote.TotalAmount, paymentMethod, out payment))
                    return;
            }
            else
            {
                var confirmation = MessageBox.Show(
                    $"Lưu hóa đơn chờ thanh toán?\n\n" +
                    $"Tiền hàng: {quote.Subtotal:N0} đ\n" +
                    $"Giảm điểm: {quote.PointDiscount:N0} đ\n" +
                    $"Giảm voucher: {quote.VoucherDiscount:N0} đ\n" +
                    $"Khách phải trả: {quote.TotalAmount:N0} đ",
                    "Treo hóa đơn",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirmation != DialogResult.Yes)
                    return;
            }

            draft = await _workflow.CreateInvoiceDraftAsync(new CreateInvoiceDraftRequest(
                _employeeId,
                _shiftId,
                NullIfEmpty(txtCustomerId.Text),
                NullIfEmpty(txtPrescriptionId.Text),
                NullIfEmpty(txtVoucherCode.Text),
                decimal.ToInt32(numPoints.Value),
                _cart.Select(item => new SalesLineInput(
                    item.MedicineId,
                    item.MedicineUnitId,
                    item.Quantity)).ToList()));

            _cart.Clear();
            txtCustomerId.Clear();
            txtPrescriptionId.Clear();
            txtVoucherCode.Clear();
            numPoints.Value = 0;
            RefreshCart();
            await RefreshPendingDraftsAsync();

            if (settleImmediately)
            {
                try
                {
                    await SettleDraftAsync(
                        draft.InvoiceId,
                        draft.TotalAmount,
                        paymentMethod,
                        payment!.TransactionReference,
                        payment.Change);
                    await RefreshPendingDraftsAsync();
                    await SearchMedicineAsync();
                }
                catch (Exception ex)
                {
                    await RefreshPendingDraftsAsync();
                    ShowError($"Đã lưu hóa đơn nháp {draft.InvoiceId}, nhưng chưa thanh toán được.", ex);
                    return;
                }
            }
            else
            {
                MessageBox.Show(
                    $"Đã lưu hóa đơn nháp {draft.InvoiceId}.\n" +
                    $"Số tiền: {draft.TotalAmount:N0} đ\n" +
                    $"Hạn giữ hàng đến: {draft.ExpiresAt:dd/MM/yyyy HH:mm}.",
                    "Đã treo đơn",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            ShowError("Không thể tạo hóa đơn nháp.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SettleSelectedDraftAsync()
    {
        if (cboPendingDrafts.SelectedItem is not PendingInvoiceDraftRow draft)
        {
            MessageBox.Show("Vui lòng chọn hóa đơn nháp cần thanh toán.");
            return;
        }

        string paymentMethod = SelectedPaymentMethod();
        if (!TryCollectPayment(draft.TotalAmount, paymentMethod, out PaymentDetails? payment))
            return;

        try
        {
            SetBusy(true);
            await SettleDraftAsync(
                draft.InvoiceId,
                draft.TotalAmount,
                paymentMethod,
                payment!.TransactionReference,
                payment.Change);
            await RefreshPendingDraftsAsync();
            await SearchMedicineAsync();
        }
        catch (Exception ex)
        {
            await RefreshPendingDraftsAsync();
            ShowError("Không thể thanh toán hóa đơn nháp.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SettleDraftAsync(
        string invoiceId,
        decimal amount,
        string paymentMethod,
        string? transactionReference,
        decimal change)
    {
        if (string.IsNullOrWhiteSpace(_shiftId))
            throw new InvalidOperationException("Không có ca làm việc đang mở.");
        var result = await _workflow.SettleInvoiceDraftAsync(new SettleInvoiceDraftRequest(
            invoiceId,
            _employeeId,
            _shiftId,
            amount,
            paymentMethod,
            transactionReference));
        MessageBox.Show(
            $"Thanh toán thành công hóa đơn {result.InvoiceId}.\n" +
            $"Số tiền: {result.AmountPaid:N0} đ\n" +
            $"Tiền thừa: {change:N0} đ\n" +
            $"Điểm tích lũy mới: {result.PointsEarned:N0}.",
            "Hoàn tất",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private bool TryCollectPayment(
        decimal totalAmount,
        string paymentMethod,
        out PaymentDetails? payment)
    {
        payment = null;
        PaymentDetails? collectedPayment = null;
        using var dialog = new Form
        {
            Text = paymentMethod == "CASH" ? "Nhận tiền mặt" : "Xác nhận thanh toán",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(420, paymentMethod == "CASH" ? 170 : 190)
        };
        var amountLabel = new Label
        {
            Text = $"Khách phải trả: {totalAmount:N0} đ",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var reference = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Mã giao dịch (nếu có)"
        };
        var paidAmount = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            DecimalPlaces = 0,
            ThousandsSeparator = true,
            Minimum = 0,
            Maximum = 999999999999M,
            Value = Math.Min(Math.Ceiling(totalAmount), 999999999999M)
        };
        var changeLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        var verified = new CheckBox
        {
            Text = "Đã xác nhận nhận đủ tiền chuyển khoản / QR",
            AutoSize = true
        };
        var errorLabel = new Label { Dock = DockStyle.Fill, ForeColor = Color.Firebrick };
        var accept = new Button { Text = "Xác nhận", AutoSize = true };
        var cancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(accept);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = paymentMethod == "CASH" ? 5 : 5,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        if (paymentMethod == "CASH")
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        }
        else
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.Controls.Add(amountLabel, 0, 0);
        if (paymentMethod == "CASH")
        {
            layout.Controls.Add(paidAmount, 0, 1);
            layout.Controls.Add(changeLabel, 0, 2);
            layout.Controls.Add(errorLabel, 0, 3);
        }
        else
        {
            layout.Controls.Add(reference, 0, 1);
            layout.Controls.Add(verified, 0, 2);
            layout.Controls.Add(errorLabel, 0, 3);
        }
        layout.Controls.Add(buttons, 0, 4);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = accept;
        dialog.CancelButton = cancel;

        if (paymentMethod == "CASH")
        {
            void UpdateChange()
            {
                changeLabel.Text = $"Tiền thừa: {Math.Max(0, paidAmount.Value - totalAmount):N0} đ";
            }
            paidAmount.ValueChanged += (_, _) => UpdateChange();
            UpdateChange();
        }

        accept.Click += (_, _) =>
        {
            if (paymentMethod == "CASH" && paidAmount.Value < totalAmount)
            {
                errorLabel.Text = "Số tiền khách đưa chưa đủ; không hỗ trợ bán chịu.";
                return;
            }
            if (paymentMethod != "CASH" && !verified.Checked)
            {
                errorLabel.Text = "Chỉ xác nhận sau khi đã nhận đủ tiền.";
                return;
            }

            decimal tendered = paymentMethod == "CASH" ? paidAmount.Value : totalAmount;
            collectedPayment = new PaymentDetails(
                tendered,
                Math.Max(0, tendered - totalAmount),
                NullIfEmpty(reference.Text));
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };

        bool confirmed = dialog.ShowDialog(this) == DialogResult.OK;
        if (confirmed)
            payment = collectedPayment;
        return confirmed;
    }

    private async Task CancelSelectedDraftAsync()
    {
        if (cboPendingDrafts.SelectedItem is not PendingInvoiceDraftRow draft)
        {
            MessageBox.Show("Vui lòng chọn hóa đơn nháp cần hủy.");
            return;
        }
        if (MessageBox.Show(
                $"Hủy hóa đơn nháp {draft.InvoiceId} và giải phóng lượng thuốc đang giữ?",
                "Xác nhận hủy",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            SetBusy(true);
            await _workflow.CancelInvoiceDraftAsync(
                draft.InvoiceId,
                _employeeId,
                "Nhân viên hủy hóa đơn nháp.");
            await RefreshPendingDraftsAsync();
            await SearchMedicineAsync();
        }
        catch (Exception ex)
        {
            ShowError("Không thể hủy hóa đơn nháp.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshPendingDraftsAsync()
    {
        if (string.IsNullOrWhiteSpace(_shiftId))
        {
            cboPendingDrafts.DataSource = null;
            SetBusy(_loading);
            return;
        }

        await _workflow.ExpirePendingDraftsAsync();
        var drafts = await _workflow.GetPendingInvoiceDraftsAsync(_shiftId);
        cboPendingDrafts.DataSource = null;
        cboPendingDrafts.DataSource = drafts;
        SetBusy(_loading);
    }

    private async Task LookupCustomerAsync()
    {
        using var dialog = new Form
        {
            Text = "Tra cứu khách hàng",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(420, 300)
        };
        var phone = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Nhập số điện thoại" };
        var list = new ListBox { Dock = DockStyle.Fill, FormattingEnabled = true };
        list.Format += (_, e) =>
        {
            if (e.ListItem is SalesCustomerRow customer)
                e.Value = $"{customer.FullName} | {customer.Phone} | {customer.CustomerId}";
        };
        var status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        var search = new Button { Text = "Tra cứu", AutoSize = true };
        var select = new Button { Text = "Chọn khách", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Đóng", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(select);
        buttons.Controls.Add(search);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.Controls.Add(phone, 0, 0);
        layout.Controls.Add(list, 0, 1);
        layout.Controls.Add(status, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = select;
        dialog.CancelButton = cancel;
        search.Click += async (_, _) =>
        {
            try
            {
                list.DataSource = await _workflow.SearchCustomersByPhoneAsync(phone.Text);
                list.DisplayMember = nameof(SalesCustomerRow.FullName);
                status.Text = $"{list.Items.Count} kết quả.";
            }
            catch (Exception ex)
            {
                ShowError("Không thể tra cứu khách hàng.", ex);
            }
        };
        select.Click += (_, _) =>
        {
            if (list.SelectedItem is SalesCustomerRow customer)
            {
                dialog.Tag = customer;
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
            else
            {
                status.Text = "Hãy chọn một khách hàng trong danh sách.";
                dialog.DialogResult = DialogResult.None;
            }
        };
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Tag is SalesCustomerRow selected)
            txtCustomerId.Text = selected.CustomerId;
    }

    private async Task CreateCustomerAsync()
    {
        using var dialog = new Form
        {
            Text = "Tạo khách hàng nhanh",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(380, 160)
        };
        var name = new TextBox { Dock = DockStyle.Fill };
        var phone = new TextBox { Dock = DockStyle.Fill };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(12) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(fields, 0, "Họ tên", name);
        AddField(fields, 1, "Điện thoại", phone);
        var save = new Button { Text = "Tạo khách", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        fields.Controls.Add(buttons, 0, 2);
        fields.SetColumnSpan(buttons, 2);
        dialog.Controls.Add(fields);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            SetBusy(true);
            var customer = await _workflow.CreateCustomerAsync(new CreateCustomerRequest(
                name.Text,
                phone.Text));
            txtCustomerId.Text = customer.CustomerId;
            MessageBox.Show(
                $"Đã chọn khách hàng {customer.FullName} ({customer.CustomerId}).",
                "Khách hàng",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError("Không thể tạo khách hàng.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task CreatePrescriptionAsync()
    {
        if (_cart.Count == 0)
        {
            MessageBox.Show("Hãy thêm thuốc vào giỏ trước khi tạo đơn thuốc.");
            return;
        }
        using var dialog = new Form
        {
            Text = "Tiếp nhận đơn thuốc",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(460, 245)
        };
        var patient = new TextBox { Dock = DockStyle.Fill };
        var phone = new TextBox { Dock = DockStyle.Fill };
        var doctor = new TextBox { Dock = DockStyle.Fill };
        var facility = new TextBox { Dock = DockStyle.Fill };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(12) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(fields, 0, "Bệnh nhân", patient);
        AddField(fields, 1, "Điện thoại", phone);
        AddField(fields, 2, "Bác sĩ", doctor);
        AddField(fields, 3, "Cơ sở khám", facility);
        var save = new Button { Text = "Lưu đơn thuốc", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        fields.Controls.Add(buttons, 0, 4);
        fields.SetColumnSpan(buttons, 2);
        dialog.Controls.Add(fields);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            SetBusy(true);
            string prescriptionId = await _workflow.CreatePrescriptionAsync(new CreatePrescriptionRequest(
                _employeeId,
                patient.Text,
                NullIfEmpty(phone.Text),
                DateOnly.FromDateTime(DateTime.Today),
                NullIfEmpty(doctor.Text),
                NullIfEmpty(facility.Text),
                _cart.Select(item => new PrescriptionLineInput(
                    item.MedicineId,
                    item.UnitName,
                    item.Quantity)).ToList()));
            txtPrescriptionId.Text = prescriptionId;
            MessageBox.Show(
                $"Đã tiếp nhận đơn thuốc {prescriptionId} cho {patient.Text.Trim()}.",
                "Đơn thuốc",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError("Không thể tiếp nhận đơn thuốc.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string SelectedPaymentMethod() => cboPayment.SelectedIndex switch
    {
        1 => "TRANSFER",
        2 => "QR",
        _ => "CASH"
    };

    // ==========================================
    // HỖ TRỢ
    // ==========================================

    private void SetBusy(bool busy)
    {
        _loading = busy;
        bool hasOpenShift = !string.IsNullOrWhiteSpace(_shiftId);
        bool hasCart = _cart.Count > 0;

        btnSearch.Enabled = !busy;
        btnAdd.Enabled = !busy && hasOpenShift;
        btnRemove.Enabled = !busy && hasCart;
        btnCheckout.Enabled = !busy && hasOpenShift && hasCart;
        btnHoldDraft.Enabled = !busy && hasOpenShift && hasCart;
        btnApplyDiscount.Enabled = !busy && hasCart;
        btnSettleDraft.Enabled = !busy && hasOpenShift && cboPendingDrafts.SelectedItem is PendingInvoiceDraftRow;
        btnCancelDraft.Enabled = !busy && hasOpenShift && cboPendingDrafts.SelectedItem is PendingInvoiceDraftRow;
        btnCustomerLookup.Enabled = !busy;
        btnCustomerCreate.Enabled = !busy;
        btnPrescriptionCreate.Enabled = !busy && hasCart;
        btnShiftAction.Enabled = !busy;

        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeight = 40;
        grid.RowTemplate.Height = 36;
        grid.GridColor = Color.FromArgb(230, 234, 239);

        grid.ColumnHeadersDefaultCellStyle.BackColor =
            Color.FromArgb(231, 238, 246);

        grid.ColumnHeadersDefaultCellStyle.ForeColor =
            Color.FromArgb(40, 55, 75);

        grid.ColumnHeadersDefaultCellStyle.Font =
            new Font("Segoe UI", 9F, FontStyle.Bold);

        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
    }

    private static DataGridViewTextBoxColumn TextColumn(
        string property,
        string header,
        float weight)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = property,
            DataPropertyName = property,
            HeaderText = header,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
    }

    private static void ConfigureButton(
        Button button,
        string text,
        Color color,
        int width)
    {
        button.Text = text;
        button.Width = width;
        button.Height = 34;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(5, 0, 5, 0);
    }

    private static void AddField(
        TableLayoutPanel panel,
        int row,
        string caption,
        Control input)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        panel.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);

        panel.Controls.Add(input, 1, row);
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ShowError(string message, Exception ex)
    {
        MessageBox.Show(
            $"{message}\n\nChi tiết: {ex.Message}",
            "Lỗi",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    // ==========================================
    // MODEL DÙNG CHO DATAGRIDVIEW
    // ==========================================

    private sealed class MedicineGridRow
    {
        public string MedicineId { get; set; } = "";
        public string MedicineName { get; set; } = "";
        public int MedicineUnitId { get; set; }
        public string UnitName { get; set; } = "";
        public decimal ConversionFactor { get; set; }
        public decimal SalePrice { get; set; }
        public decimal AvailableStock { get; set; }
        public bool RequiresPrescription { get; set; }
    }

    private sealed class CartItem
    {
        public string MedicineId { get; set; } = "";
        public string MedicineName { get; set; } = "";
        public int MedicineUnitId { get; set; }
        public string UnitName { get; set; } = "";
        public decimal ConversionFactor { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool RequiresPrescription { get; set; }
    }

    private sealed class CartGridRow
    {
        public string MedicineId { get; set; } = "";
        public string MedicineName { get; set; } = "";
        public int MedicineUnitId { get; set; }
        public string UnitName { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    private sealed record PaymentDetails(
        decimal TenderedAmount,
        decimal Change,
        string? TransactionReference);
}
