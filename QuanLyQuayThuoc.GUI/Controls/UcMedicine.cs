using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyQuayThuoc.DAL.Repositories;
using QuanLyQuayThuoc.BLL.Services;

namespace QuanLyQuayThuoc.GUI.Controls
{
    /// <summary>
    /// Giao diện quản lý thuốc: tìm kiếm, thêm, sửa và đổi trạng thái.
    /// Danh mục thuốc được tải bất đồng bộ một lần khi UserControl khởi tạo.
    /// </summary>
    public sealed class UcMedicine : UserControl
    {
        private readonly MedicineRepository _repository = new();
        private readonly MedicineService _service;
        private readonly string _changedBy;
        private List<MedicineRepository.CategoryRow> _categories = new();
        private bool _initialDataLoaded;
        private bool _busy;

        private readonly TextBox txtSearch = new();
        private readonly ComboBox cboCategory = new();
        private readonly ComboBox cboStatus = new();
        private readonly DataGridView dgvMedicine = new();
        private readonly Button btnSearch = new();
        private readonly Button btnAdd = new();
        private readonly Button btnEdit = new();
        private readonly Button btnToggleStatus = new();
        private readonly Label lblCount = new();

        public UcMedicine(string changedBy = "")
        {
            _changedBy = string.IsNullOrWhiteSpace(changedBy) ? "SYSTEM" : changedBy.Trim();
            _service = new MedicineService(_repository);

            InitializeLayout();
            SetActionButtonsEnabled(false);
            Load += async (_, _) => await LoadInitialDataAsync();
        }

        private void InitializeLayout()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(245, 247, 250);
            Padding = new Padding(18);

            var title = new Label
            {
                Text = "QUẢN LÝ THUỐC",
                Dock = DockStyle.Top,
                Height = 48,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 55, 80),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 82,
                WrapContents = true,
                Padding = new Padding(0, 8, 0, 4),
                BackColor = Color.White
            };

            txtSearch.Width = 230;
            txtSearch.Font = new Font("Segoe UI", 10);
            txtSearch.PlaceholderText = "Tên thuốc, mã thuốc, hoạt chất, barcode...";

            cboCategory.Width = 175;
            cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCategory.Font = new Font("Segoe UI", 10);

            cboStatus.Width = 150;
            cboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStatus.Font = new Font("Segoe UI", 10);
            cboStatus.Items.AddRange(new object[]
            {
                "Tất cả trạng thái", "Đang kinh doanh", "Ngừng kinh doanh"
            });
            cboStatus.SelectedIndex = 0;

            ConfigureButton(btnSearch, "Tìm kiếm", Color.FromArgb(35, 112, 190));
            btnSearch.Click += async (_, _) => await SearchAsync();

