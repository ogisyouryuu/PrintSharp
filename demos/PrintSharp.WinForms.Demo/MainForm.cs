using PrintSharp.Excel;
using PrintSharp.Pdf;
using PrintSharp.Windows;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Printing;

namespace PrintSharp.WinForms.Demo;

public sealed class MainForm : Form
{
    private readonly BindingSource _source = new();
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly PrintPreviewControl _preview = new() { Dock = DockStyle.Fill, AutoZoom = true, UseAntiAlias = true };
    private readonly ComboBox _report = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Label _status = new() { AutoSize = true, Padding = new Padding(8) };
    private readonly NumericUpDown _page = new() { Minimum = 1, Maximum = 1, Width = 60 };
    private PrintDocument? _printDocument;

    public MainForm()
    {
        Text = "PrintSharp · WinForms Demo";
        Width = 1450;
        Height = 950;
        MinimumSize = new Size(1000, 650);
        Font = new Font("Microsoft YaHei UI", 9);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        _report.Items.AddRange(["1. 代码生成宿泊列表", "2. Excel 模板套用列表", "3. 日本酒店领收书（模板）", "4. 日本酒店请求书（模板）", "5. DataSet 数据绑定"]);
        _report.SelectedIndex = 0;
        tools.Controls.Add(_report);
        AddButton(tools, "导出 Excel", () => Export(false));
        AddButton(tools, "导出 PDF", () => Export(true));
        AddButton(tools, "打印（生成预览）", GeneratePreview);
        AddButton(tools, "重置 100 行", ResetData);
        AddButton(tools, "打开模板目录", () => { TemplateFactory.EnsureTemplates(); Process.Start(new ProcessStartInfo(TemplateFactory.DirectoryPath) { UseShellExecute = true }); });
        tools.Controls.Add(new Label { Text = "预览页", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        tools.Controls.Add(_page);
        _page.ValueChanged += (_, _) => _preview.StartPage = (int)_page.Value - 1;
        _report.SelectedIndexChanged += (_, _) => ClearPreview();
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, Size = new Size(1400, 800), SplitterDistance = 650 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_preview);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        footer.Controls.Add(_status);
        Controls.Add(split);
        Controls.Add(footer);
        Controls.Add(tools);
        AddColumn(nameof(StayRecord.Number), "编号");
        AddColumn(nameof(StayRecord.Guest), "宿泊者");
        AddColumn(nameof(StayRecord.Room), "客室");
        AddColumn(nameof(StayRecord.Date), "宿泊日", "yyyy/MM/dd");
        AddColumn(nameof(StayRecord.Nights), "泊数");
        AddColumn(nameof(StayRecord.UnitPrice), "单价", "N0");
        AddColumn(nameof(StayRecord.Amount), "金额", "N0", true);
        _grid.DataSource = _source;
        _grid.DataError += (_, e) => { e.ThrowException = false; _status.Text = "输入格式不正确，请检查日期或数值。"; };
        _grid.CellValueChanged += (_, _) => { _grid.Invalidate(); ClearPreview(); };
        ResetData();
    }

    private void AddColumn(string property, string title, string? format = null, bool readOnly = false)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            HeaderText = title,
            ReadOnly = readOnly,
            DefaultCellStyle = new DataGridViewCellStyle { Format = format ?? "" }
        });
    }

    private void AddButton(FlowLayoutPanel panel, string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        panel.Controls.Add(button);
    }

    private void ResetData()
    {
        _source.DataSource = new BindingList<StayRecord>(StayRecord.CreateSamples());
        ClearPreview();
    }

    private PrintSharp.Documents.Document BuildDocument()
    {
        if (!_grid.EndEdit()) throw new InvalidOperationException("请先修正表格中的输入。");
        _source.EndEdit();
        var records = _grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => (StayRecord)r.DataBoundItem!).ToArray();
        if (records.Any(x => x.Nights <= 0 || x.UnitPrice < 0 || string.IsNullOrWhiteSpace(x.Guest)))
            throw new InvalidOperationException("宿泊者不能为空，泊数须大于 0，单价不能为负数。");
        return ReportService.Build((ReportKind)_report.SelectedIndex, records);
    }

    private void Export(bool pdf)
    {
        var document = BuildDocument();
        using var dialog = new SaveFileDialog
        {
            Filter = pdf ? "PDF 文件|*.pdf" : "Excel 工作簿|*.xlsx",
            FileName = $"{(ReportKind)_report.SelectedIndex}-{DateTime.Now:yyyyMMdd-HHmmss}.{(pdf ? "pdf" : "xlsx")}"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (pdf) document.SaveAsPdf(dialog.FileName);
        else document.SaveAsExcel(dialog.FileName);
        _status.Text = $"已导出 {document.Pages.Count} 页：{dialog.FileName}";
    }

    private void GeneratePreview()
    {
        var document = BuildDocument();
        var next = document.ToPrintDocument();
        if (!next.PrinterSettings.IsValid)
        {
            next.Dispose();
            throw new InvalidOperationException("Windows 打印预览需要可用的打印机驱动，请安装或启用 Microsoft Print to PDF。");
        }
        next.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        next.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        ClearPreview();
        _printDocument = next;
        _preview.Document = next;
        _page.Maximum = document.Pages.Count;
        _page.Value = 1;
        _preview.InvalidatePreview();
        _status.Text = $"已生成 {document.Pages.Count} 页预览；打印按钮仅预览。";
    }

    private void ClearPreview()
    {
        _preview.Document = null;
        _printDocument?.Dispose();
        _printDocument = null;
        _page.Value = 1;
        _page.Maximum = 1;
        _status.Text = "可编辑左侧 100 行数据，选择示例后导出或生成右侧预览。";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _preview.Document = null;
            _printDocument?.Dispose();
            _source.Dispose();
        }
        base.Dispose(disposing);
    }
}

