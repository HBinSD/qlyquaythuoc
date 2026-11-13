
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyQuayThuoc.BLL.Services;
using QuanLyQuayThuoc.DAL.Repositories;

namespace QuanLyQuayThuoc.GUI.Controls;

public sealed class UcSupplier : UserControl
{
    private readonly SupplierService _service = new();

    private readonly TextBox txtSearch = new();
    private readonly ComboBox cboStatus = new();
    private readonly DataGridView dgvSupplier = new();

    private readonly Button btnSearch = new();
    private readonly Button btnAdd = new();
    private readonly Button btnEdit = new();
    private readonly Button btnToggleStatus = new();
    private readonly Button btnRefresh = new();

    private readonly Label lblCount = new();
    private readonly Label lblTitle = new();

    private bool _isLoading;

    public UcSupplier()
    {
        InitializeComponent();

        Load += async (_, _) => await LoadSuppliersAsync();
    }

    // =========================
    // GIAO DIỆN
    // =========================

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 9F);

        // Tiêu đề
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            Padding = new Padding(20, 12, 15, 8),
            BackColor = Color.White
        };

        lblTitle.Text = "QUẢN LÝ NHÀ CUNG CẤP";
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(35, 55, 80);
        lblTitle.AutoSize = true;
        lblTitle.Location = new Point(20, 17);

        headerPanel.Controls.Add(lblTitle);

        // Thanh tìm kiếm
        var filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 65,
            Padding = new Padding(15, 13, 10, 5),
            BackColor = Color.White,
            WrapContents = false
        };

        txtSearch.Width = 300;
        txtSearch.Height = 32;
        txtSearch.Font = new Font("Segoe UI", 10F);
        txtSearch.PlaceholderText =
            "Tìm mã, tên, số điện thoại, mã số thuế...";

        cboStatus.Width = 175;
        cboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
        cboStatus.Font = new Font("Segoe UI", 10F);

        cboStatus.Items.AddRange(new object[]
        {
            "Tất cả trạng thái",
            "Đang hoạt động",
            "Ngừng hoạt động"
        });

        cboStatus.SelectedIndex = 0;

        ConfigureButton(btnSearch, "Tìm kiếm",
            Color.FromArgb(35, 112, 190));

        ConfigureButton(btnRefresh, "Làm mới",
            Color.FromArgb(100, 110, 125));

        btnSearch.Click += async (_, _) => await LoadSuppliersAsync();

        btnRefresh.Click += async (_, _) =>
        {
            txtSearch.Clear();
            cboStatus.SelectedIndex = 0;
            await LoadSuppliersAsync();
        };

        txtSearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LoadSuppliersAsync();
            }
        };

        filterPanel.Controls.Add(MakeCaption("Từ khóa"));
        filterPanel.Controls.Add(txtSearch);
        filterPanel.Controls.Add(MakeCaption("Trạng thái"));
        filterPanel.Controls.Add(cboStatus);
        filterPanel.Controls.Add(btnSearch);
        filterPanel.Controls.Add(btnRefresh);

        // Thanh chức năng
        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 55,
            Padding = new Padding(15, 9, 10, 5),
            BackColor = Color.FromArgb(245, 247, 250),
            WrapContents = false
        };

        ConfigureButton(btnAdd, "+ Thêm nhà cung cấp",
            Color.FromArgb(30, 145, 95));

        ConfigureButton(btnEdit, "Sửa thông tin",
            Color.FromArgb(230, 150, 40));

        ConfigureButton(btnToggleStatus, "Đổi trạng thái",
            Color.FromArgb(100, 110, 125));

        lblCount.AutoSize = true;
        lblCount.Font = new Font("Segoe UI", 9F);
        lblCount.ForeColor = Color.DimGray;
        lblCount.Padding = new Padding(15, 10, 0, 0);

        btnAdd.Click += async (_, _) => await AddSupplierAsync();
        btnEdit.Click += async (_, _) => await EditSupplierAsync();
        btnToggleStatus.Click += async (_, _) => await ToggleStatusAsync();

        actionPanel.Controls.AddRange(new Control[]
        {
            btnAdd,
            btnEdit,
            btnToggleStatus,
            lblCount
        });

        // Bảng dữ liệu
        ConfigureGrid();

        // Thêm control
        Controls.Add(dgvSupplier);
        Controls.Add(actionPanel);
        Controls.Add(filterPanel);
        Controls.Add(headerPanel);
    }

    private void ConfigureGrid()
    {
        dgvSupplier.Dock = DockStyle.Fill;
        dgvSupplier.BackgroundColor = Color.White;
        dgvSupplier.BorderStyle = BorderStyle.None;

        dgvSupplier.AutoGenerateColumns = false;
        dgvSupplier.AllowUserToAddRows = false;
        dgvSupplier.AllowUserToDeleteRows = false;
        dgvSupplier.ReadOnly = true;

        dgvSupplier.MultiSelect = false;
        dgvSupplier.SelectionMode =
            DataGridViewSelectionMode.FullRowSelect;

        dgvSupplier.RowHeadersVisible = false;
        dgvSupplier.AutoSizeColumnsMode =
            DataGridViewAutoSizeColumnsMode.Fill;

        dgvSupplier.EnableHeadersVisualStyles = false;

        dgvSupplier.ColumnHeadersDefaultCellStyle.BackColor =
            Color.FromArgb(231, 238, 246);

        dgvSupplier.ColumnHeadersDefaultCellStyle.ForeColor =
            Color.FromArgb(40, 55, 75);

        dgvSupplier.ColumnHeadersDefaultCellStyle.Font =
            new Font("Segoe UI", 9F, FontStyle.Bold);

        dgvSupplier.ColumnHeadersHeight = 42;
        dgvSupplier.RowTemplate.Height = 38;

        dgvSupplier.DefaultCellStyle.Font =
            new Font("Segoe UI", 9F);

        dgvSupplier.DefaultCellStyle.SelectionBackColor =
            Color.FromArgb(220, 235, 250);

        dgvSupplier.DefaultCellStyle.SelectionForeColor =
            Color.FromArgb(30, 45, 60);

        dgvSupplier.GridColor = Color.FromArgb(230, 234, 239);

        dgvSupplier.Columns.AddRange(
            new DataGridViewColumn[]
            {
                CreateColumn("SupplierId", "Mã NCC", 12),
                CreateColumn("SupplierName", "Tên nhà cung cấp", 23),
                CreateColumn("Phone", "Số điện thoại", 14),
                CreateColumn("Email", "Email", 18),
                CreateColumn("TaxCode", "Mã số thuế", 13),
                CreateColumn("Address", "Địa chỉ", 20),
                CreateStatusColumn()
            });

        dgvSupplier.CellFormatting += DgvSupplier_CellFormatting;

        dgvSupplier.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0)
                await EditSupplierAsync();
        };
    }

    private static DataGridViewTextBoxColumn CreateColumn(
        string property,
        string header,
        float fillWeight)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = property,
            DataPropertyName = property,
            HeaderText = header,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
    }

    private static DataGridViewTextBoxColumn CreateStatusColumn()
    {
        return new DataGridViewTextBoxColumn
        {
            Name = "StatusText",
            HeaderText = "Trạng thái",
            DataPropertyName = "IsActive",
            FillWeight = 12,
            ReadOnly = true
        };
    }

    private void DgvSupplier_CellFormatting(
        object? sender,
        DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0)
            return;

        if (dgvSupplier.Columns[e.ColumnIndex].Name == "StatusText")
        {
            if (dgvSupplier.Rows[e.RowIndex].DataBoundItem
                is SupplierRepository.SupplierRow supplier)
            {
                e.Value = supplier.IsActive
                    ? "Đang hoạt động"
                    : "Ngừng hoạt động";

                e.CellStyle.ForeColor = supplier.IsActive
                    ? Color.FromArgb(25, 135, 84)
                    : Color.FromArgb(190, 65, 65);

                e.CellStyle.Font =
                    new Font("Segoe UI", 9F, FontStyle.Bold);

                e.FormattingApplied = true;
            }
        }
    }

    // =========================
    // TẢI DANH SÁCH
    // =========================

    private async Task LoadSuppliersAsync()
    {
        if (_isLoading)
            return;

        try
        {
            _isLoading = true;
            SetLoading(true);

            string keyword = txtSearch.Text.Trim();

            int statusIndex = cboStatus.SelectedIndex;

            bool includeInactive = statusIndex != 1;

            var data = await _service.GetAllAsync(
                keyword,
                includeInactive);

            if (statusIndex == 1)
            {
                data = data.Where(x => x.IsActive).ToList();
            }
            else if (statusIndex == 2)
            {
                data = data.Where(x => !x.IsActive).ToList();
            }

            dgvSupplier.DataSource = null;
            dgvSupplier.DataSource = data;

            lblCount.Text = $"Có {data.Count} nhà cung cấp";

            btnToggleStatus.Text = "Đổi trạng thái";
        }
        catch (Exception ex)
        {
            ShowError("Không thể tải danh sách nhà cung cấp.", ex);
        }
        finally
        {
            SetLoading(false);
            _isLoading = false;
        }
    }

    // =========================
    // THÊM NHÀ CUNG CẤP
    // =========================

    private async Task AddSupplierAsync()
    {
        using var dialog = new SupplierEditDialog();

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        try
        {
            await _service.AddAsync(dialog.Result);

            await LoadSuppliersAsync();

            MessageBox.Show(
                "Thêm nhà cung cấp thành công.",
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError("Không thể thêm nhà cung cấp.", ex);
        }
    }

    // =========================
    // SỬA NHÀ CUNG CẤP
    // =========================

    private async Task EditSupplierAsync()
    {
        if (dgvSupplier.CurrentRow?.DataBoundItem
            is not SupplierRepository.SupplierRow selected)
        {
            MessageBox.Show(
                "Vui lòng chọn nhà cung cấp cần sửa.",
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        using var dialog = new SupplierEditDialog(selected);

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        try
        {
            await _service.UpdateAsync(dialog.Result);

            await LoadSuppliersAsync();

            MessageBox.Show(
                "Cập nhật nhà cung cấp thành công.",
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError("Không thể cập nhật nhà cung cấp.", ex);
        }
    }

    // =========================
    // NGỪNG HOẠT ĐỘNG / KÍCH HOẠT
    // =========================

    private async Task ToggleStatusAsync()
    {
        if (dgvSupplier.CurrentRow?.DataBoundItem
            is not SupplierRepository.SupplierRow selected)
        {
            MessageBox.Show(
                "Vui lòng chọn nhà cung cấp.",
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        bool newStatus = !selected.IsActive;

        string action = newStatus
            ? "kích hoạt lại"
            : "ngừng hoạt động";

        var confirm = MessageBox.Show(
            $"Bạn có chắc muốn {action} nhà cung cấp '{selected.SupplierName}'?",
            "Xác nhận",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            await _service.SetActiveAsync(
                selected.SupplierId,
                newStatus);

            await LoadSuppliersAsync();

            MessageBox.Show(
                newStatus
                    ? "Đã kích hoạt nhà cung cấp."
                    : "Đã ngừng hoạt động nhà cung cấp.",
                "Thông báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError("Không thể thay đổi trạng thái.", ex);
        }
    }

    // =========================
    // HỖ TRỢ GIAO DIỆN
    // =========================

    private void SetLoading(bool loading)
    {
        btnSearch.Enabled = !loading;
        btnRefresh.Enabled = !loading;
        btnAdd.Enabled = !loading;
        btnEdit.Enabled = !loading;
        btnToggleStatus.Enabled = !loading;

        Cursor = loading ? Cursors.WaitCursor : Cursors.Default;
    }

    private static void ConfigureButton(
        Button button,
        string text,
        Color background)
    {
        button.Text = text;
        button.Width = 135;
        button.Height = 34;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = background;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(5, 0, 5, 0);
    }

    private static Label MakeCaption(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(65, 75, 90),
            Padding = new Padding(0, 8, 0, 0),
            Margin = new Padding(5, 0, 5, 0)
        };
    }

    private static void ShowError(string message, Exception ex)
    {
        MessageBox.Show(
            $"{message}\n\nChi tiết: {ex.Message}",
            "Lỗi",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    // =========================
    // FORM THÊM / SỬA
    // =========================

    private sealed class SupplierEditDialog : Form
    {
        private readonly TextBox txtId = new();
        private readonly TextBox txtName = new();
        private readonly TextBox txtPhone = new();
        private readonly TextBox txtEmail = new();
        private readonly TextBox txtTaxCode = new();
        private readonly TextBox txtAddress = new();

        private readonly bool _isEdit;

        public SupplierRepository.SupplierRow Result { get; private set; }
            = new();

        public SupplierEditDialog(
            SupplierRepository.SupplierRow? supplier = null)
        {
            _isEdit = supplier != null;

            Text = _isEdit
                ? "Cập nhật nhà cung cấp"
                : "Thêm nhà cung cấp";

            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            ClientSize = new Size(520, 430);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            InitializeDialog(supplier);
        }

        private void InitializeDialog(
            SupplierRepository.SupplierRow? supplier)
        {
            var title = new Label
            {
                Text = _isEdit
                    ? "THÔNG TIN NHÀ CUNG CẤP"
                    : "THÊM NHÀ CUNG CẤP MỚI",

                Dock = DockStyle.Top,
                Height = 55,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 75, 115)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(25, 10, 25, 10),
                BackColor = Color.White
            };

            table.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 140));

            table.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            for (int i = 0; i < 6; i++)
            {
                table.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, 48));
            }

            AddField(table, 0, "Mã nhà cung cấp *", txtId);
            AddField(table, 1, "Tên nhà cung cấp *", txtName);
            AddField(table, 2, "Số điện thoại", txtPhone);
            AddField(table, 3, "Email", txtEmail);
            AddField(table, 4, "Mã số thuế", txtTaxCode);
            AddField(table, 5, "Địa chỉ", txtAddress);

            txtId.MaxLength = 20;
            txtName.MaxLength = 200;
            txtPhone.MaxLength = 20;
            txtEmail.MaxLength = 150;
            txtTaxCode.MaxLength = 30;
            txtAddress.MaxLength = 300;

            if (supplier != null)
            {
                txtId.Text = supplier.SupplierId;
                txtName.Text = supplier.SupplierName;
                txtPhone.Text = supplier.Phone ?? "";
                txtEmail.Text = supplier.Email ?? "";
                txtTaxCode.Text = supplier.TaxCode ?? "";
                txtAddress.Text = supplier.Address ?? "";
            }

            txtId.ReadOnly = _isEdit;
            txtId.BackColor = _isEdit
                ? Color.FromArgb(240, 243, 247)
                : Color.White;

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10, 10, 20, 5),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            var btnSave = new Button
            {
                Text = "Lưu thông tin",
                Width = 120,
                Height = 35,
                BackColor = Color.FromArgb(30, 145, 95),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            btnSave.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Hủy",
                Width = 90,
                Height = 35,
                DialogResult = DialogResult.Cancel
            };

            btnSave.Click += (_, _) => SaveAndClose();

            footer.Controls.Add(btnSave);
            footer.Controls.Add(btnCancel);

            Controls.Add(table);
            Controls.Add(footer);
            Controls.Add(title);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private static void AddField(
            TableLayoutPanel table,
            int row,
            string caption,
            TextBox input)
        {
            var label = new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 70, 85)
            };

            input.Dock = DockStyle.Fill;
            input.Font = new Font("Segoe UI", 10F);
            input.Margin = new Padding(3, 7, 3, 7);

            table.Controls.Add(label, 0, row);
            table.Controls.Add(input, 1, row);
        }

        private void SaveAndClose()
        {
            string id = txtId.Text.Trim();
            string name = txtName.Text.Trim();

            if (string.IsNullOrWhiteSpace(id))
            {
                MessageBox.Show(
                    "Vui lòng nhập mã nhà cung cấp.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtId.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    "Vui lòng nhập tên nhà cung cấp.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtName.Focus();
                return;
            }

            string email = txtEmail.Text.Trim();

            if (!string.IsNullOrWhiteSpace(email) &&
                (!email.Contains('@') || !email.Contains('.')))
            {
                MessageBox.Show(
                    "Email không hợp lệ.",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtEmail.Focus();
                return;
            }

            Result = new SupplierRepository.SupplierRow
            {
                SupplierId = id,
                SupplierName = name,
                Phone = EmptyToNull(txtPhone.Text),
                Email = EmptyToNull(email),
                TaxCode = EmptyToNull(txtTaxCode.Text),
                Address = EmptyToNull(txtAddress.Text),
                IsActive = true
            };

            DialogResult = DialogResult.OK;
            Close();
        }

        private static string? EmptyToNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