            filterPanel.Controls.Add(MakeCaption("Từ khóa"));
            filterPanel.Controls.Add(txtSearch);
            filterPanel.Controls.Add(MakeCaption("Nhóm thuốc"));
            filterPanel.Controls.Add(cboCategory);
            filterPanel.Controls.Add(MakeCaption("Trạng thái"));
            filterPanel.Controls.Add(cboStatus);
            filterPanel.Controls.Add(btnSearch);

            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(0, 7, 0, 5),
                BackColor = Color.FromArgb(245, 247, 250)
            };

            ConfigureButton(btnAdd, "+ Thêm thuốc", Color.FromArgb(30, 145, 95));
            ConfigureButton(btnEdit, "Sửa thông tin", Color.FromArgb(230, 150, 40));
            ConfigureButton(btnToggleStatus, "Đổi trạng thái", Color.FromArgb(100, 110, 125));

            btnAdd.Click += async (_, _) => await AddMedicineAsync();
            btnEdit.Click += async (_, _) => await EditMedicineAsync();
            btnToggleStatus.Click += async (_, _) => await ToggleStatusAsync();

            lblCount.AutoSize = true;
            lblCount.Font = new Font("Segoe UI", 9);
            lblCount.ForeColor = Color.DimGray;
            lblCount.Padding = new Padding(12, 10, 0, 0);

            actionPanel.Controls.AddRange(new Control[]
            {
                btnAdd, btnEdit, btnToggleStatus, lblCount
            });

            dgvMedicine.Dock = DockStyle.Fill;
            dgvMedicine.BackgroundColor = Color.White;
            dgvMedicine.BorderStyle = BorderStyle.None;
            dgvMedicine.AutoGenerateColumns = false;
            dgvMedicine.AllowUserToAddRows = false;
            dgvMedicine.AllowUserToDeleteRows = false;
            dgvMedicine.ReadOnly = true;
            dgvMedicine.MultiSelect = false;
            dgvMedicine.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMedicine.RowHeadersVisible = false;
            dgvMedicine.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvMedicine.EnableHeadersVisualStyles = false;
            dgvMedicine.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(231, 238, 246);
            dgvMedicine.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 55, 75);
            dgvMedicine.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            dgvMedicine.DefaultCellStyle.Font = new Font("Segoe UI", 9);
            dgvMedicine.RowTemplate.Height = 34;

            dgvMedicine.Columns.Add(TextColumn("MedicineId", "Mã thuốc", 90));
            dgvMedicine.Columns.Add(TextColumn("MedicineName", "Tên thuốc", 180));
            dgvMedicine.Columns.Add(TextColumn("CategoryName", "Nhóm thuốc", 120));
            dgvMedicine.Columns.Add(TextColumn("ActiveIngredient", "Hoạt chất", 130));
            dgvMedicine.Columns.Add(TextColumn("Strength", "Hàm lượng", 85));
            dgvMedicine.Columns.Add(TextColumn("DosageForm", "Dạng bào chế", 95));
            dgvMedicine.Columns.Add(TextColumn("BaseUnit", "ĐVT cơ bản", 75));
            dgvMedicine.Columns.Add(TextColumn("BaseSalePrice", "Giá bán", 90, "N0"));
            dgvMedicine.Columns.Add(TextColumn("MinimumStock", "Tồn tối thiểu", 90, "N0"));
            dgvMedicine.Columns.Add(TextColumn("BusinessStatus", "Trạng thái", 100));

            Controls.Add(dgvMedicine);
            Controls.Add(actionPanel);
            Controls.Add(filterPanel);
            Controls.Add(title);
        }

        private static Label MakeCaption(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(65, 75, 90),
            Padding = new Padding(8, 10, 2, 0)
        };

        private static DataGridViewTextBoxColumn TextColumn(
            string property, string header, int minWidth, string? format = null)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = property,
                HeaderText = header,
                MinimumWidth = minWidth,
                FillWeight = minWidth,
                SortMode = DataGridViewColumnSortMode.Automatic,
                DefaultCellStyle = { Format = format ?? "" }
            };
        }

        private static void ConfigureButton(Button button, string text, Color color)
        {
            button.Text = text;
            button.Width = 125;
            button.Height = 34;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(5, 3, 5, 3);
        }

        private void SetActionButtonsEnabled(bool enabled)
        {
            btnAdd.Enabled = enabled;
            btnEdit.Enabled = enabled;
            btnToggleStatus.Enabled = enabled;
            btnSearch.Enabled = enabled;
        }

        private async Task LoadInitialDataAsync()
        {
            if (_initialDataLoaded || IsDisposed) return;

            try
            {
                UseWaitCursor = true;

                // Tải danh mục một lần. Dialog chỉ dùng dữ liệu đã tải,
                // tuyệt đối không gọi .Result/.Wait() trong constructor.
                _categories = await _repository.GetCategoriesAsync();

                cboCategory.Items.Clear();
                cboCategory.Items.Add(new CategoryChoice(null, "Tất cả nhóm thuốc"));

                foreach (var category in _categories)
                    cboCategory.Items.Add(
                        new CategoryChoice(category.CategoryId, category.CategoryName));

                cboCategory.SelectedIndex = 0;
                _initialDataLoaded = true;
                SetActionButtonsEnabled(true);

                await SearchAsync();
            }
            catch (Exception ex)
            {
                ShowError("Không thể tải dữ liệu thuốc và nhóm thuốc.", ex);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private async Task SearchAsync()
        {
            if (_busy || !_initialDataLoaded) return;

            try
            {
                string? keyword = string.IsNullOrWhiteSpace(txtSearch.Text)
                    ? null
                    : txtSearch.Text.Trim();

                var category = cboCategory.SelectedItem as CategoryChoice;
                bool includeInactive = cboStatus.SelectedIndex != 1;

                var rows = await _repository.SearchAsync(keyword, includeInactive);
                var filtered = rows.AsEnumerable();

                if (category?.Id != null)
                    filtered = filtered.Where(x => x.CategoryId == category.Id);

                if (cboStatus.SelectedIndex == 1)
                    filtered = filtered.Where(x =>
                        string.Equals(x.BusinessStatus, "ACTIVE",
                            StringComparison.OrdinalIgnoreCase));
                else if (cboStatus.SelectedIndex == 2)
                    filtered = filtered.Where(x =>
                        string.Equals(x.BusinessStatus, "INACTIVE",
                            StringComparison.OrdinalIgnoreCase));

                var result = filtered.ToList();
                dgvMedicine.DataSource = null;
                dgvMedicine.DataSource = result;
                lblCount.Text = $"Có {result.Count} thuốc";
            }
            catch (Exception ex)
            {
                ShowError("Không thể tìm kiếm thuốc.", ex);
            }
        }

        private async Task AddMedicineAsync()
        {
            if (!_initialDataLoaded || _busy) return;

            // Truyền danh mục đã tải sẵn vào dialog; không truy vấn SQL
            // đồng bộ trong lúc khởi tạo Form.
            using var dialog = new MedicineEditDialog(_categories);

            if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            try
            {
                SetBusy(true);
                var m = dialog.Result;

                var model = new MedicineSaveModel
                {
                    MedicineName = m.MedicineName,
                    CategoryId = m.CategoryId,
                    ActiveIngredient = m.ActiveIngredient,
                    Strength = m.Strength,
                    DosageForm = m.DosageForm,
                    BaseUnit = m.BaseUnit,
                    MinimumStock = m.MinimumStock,
                    RequiresPrescription = m.RequiresPrescription,
                    Barcode = m.Barcode,
                    Description = m.Description,
                    SalePrice = m.SalePrice
                };

                await _service.SaveAsync(model, _changedBy);
                await SearchAsync();

                MessageBox.Show("Thêm thuốc thành công.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError("Không thể thêm thuốc.", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task EditMedicineAsync()
        {
            if (!_initialDataLoaded || _busy) return;

            if (dgvMedicine.CurrentRow?.DataBoundItem is not MedicineRepository.MedicineRow selected)
            {
                MessageBox.Show("Vui lòng chọn thuốc cần sửa.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new MedicineEditDialog(_categories, selected);

            if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            try
            {
                SetBusy(true);
                var m = dialog.Result;

                var model = new MedicineSaveModel
                {
                    MedicineId = selected.MedicineId,
                    MedicineName = m.MedicineName,
                    CategoryId = m.CategoryId,
                    ActiveIngredient = m.ActiveIngredient,
                    Strength = m.Strength,
                    DosageForm = m.DosageForm,
                    BaseUnit = m.BaseUnit,
                    MinimumStock = m.MinimumStock,
                    RequiresPrescription = m.RequiresPrescription,
                    Barcode = m.Barcode,
                    Description = m.Description,
                    SalePrice = m.SalePrice
                };

                await _service.SaveAsync(model, _changedBy);
                await SearchAsync();

                MessageBox.Show("Cập nhật thuốc thành công.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError("Không thể cập nhật thuốc.", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ToggleStatusAsync()
        {
            if (!_initialDataLoaded || _busy) return;

            if (dgvMedicine.CurrentRow?.DataBoundItem is not MedicineRepository.MedicineRow selected)
            {
                MessageBox.Show("Vui lòng chọn thuốc cần đổi trạng thái.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool currentlyActive = string.Equals(selected.BusinessStatus, "ACTIVE",
                StringComparison.OrdinalIgnoreCase);
            string action = currentlyActive ? "ngừng kinh doanh" : "mở kinh doanh";

            if (MessageBox.Show(
                $"Bạn có chắc muốn {action} thuốc '{selected.MedicineName}'?",
                "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes)
                return;

            try
            {
                SetBusy(true);
                await _service.SetStatusAsync(
                    selected.MedicineId,
                    currentlyActive ? "INACTIVE" : "ACTIVE");

                await SearchAsync();
            }
            catch (Exception ex)
            {
                ShowError("Không thể đổi trạng thái thuốc.", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            UseWaitCursor = busy;
            SetActionButtonsEnabled(!busy && _initialDataLoaded);
        }

        private static void ShowError(string message, Exception ex)
        {
            MessageBox.Show($"{message}\n\nChi tiết: {ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private sealed record CategoryChoice(string? Id, string Name)
        {
            public override string ToString() => Name;
        }

        private sealed class MedicineEditDialog : Form
        {
            private readonly TextBox txtName = new();
            private readonly ComboBox cboCategory = new();
            private readonly TextBox txtIngredient = new();
            private readonly TextBox txtStrength = new();
            private readonly TextBox txtForm = new();
            private readonly TextBox txtUnit = new();
            private readonly NumericUpDown numMinimum = new();
            private readonly CheckBox chkPrescription = new();
            private readonly TextBox txtBarcode = new();
            private readonly TextBox txtDescription = new();
            private readonly NumericUpDown numPrice = new();

            public MedicineEditResult Result { get; private set; } = new();

            public MedicineEditDialog(
                IReadOnlyList<MedicineRepository.CategoryRow> categories,
                MedicineRepository.MedicineRow? medicine = null)
            {
                Text = medicine == null ? "Thêm thuốc" : "Cập nhật thuốc";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                ClientSize = new Size(540, 590);
                Font = new Font("Segoe UI", 9);
                BackColor = Color.White;

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(18),
                    ColumnCount = 2,
                    RowCount = 12,
                    AutoScroll = true
                };

                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                AddField(layout, 0, "Tên thuốc *", txtName);
                AddField(layout, 1, "Nhóm thuốc *", cboCategory);
                AddField(layout, 2, "Hoạt chất", txtIngredient);
                AddField(layout, 3, "Hàm lượng", txtStrength);
                AddField(layout, 4, "Dạng bào chế", txtForm);
                AddField(layout, 5, "Đơn vị cơ bản *", txtUnit);
                AddField(layout, 6, "Tồn tối thiểu", numMinimum);
                AddField(layout, 7, "Giá bán", numPrice);
                AddField(layout, 8, "Barcode", txtBarcode);
                AddField(layout, 9, "Mô tả", txtDescription);

                chkPrescription.Text = "Thuốc kê đơn";
                layout.Controls.Add(chkPrescription, 1, 10);

                cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
                cboCategory.Items.Add(new CategoryChoice(null, "-- Chọn nhóm thuốc --"));

                foreach (var category in categories)
                {
                    cboCategory.Items.Add(
                        new CategoryChoice(category.CategoryId, category.CategoryName));
                }

                cboCategory.SelectedIndex = 0;

                numMinimum.DecimalPlaces = 3;
                numMinimum.Maximum = 100000000;
                numPrice.DecimalPlaces = 2;
                numPrice.Maximum = 1000000000;

                txtDescription.Multiline = true;
                txtDescription.Height = 58;

                if (medicine != null)
                {
                    txtName.Text = medicine.MedicineName;
                    SelectCategory(medicine.CategoryId);
                    txtIngredient.Text = medicine.ActiveIngredient ?? "";
                    txtStrength.Text = medicine.Strength ?? "";
                    txtForm.Text = medicine.DosageForm ?? "";
                    txtUnit.Text = medicine.BaseUnit;
                    numMinimum.Value = Math.Min(numMinimum.Maximum, medicine.MinimumStock);
                    numPrice.Value = Math.Min(numPrice.Maximum, medicine.BaseSalePrice);
                    chkPrescription.Checked = medicine.RequiresPrescription;
                    txtBarcode.Text = medicine.Barcode ?? "";
                }

                var buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 52,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(8)
                };

                var btnSave = new Button
                {
                    Text = "Lưu",
                    Width = 95,
                    Height = 32,
                    DialogResult = DialogResult.None
                };

                var btnCancel = new Button
                {
                    Text = "Hủy",
                    Width = 95,
                    Height = 32,
                    DialogResult = DialogResult.Cancel
                };

                btnSave.Click += (_, _) => SaveAndClose();
                buttons.Controls.Add(btnSave);
                buttons.Controls.Add(btnCancel);

                Controls.Add(layout);
                Controls.Add(buttons);
                AcceptButton = btnSave;
                CancelButton = btnCancel;
            }

            private static void AddField(
                TableLayoutPanel panel, int row, string label, Control input)
            {
                input.Dock = DockStyle.Fill;
                input.Margin = new Padding(3, 5, 3, 5);
                input.Font = new Font("Segoe UI", 9);

                panel.RowStyles.Add(new RowStyle(
                    SizeType.Absolute,
                    input is TextBox t && t.Multiline ? 72 : 39));

                panel.Controls.Add(new Label
                {
                    Text = label,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, row);

                panel.Controls.Add(input, 1, row);
            }

            private void SelectCategory(string? categoryId)
            {
                for (int i = 0; i < cboCategory.Items.Count; i++)
                {
                    if (cboCategory.Items[i] is CategoryChoice item
                        && item.Id == categoryId)
                    {
                        cboCategory.SelectedIndex = i;
                        return;
                    }
                }
            }

            private void SaveAndClose()
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Vui lòng nhập tên thuốc.");
                    txtName.Focus();
                    return;
                }

                if (cboCategory.SelectedItem is not CategoryChoice category
                    || category.Id == null)
                {
                    MessageBox.Show("Vui lòng chọn nhóm thuốc.");
                    cboCategory.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtUnit.Text))
                {
                    MessageBox.Show("Vui lòng nhập đơn vị cơ bản.");
                    txtUnit.Focus();
                    return;
                }

                Result = new MedicineEditResult
                {
                    MedicineName = txtName.Text.Trim(),
                    CategoryId = category.Id,
                    ActiveIngredient = NullIfEmpty(txtIngredient.Text),
                    Strength = NullIfEmpty(txtStrength.Text),
                    DosageForm = NullIfEmpty(txtForm.Text),
                    BaseUnit = txtUnit.Text.Trim(),
                    MinimumStock = numMinimum.Value,
                    RequiresPrescription = chkPrescription.Checked,
                    Barcode = NullIfEmpty(txtBarcode.Text),
                    Description = NullIfEmpty(txtDescription.Text),
                    SalePrice = numPrice.Value
                };

                DialogResult = DialogResult.OK;
                Close();
            }

            private static string? NullIfEmpty(string value) =>
                string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private sealed class MedicineEditResult
        {
            public string MedicineName { get; set; } = "";
            public string CategoryId { get; set; } = "";
            public string? ActiveIngredient { get; set; }
            public string? Strength { get; set; }
            public string? DosageForm { get; set; }
            public string BaseUnit { get; set; } = "";
            public decimal MinimumStock { get; set; }
            public bool RequiresPrescription { get; set; }
            public string? Barcode { get; set; }
            public string? Description { get; set; }
            public decimal SalePrice { get; set; }
        }
    }
}