
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyQuayThuoc.BLL.Services;
using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.GUI.Controls;

public sealed class UcSale : UserControl
{
    private readonly SaleService _service = new();

    // Mã nhân viên lấy từ tài khoản đăng nhập.
    private readonly string _employeeId;

    private string? _shiftId;

    private readonly List<CartItem> _cart = new();

    private readonly TextBox txtSearch = new();
    private readonly TextBox txtCustomerId = new();
    private readonly TextBox txtPrescriptionId = new();

    private readonly NumericUpDown numQuantity = new();

    private readonly ComboBox cboPayment = new();

    private readonly DataGridView dgvMedicine = new();
    private readonly DataGridView dgvCart = new();

    private readonly Label lblEmployee = new();
    private readonly Label lblShift = new();
    private readonly Label lblTotal = new();
    private readonly Label lblCartCount = new();

    private readonly Button btnSearch = new();
    private readonly Button btnAdd = new();
    private readonly Button btnRemove = new();
    private readonly Button btnCheckout = new();

    private bool _loading;

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

        lblShift.AutoSize = true;
        lblShift.Text = "Đang kiểm tra ca làm việc...";
        lblShift.ForeColor = Color.FromArgb(30, 130, 85);
        lblShift.Location = new Point(540, 23);

        header.Controls.Add(title);
        header.Controls.Add(lblEmployee);
        header.Controls.Add(lblShift);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor = Color.FromArgb(245, 247, 250),
            BorderStyle = BorderStyle.None
        };

        BuildMedicinePanel(split.Panel1);
        BuildCartPanel(split.Panel2);

        Controls.Add(split);
        Controls.Add(header);
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
            Height = 245,
            BackColor = Color.White,
            Padding = new Padding(12)
        };

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 100,
            ColumnCount = 2,
            RowCount = 3
        };

        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        txtCustomerId.Dock = DockStyle.Fill;
        txtCustomerId.PlaceholderText = "Để trống nếu khách lẻ";

        txtPrescriptionId.Dock = DockStyle.Fill;
        txtPrescriptionId.PlaceholderText = "Bắt buộc nếu có thuốc kê đơn";

        cboPayment.Dock = DockStyle.Fill;
        cboPayment.DropDownStyle = ComboBoxStyle.DropDownList;
        cboPayment.Items.AddRange(new object[]
        {
            "Tiền mặt",
            "Chuyển khoản",
            "QR"
        });
        cboPayment.SelectedIndex = 0;

        AddField(fields, 0, "Mã khách hàng", txtCustomerId);
        AddField(fields, 1, "Mã đơn thuốc", txtPrescriptionId);
        AddField(fields, 2, "Thanh toán", cboPayment);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        ConfigureButton(btnCheckout, "THANH TOÁN", Color.FromArgb(30, 145, 95), 145);
        ConfigureButton(btnRemove, "Xóa dòng", Color.FromArgb(190, 70, 70), 100);

        btnCheckout.Click += async (_, _) => await CheckoutAsync();
        btnRemove.Click += (_, _) => RemoveCartItem();

        actionPanel.Controls.Add(btnCheckout);
        actionPanel.Controls.Add(btnRemove);

        lblTotal.Dock = DockStyle.Bottom;
        lblTotal.Height = 48;
        lblTotal.Text = "TỔNG TIỀN: 0 đ";
        lblTotal.TextAlign = ContentAlignment.MiddleRight;
        lblTotal.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        lblTotal.ForeColor = Color.FromArgb(190, 55, 55);

        bottom.Controls.Add(fields);
        bottom.Controls.Add(lblTotal);
        bottom.Controls.Add(actionPanel);

        panel.Controls.Add(dgvCart);
        panel.Controls.Add(bottom);
        panel.Controls.Add(lblCartCount);
        panel.Controls.Add(title);
    }

    // ==========================================
    // KHỞI TẠO CA VÀ TẢI THUỐC
    // ==========================================

    private async Task InitializeSaleAsync()
    {
        try
        {
            SetBusy(true);

            var shift = await _service.GetOpenShiftAsync(_employeeId);

            if (shift == null)
            {
                lblShift.Text = "Chưa mở ca làm việc";
                lblShift.ForeColor = Color.Firebrick;

                MessageBox.Show(
                    "Dược sĩ chưa có ca làm việc đang mở.\n" +
                    "Vui lòng mở ca trước khi bán thuốc.",
                    "Chưa mở ca",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                btnCheckout.Enabled = false;
                btnAdd.Enabled = false;
                return;
            }

            _shiftId = shift.ShiftId;

            lblEmployee.Text = $"Dược sĩ: {shift.EmployeeName}";
            lblShift.Text = $"Ca: {shift.ShiftId}";

            await SearchMedicineAsync();
        }
        catch (Exception ex)
        {
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
    }

    // ==========================================
    // THANH TOÁN
    // ==========================================

    private async Task CheckoutAsync()
    {
        if (string.IsNullOrWhiteSpace(_shiftId))
        {
            MessageBox.Show("Không có ca làm việc hợp lệ.");
            return;
        }

        if (_cart.Count == 0)
        {
            MessageBox.Show("Giỏ hàng chưa có thuốc.");
            return;
        }

        bool hasPrescriptionMedicine = _cart.Any(x => x.RequiresPrescription);

        if (hasPrescriptionMedicine &&
            string.IsNullOrWhiteSpace(txtPrescriptionId.Text))
        {
            MessageBox.Show(
                "Giỏ hàng có thuốc kê đơn.\nVui lòng nhập mã đơn thuốc.",
                "Thiếu đơn thuốc",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            txtPrescriptionId.Focus();
            return;
        }

        decimal total = _cart.Sum(x => x.Quantity * x.UnitPrice);

        var confirm = MessageBox.Show(
            $"Xác nhận thanh toán hóa đơn?\n\nTổng tiền: {total:N0} đ",
            "Xác nhận bán hàng",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            SetBusy(true);

            string paymentMethod = cboPayment.SelectedIndex switch
            {
                1 => "TRANSFER",
                2 => "QR",
                _ => "CASH"
            };

            var request = new SaleRepository.CheckoutRequest
            {
                EmployeeId = _employeeId,
                ShiftId = _shiftId,
                CustomerId = NullIfEmpty(txtCustomerId.Text),
                PrescriptionId = NullIfEmpty(txtPrescriptionId.Text),
                PaymentMethod = paymentMethod,
                Lines = _cart.Select(x => new SaleRepository.SaleLine
                {
                    MedicineId = x.MedicineId,
                    MedicineUnitId = x.MedicineUnitId,
                    UnitName = x.UnitName,
                    ConversionFactor = x.ConversionFactor,
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice
                }).ToList()
            };

            string invoiceId = await _service.CheckoutAsync(request);

            MessageBox.Show(
                $"Thanh toán thành công!\n\nMã hóa đơn: {invoiceId}\n" +
                $"Tổng tiền: {total:N0} đ",
                "Hoàn tất",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            _cart.Clear();
            txtCustomerId.Clear();
            txtPrescriptionId.Clear();

            RefreshCart();

            await SearchMedicineAsync();
        }
        catch (Exception ex)
        {
            ShowError("Thanh toán thất bại. Hóa đơn chưa được hoàn tất.", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ==========================================
    // HỖ TRỢ
    // ==========================================

    private void SetBusy(bool busy)
    {
        _loading = busy;

        btnSearch.Enabled = !busy;
        btnAdd.Enabled = !busy && !string.IsNullOrWhiteSpace(_shiftId);
        btnRemove.Enabled = !busy;
        btnCheckout.Enabled = !busy && !string.IsNullOrWhiteSpace(_shiftId);

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
}
