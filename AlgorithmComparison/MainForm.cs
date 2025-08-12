using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AlgorithmComparison.Utilities;
using AnythingToGif.ColorDistanceMetrics;
using AnythingToGif.Ditherers;
using AnythingToGif.Quantizers;
using AnythingToGif.Quantizers.Wrappers;

namespace AlgorithmComparison;

/// <summary>
/// Extension methods for performance optimizations
/// </summary>
public static class DataGridViewExtensions {
  public static void DoubleBuffered(this DataGridView dgv, bool setting) {
    Type dgvType = dgv.GetType();
    PropertyInfo? pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
    pi?.SetValue(dgv, setting, null);
  }
}

public partial class MainForm : Form {
  
  public class ComparisonResult {
    public string Quantizer { get; set; } = string.Empty;
    public string Ditherer { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double PSNR { get; set; }
    public double SSIM { get; set; }
    public double SNR { get; set; }
    public double EdgePreservation { get; set; }
    public double Contrast { get; set; }
    public int ColorCount { get; set; }
    public int UniqueColors { get; set; }
    public int HistogramBins { get; set; }
    public double HistogramEntropy { get; set; }
    public double ColorSpread { get; set; }
    public double ColorUniformity { get; set; }
    public double HistogramDifference { get; set; }
    public double ExecutionTime_ms { get; set; }
    public double PixelsPerSecond { get; set; }
    public string Status { get; set; } = "Ready";
  }

  private DataGridView _resultsGrid;
  private Button _runTestsButton;
  private Button _loadImageButton;
  private Button _exportButton;
  private PictureBox _originalPictureBox;
  private PictureBox _processedPictureBox;
  private ProgressBar _progressBar;
  private Label _statusLabel;
  private NumericUpDown _paletteSizeNumeric;
  private NumericUpDown _imageSizeNumeric;
  private ComboBox _testPatternCombo;
  
  // Algorithm selection controls
  private CheckedListBox _quantizersList;
  private CheckedListBox _ditherersList;
  private CheckedListBox _metricsList;
  
  private Bitmap? _testImage;
  private readonly BindingList<ComparisonResult> _results = new();
  private readonly DataTable _dataTable = new();
  private bool _dataTableInitialized = false;
  private readonly BackgroundWorker _backgroundWorker = new();

  public MainForm() {
    InitializeComponent();
    SetupDataGrid();
    SetupBackgroundWorker();
    LoadDefaultTestImage();
  }

  private void InitializeComponent() {
    Text = "Algorithm Comparison Tool";
    Size = new Size(1600, 900);
    StartPosition = FormStartPosition.CenterScreen;

    var mainPanel = new TableLayoutPanel {
      Dock = DockStyle.Fill,
      ColumnCount = 4,
      RowCount = 4
    };

    // Configure column widths
    mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250)); // Controls
    mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300)); // Algorithm selection
    mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));   // Results grid
    mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));   // Info/Images

    // Configure row heights
    mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); // Controls
    mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));   // Results grid
    mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));   // Images
    mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Status bar

    // Controls Panel
    var controlsPanel = CreateControlsPanel();
    mainPanel.Controls.Add(controlsPanel, 0, 0);
    mainPanel.SetRowSpan(controlsPanel, 3);

    // Algorithm Selection Panel
    var algorithmPanel = CreateAlgorithmSelectionPanel();
    mainPanel.Controls.Add(algorithmPanel, 1, 0);
    mainPanel.SetRowSpan(algorithmPanel, 3);

    // Results Grid with performance optimizations
    _resultsGrid = new DataGridView {
      Dock = DockStyle.Fill,
      AllowUserToAddRows = false,
      ReadOnly = true,
      SelectionMode = DataGridViewSelectionMode.FullRowSelect,
      AllowUserToResizeColumns = true,
      AllowUserToResizeRows = false,
      ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
      RowHeadersVisible = false,
      CellBorderStyle = DataGridViewCellBorderStyle.Single,
      BackgroundColor = SystemColors.Control
    };
    mainPanel.Controls.Add(_resultsGrid, 2, 0);
    mainPanel.SetRowSpan(_resultsGrid, 2);

    // Images Panel
    var imagesPanel = CreateImagesPanel();
    mainPanel.Controls.Add(imagesPanel, 2, 2);

    // Algorithm Info Panel
    var infoPanel = CreateInfoPanel();
    mainPanel.Controls.Add(infoPanel, 3, 0);
    mainPanel.SetRowSpan(infoPanel, 3);

    // Status Panel
    var statusPanel = new Panel { Dock = DockStyle.Fill };
    _progressBar = new ProgressBar { Dock = DockStyle.Fill };
    _statusLabel = new Label { 
      Text = "Ready", 
      Dock = DockStyle.Right, 
      AutoSize = true 
    };
    statusPanel.Controls.Add(_progressBar);
    statusPanel.Controls.Add(_statusLabel);
    mainPanel.Controls.Add(statusPanel, 0, 3);
    mainPanel.SetColumnSpan(statusPanel, 4);

    Controls.Add(mainPanel);
  }

  private Panel CreateControlsPanel() {
    var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

    var layout = new TableLayoutPanel {
      Dock = DockStyle.Fill,
      ColumnCount = 2,
      RowCount = 8
    };

    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

    // Load Image Button
    _loadImageButton = new Button { 
      Text = "Load Image", 
      Dock = DockStyle.Fill, 
      Height = 30 
    };
    _loadImageButton.Click += LoadImageButton_Click;
    layout.Controls.Add(_loadImageButton, 0, 0);
    layout.SetColumnSpan(_loadImageButton, 2);

    // Test Pattern
    layout.Controls.Add(new Label { Text = "Test Pattern:", Dock = DockStyle.Fill }, 0, 1);
    _testPatternCombo = new ComboBox {
      Dock = DockStyle.Fill,
      DropDownStyle = ComboBoxStyle.DropDownList
    };
    _testPatternCombo.Items.AddRange(new[] { "Gradient", "Photo-like", "Geometric", "Noise", "Grayscale", "Black & White" });
    _testPatternCombo.SelectedIndex = 1;
    _testPatternCombo.SelectedIndexChanged += TestPatternCombo_SelectedIndexChanged;
    layout.Controls.Add(_testPatternCombo, 1, 1);

    // Image Size
    layout.Controls.Add(new Label { Text = "Image Size:", Dock = DockStyle.Fill }, 0, 2);
    _imageSizeNumeric = new NumericUpDown {
      Minimum = 32,
      Maximum = 512,
      Value = 128,
      Dock = DockStyle.Fill
    };
    _imageSizeNumeric.ValueChanged += ImageSizeNumeric_ValueChanged;
    layout.Controls.Add(_imageSizeNumeric, 1, 2);

    // Palette Size
    layout.Controls.Add(new Label { Text = "Palette Size:", Dock = DockStyle.Fill }, 0, 3);
    _paletteSizeNumeric = new NumericUpDown {
      Minimum = 4,
      Maximum = 256,
      Value = 16,
      Dock = DockStyle.Fill
    };
    layout.Controls.Add(_paletteSizeNumeric, 1, 3);

    // Run Tests Button
    _runTestsButton = new Button { 
      Text = "Run Comparison", 
      Dock = DockStyle.Fill, 
      Height = 40,
      BackColor = Color.LightGreen
    };
    _runTestsButton.Click += RunTestsButton_Click;
    layout.Controls.Add(_runTestsButton, 0, 5);
    layout.SetColumnSpan(_runTestsButton, 2);

    // Export Button
    _exportButton = new Button { 
      Text = "Export CSV", 
      Dock = DockStyle.Fill, 
      Height = 30
    };
    _exportButton.Click += ExportButton_Click;
    layout.Controls.Add(_exportButton, 0, 6);
    layout.SetColumnSpan(_exportButton, 2);

    panel.Controls.Add(layout);
    return panel;
  }

  private Panel CreateAlgorithmSelectionPanel() {
    var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
    
    var layout = new TableLayoutPanel {
      Dock = DockStyle.Fill,
      ColumnCount = 1,
      RowCount = 6
    };

    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Header
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33));  // Quantizers
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Header
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33));  // Ditherers
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Header
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));  // Color metrics

    // Quantizers section
    var quantizersLabel = new Label { 
      Text = "Quantizers:", 
      Dock = DockStyle.Fill,
      Font = new Font("Segoe UI", 9, FontStyle.Bold)
    };
    layout.Controls.Add(quantizersLabel, 0, 0);

    _quantizersList = new CheckedListBox { 
      Dock = DockStyle.Fill,
      CheckOnClick = true
    };
    PopulateQuantizersList();
    layout.Controls.Add(_quantizersList, 0, 1);

    // Ditherers section
    var ditherersLabel = new Label { 
      Text = "Ditherers:", 
      Dock = DockStyle.Fill,
      Font = new Font("Segoe UI", 9, FontStyle.Bold)
    };
    layout.Controls.Add(ditherersLabel, 0, 2);

    _ditherersList = new CheckedListBox { 
      Dock = DockStyle.Fill,
      CheckOnClick = true
    };
    PopulateDitherersList();
    layout.Controls.Add(_ditherersList, 0, 3);

    // Color Metrics section
    var metricsLabel = new Label { 
      Text = "Color Distance Metrics:", 
      Dock = DockStyle.Fill,
      Font = new Font("Segoe UI", 9, FontStyle.Bold)
    };
    layout.Controls.Add(metricsLabel, 0, 4);

    _metricsList = new CheckedListBox { 
      Dock = DockStyle.Fill,
      CheckOnClick = true
    };
    PopulateColorMetricsList();
    layout.Controls.Add(_metricsList, 0, 5);

    panel.Controls.Add(layout);
    return panel;
  }

  private Panel CreateImagesPanel() {
    var panel = new Panel { Dock = DockStyle.Fill };
    var layout = new TableLayoutPanel {
      Dock = DockStyle.Fill,
      ColumnCount = 2,
      RowCount = 2
    };

    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

    layout.Controls.Add(new Label { Text = "Original", Dock = DockStyle.Fill }, 0, 0);
    layout.Controls.Add(new Label { Text = "Processed", Dock = DockStyle.Fill }, 1, 0);

    _originalPictureBox = new PictureBox {
      Dock = DockStyle.Fill,
      SizeMode = PictureBoxSizeMode.Zoom,
      BorderStyle = BorderStyle.FixedSingle
    };
    layout.Controls.Add(_originalPictureBox, 0, 1);

    _processedPictureBox = new PictureBox {
      Dock = DockStyle.Fill,
      SizeMode = PictureBoxSizeMode.Zoom,
      BorderStyle = BorderStyle.FixedSingle
    };
    layout.Controls.Add(_processedPictureBox, 1, 1);

    panel.Controls.Add(layout);
    return panel;
  }

  private Panel CreateInfoPanel() {
    var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
    
    var infoText = new RichTextBox {
      Dock = DockStyle.Fill,
      ReadOnly = true,
      Text = @"Algorithm Comparison Tool

This tool tests all combinations of:
• Quantizers (color reduction)
• Ditherers (error diffusion)
• Color Distance Metrics

Quality Metrics:
• PSNR - Peak Signal-to-Noise Ratio (dB)
• SSIM - Structural Similarity Index
• SNR - Signal-to-Noise Ratio (dB)
• Edge Preservation - Edge retention ratio
• Contrast - Standard deviation of luminance
• Color Count - Unique colors in result

Performance Metrics:
• Execution Time - Processing time (ms)
• Pixels/Sec - Processing speed

Usage:
1. Load an image or use test pattern
2. Set palette size and image dimensions
3. Click 'Run Comparison'
4. View results in grid
5. Select row to see processed image
6. Export results to CSV for analysis

The results will show which algorithm combinations work best for your specific image types and quality requirements."
    };

    panel.Controls.Add(infoText);
    return panel;
  }

  private void SetupDataGrid() {
    // Enable performance optimizations
    _resultsGrid.AutoGenerateColumns = true;
    _resultsGrid.AllowUserToOrderColumns = true;
    _resultsGrid.EnableHeadersVisualStyles = false;
    _resultsGrid.DoubleBuffered(true); // Extension method to enable double buffering
    
    // Initialize DataTable with proper columns for sorting
    InitializeDataTable();
    _resultsGrid.DataSource = _dataTable;
    _resultsGrid.SelectionChanged += ResultsGrid_SelectionChanged;
    
    // Format numeric columns
    _resultsGrid.DataBindingComplete += (s, e) => {
      foreach (DataGridViewColumn column in _resultsGrid.Columns) {
        // Enable sorting for all columns (DataTable automatically supports sorting)
        column.SortMode = DataGridViewColumnSortMode.Automatic;
        
        // Format all numeric columns to 2 decimal places maximum
        if (column.Name.Contains("PSNR") || column.Name.Contains("SSIM") || column.Name.Contains("SNR") ||
            column.Name.Contains("EdgePreservation") || column.Name.Contains("Contrast") ||
            column.Name.Contains("Histogram") || column.Name.Contains("Color") || column.Name.Contains("ExecutionTime")) {
          column.DefaultCellStyle.Format = "F2";
        }
        // Integer columns should have no decimal places
        if (column.Name.Contains("Count") || column.Name.Contains("Bins") || column.Name.Contains("PixelsPerSecond")) {
          column.DefaultCellStyle.Format = "F0";
        }
        
        // Auto-size columns for better readability
        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
      }
    };
  }
  
  private void InitializeDataTable() {
    if (_dataTableInitialized) return;
    
    _dataTable.Columns.Add("Quantizer", typeof(string));
    _dataTable.Columns.Add("Ditherer", typeof(string));
    _dataTable.Columns.Add("Metric", typeof(string));
    _dataTable.Columns.Add("PSNR", typeof(double));
    _dataTable.Columns.Add("SSIM", typeof(double));
    _dataTable.Columns.Add("SNR", typeof(double));
    _dataTable.Columns.Add("EdgePreservation", typeof(double));
    _dataTable.Columns.Add("Contrast", typeof(double));
    _dataTable.Columns.Add("ColorCount", typeof(int));
    _dataTable.Columns.Add("UniqueColors", typeof(int));
    _dataTable.Columns.Add("HistogramBins", typeof(int));
    _dataTable.Columns.Add("HistogramEntropy", typeof(double));
    _dataTable.Columns.Add("ColorSpread", typeof(double));
    _dataTable.Columns.Add("ColorUniformity", typeof(double));
    _dataTable.Columns.Add("HistogramDifference", typeof(double));
    _dataTable.Columns.Add("ExecutionTime_ms", typeof(double));
    _dataTable.Columns.Add("PixelsPerSecond", typeof(double));
    _dataTable.Columns.Add("Status", typeof(string));
    
    _dataTableInitialized = true;
  }
  
  private void AddResultToDataTable(ComparisonResult result) {
    var row = _dataTable.NewRow();
    row["Quantizer"] = result.Quantizer;
    row["Ditherer"] = result.Ditherer;
    row["Metric"] = result.Metric;
    row["PSNR"] = result.PSNR;
    row["SSIM"] = result.SSIM;
    row["SNR"] = result.SNR;
    row["EdgePreservation"] = result.EdgePreservation;
    row["Contrast"] = result.Contrast;
    row["ColorCount"] = result.ColorCount;
    row["UniqueColors"] = result.UniqueColors;
    row["HistogramBins"] = result.HistogramBins;
    row["HistogramEntropy"] = result.HistogramEntropy;
    row["ColorSpread"] = result.ColorSpread;
    row["ColorUniformity"] = result.ColorUniformity;
    row["HistogramDifference"] = result.HistogramDifference;
    row["ExecutionTime_ms"] = result.ExecutionTime_ms;
    row["PixelsPerSecond"] = result.PixelsPerSecond;
    row["Status"] = result.Status;
    _dataTable.Rows.Add(row);
  }
  
  private void UpdateResultInDataTable(ComparisonResult result, int resultIndex) {
    if (resultIndex >= 0 && resultIndex < _dataTable.Rows.Count) {
      var row = _dataTable.Rows[resultIndex];
      row["PSNR"] = result.PSNR;
      row["SSIM"] = result.SSIM;
      row["SNR"] = result.SNR;
      row["EdgePreservation"] = result.EdgePreservation;
      row["Contrast"] = result.Contrast;
      row["ColorCount"] = result.ColorCount;
      row["UniqueColors"] = result.UniqueColors;
      row["HistogramBins"] = result.HistogramBins;
      row["HistogramEntropy"] = result.HistogramEntropy;
      row["ColorSpread"] = result.ColorSpread;
      row["ColorUniformity"] = result.ColorUniformity;
      row["HistogramDifference"] = result.HistogramDifference;
      row["ExecutionTime_ms"] = result.ExecutionTime_ms;
      row["PixelsPerSecond"] = result.PixelsPerSecond;
      row["Status"] = result.Status;
    }
  }

  private void SetupBackgroundWorker() {
    _backgroundWorker.WorkerReportsProgress = true;
    _backgroundWorker.WorkerSupportsCancellation = true;
    _backgroundWorker.DoWork += BackgroundWorker_DoWork;
    _backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
    _backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
  }

  private void LoadDefaultTestImage() {
    _testImage = CreateTestImage();
    _originalPictureBox.Image = _testImage;
  }

  private void PopulateQuantizersList() {
    var quantizers = GetAllQuantizers();
    foreach (var quantizer in quantizers.Keys) {
      _quantizersList.Items.Add(quantizer);
    }
    
    // Check first 2 by default for better performance
    for (var i = 0; i < Math.Min(2, _quantizersList.Items.Count); i++) {
      _quantizersList.SetItemChecked(i, true);
    }
  }

  private void PopulateDitherersList() {
    var ditherers = GetAllDitherers();
    foreach (var ditherer in ditherers.Keys) {
      _ditherersList.Items.Add(ditherer);
    }
    
    // Check first 2 by default for better performance
    for (var i = 0; i < Math.Min(2, _ditherersList.Items.Count); i++) {
      _ditherersList.SetItemChecked(i, true);
    }
  }

  private void PopulateColorMetricsList() {
    var metrics = GetAllColorDistanceMetrics();
    foreach (var metric in metrics.Keys) {
      _metricsList.Items.Add(metric);
    }
    
    // Check only first metric by default for better performance
    if (_metricsList.Items.Count > 0) {
      _metricsList.SetItemChecked(0, true);
    }
  }

  private Bitmap CreateTestImage() {
    var size = (int)_imageSizeNumeric.Value;
    var pattern = _testPatternCombo.SelectedIndex;
    var bitmap = new Bitmap(size, size, PixelFormat.Format24bppRgb);

    switch (pattern) {
      case 0: CreateGradientPattern(bitmap); break;
      case 1: CreatePhotoLikePattern(bitmap); break;
      case 2: CreateGeometricPattern(bitmap); break;
      case 3: CreateNoisePattern(bitmap); break;
      case 4: CreateGrayscalePattern(bitmap); break;
      case 5: CreateBlackWhitePattern(bitmap); break;
    }

    return bitmap;
  }

  private void CreateGradientPattern(Bitmap bitmap) {
    for (var y = 0; y < bitmap.Height; ++y)
    for (var x = 0; x < bitmap.Width; ++x) {
      var r = x * 255 / bitmap.Width;
      var g = y * 255 / bitmap.Height;
      var b = (x + y) * 255 / (bitmap.Width + bitmap.Height);
      bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
    }
  }

  private void CreatePhotoLikePattern(Bitmap bitmap) {
    for (var y = 0; y < bitmap.Height; ++y)
    for (var x = 0; x < bitmap.Width; ++x) {
      var nx = (double)x / bitmap.Width;
      var ny = (double)y / bitmap.Height;

      var r = (int)(127 + 64 * Math.Sin(nx * Math.PI * 4) * Math.Cos(ny * Math.PI * 3));
      var g = (int)(127 + 64 * Math.Sin(nx * Math.PI * 6) * Math.Sin(ny * Math.PI * 2));
      var b = (int)(127 + 64 * Math.Cos(nx * Math.PI * 5) * Math.Cos(ny * Math.PI * 4));

      r = Math.Max(0, Math.Min(255, r));
      g = Math.Max(0, Math.Min(255, g));
      b = Math.Max(0, Math.Min(255, b));

      bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
    }
  }

  private void CreateGeometricPattern(Bitmap bitmap) {
    for (var y = 0; y < bitmap.Height; ++y)
    for (var x = 0; x < bitmap.Width; ++x) {
      var pattern = x / 32 % 2 == y / 32 % 2;
      var intensity = pattern ? 255 : 0;
      var r = intensity;
      var g = x * 255 / bitmap.Width;
      var b = y * 255 / bitmap.Height;
      bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
    }
  }

  private void CreateNoisePattern(Bitmap bitmap) {
    var random = new Random(42);
    for (var y = 0; y < bitmap.Height; ++y)
    for (var x = 0; x < bitmap.Width; ++x) {
      var r = random.Next(256);
      var g = random.Next(256);
      var b = random.Next(256);
      bitmap.SetPixel(x, y, Color.FromArgb(r, g, b));
    }
  }

  private void CreateGrayscalePattern(Bitmap bitmap) {
    for (var y = 0; y < bitmap.Height; ++y)
    for (var x = 0; x < bitmap.Width; ++x) {
      var nx = (double)x / bitmap.Width;
      var ny = (double)y / bitmap.Height;

      // Create complex grayscale patterns with gradients and textures
      var base_value = (int)(127 + 64 * Math.Sin(nx * Math.PI * 3) * Math.Cos(ny * Math.PI * 2));
      var texture = (int)(20 * Math.Sin(nx * Math.PI * 10) * Math.Sin(ny * Math.PI * 8));
      var gradient = (int)(64 * (nx + ny) / 2);
      
      var gray = Math.Max(0, Math.Min(255, base_value + texture + gradient));
      bitmap.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
    }
  }

  private void CreateBlackWhitePattern(Bitmap bitmap) {
    for (var y = 0; y < bitmap.Height; ++y)
    for (var x = 0; x < bitmap.Width; ++x) {
      var nx = (double)x / bitmap.Width;
      var ny = (double)y / bitmap.Height;

      // Create complex black and white patterns with various shapes
      var circle = Math.Sqrt((nx - 0.5) * (nx - 0.5) + (ny - 0.5) * (ny - 0.5));
      var stripes = Math.Sin(nx * Math.PI * 8) * Math.Sin(ny * Math.PI * 6);
      var checker = ((x / 16) % 2 == (y / 16) % 2) ? 1 : -1;
      
      var pattern_value = circle * 2 + stripes + checker * 0.3;
      var color = pattern_value > 0.5 ? Color.White : Color.Black;
      
      bitmap.SetPixel(x, y, color);
    }
  }

  private void LoadImageButton_Click(object sender, EventArgs e) {
    using var dialog = new OpenFileDialog {
      Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
      Title = "Select Test Image"
    };

    if (dialog.ShowDialog() == DialogResult.OK) {
      try {
        _testImage?.Dispose();
        _testImage = new Bitmap(dialog.FileName);
        _originalPictureBox.Image = _testImage;
        _statusLabel.Text = $"Loaded: {Path.GetFileName(dialog.FileName)}";
      }
      catch (Exception ex) {
        MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }
  }

  private void RunTestsButton_Click(object sender, EventArgs e) {
    if (_testImage == null) {
      LoadDefaultTestImage();
    }

    _runTestsButton.Enabled = false;
    _results.Clear();
    _statusLabel.Text = "Running comparison...";
    _backgroundWorker.RunWorkerAsync();
  }

  private void ExportButton_Click(object sender, EventArgs e) {
    if (_results.Count == 0) {
      MessageBox.Show("No results to export!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return;
    }

    using var dialog = new SaveFileDialog {
      Filter = "CSV Files|*.csv",
      Title = "Export Results",
      FileName = $"algorithm_comparison_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
    };

    if (dialog.ShowDialog() == DialogResult.OK) {
      try {
        ExportResultsToCSV(dialog.FileName);
        _statusLabel.Text = $"Exported to {Path.GetFileName(dialog.FileName)}";
      }
      catch (Exception ex) {
        MessageBox.Show($"Error exporting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }
  }

  private void ResultsGrid_SelectionChanged(object sender, EventArgs e) {
    if (_resultsGrid.SelectedRows.Count > 0 && _resultsGrid.SelectedRows[0].DataBoundItem is DataRowView row) {
      var status = row["Status"].ToString();
      if (status == "Completed" && _testImage != null) {
        try {
          // Create ComparisonResult from DataRow for processing
          var result = new ComparisonResult {
            Quantizer = row["Quantizer"].ToString() ?? "",
            Ditherer = row["Ditherer"].ToString() ?? "",
            Metric = row["Metric"].ToString() ?? ""
          };
          
          // Recreate the processed image for preview
          var processedImage = ProcessImageForPreview(result);
          _processedPictureBox.Image?.Dispose();
          _processedPictureBox.Image = processedImage;
        }
        catch {
          _processedPictureBox.Image = null;
        }
      }
    }
  }

  private void TestPatternCombo_SelectedIndexChanged(object sender, EventArgs e) {
    UpdateTestImage();
  }

  private void ImageSizeNumeric_ValueChanged(object sender, EventArgs e) {
    UpdateTestImage();
  }

  private void UpdateTestImage() {
    try {
      _testImage?.Dispose();
      _testImage = CreateTestImage();
      _originalPictureBox.Image = _testImage;
      
      // Clear the processed image preview since the original changed
      _processedPictureBox.Image?.Dispose();
      _processedPictureBox.Image = null;
    }
    catch (Exception ex) {
      // Log error but don't show message box for every change
      System.Diagnostics.Debug.WriteLine($"Error updating test image: {ex.Message}");
    }
  }

  private Bitmap? ProcessImageForPreview(ComparisonResult result) {
    if (_testImage == null) return null;

    try {
      // Get algorithms
      var quantizer = GetQuantizer(result.Quantizer);
      var ditherer = GetDitherer(result.Ditherer);
      
      if (quantizer == null || ditherer == null) return null;

      // Create palette
      var colors = new List<Color>();
      for (var y = 0; y < _testImage.Height; ++y)
      for (var x = 0; x < _testImage.Width; ++x) {
        colors.Add(_testImage.GetPixel(x, y));
      }

      var palette = quantizer.ReduceColorsTo((byte)_paletteSizeNumeric.Value, colors);
      return ApplyDithering(_testImage, ditherer, palette);
    }
    catch {
      return null;
    }
  }

  private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
    // Get only selected algorithms
    var selectedQuantizers = GetSelectedQuantizers();
    var selectedDitherers = GetSelectedDitherers();
    var selectedMetrics = GetSelectedColorMetrics();

    // Create thread-safe copy of test image for parallel processing
    if (_testImage == null) return;

    // Create all test combinations
    var testCombinations = new List<(string quantizerName, string dithererName, string metricName, IDitherer ditherer, IColorDistanceMetric metric)>();
    
    foreach (var (quantizerName, _) in selectedQuantizers) {
      foreach (var (dithererName, ditherer) in selectedDitherers) {
        foreach (var (metricName, metric) in selectedMetrics) {
          testCombinations.Add((quantizerName, dithererName, metricName, ditherer, metric));
        }
      }
    }

    var totalTests = testCombinations.Count;
    var completedTests = 0; // This will be accessed via Interlocked operations
    var lockObject = new object();

    // Configure parallel execution with aggressive settings
    var parallelOptions = new ParallelOptions {
      MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount, 8), // Use at least 8 threads, more if CPU has more cores
      CancellationToken = new System.Threading.CancellationToken() // We'll handle cancellation manually
    };
    
    // Add performance monitoring
    var startTime = DateTime.Now;
    var coreCount = Environment.ProcessorCount;
    Invoke(new Action(() => _statusLabel.Text = $"Starting {totalTests} tests on {coreCount} cores with {parallelOptions.MaxDegreeOfParallelism} max threads..."));

    // Create results collection for thread-safe access
    var results = new List<ComparisonResult>();
    foreach (var (quantizerName, dithererName, metricName, _, _) in testCombinations) {
      var result = new ComparisonResult {
        Quantizer = quantizerName,
        Ditherer = dithererName,
        Metric = metricName,
        Status = "Pending..."
      };
      results.Add(result);
    }
    
    // Add all results to DataTable in one batch for better performance
    Invoke(new Action(() => {
      _dataTable.Clear(); // Clear previous results
      foreach (var result in results) {
        AddResultToDataTable(result);
      }
    }));
    
    // Pre-create multiple bitmap copies to reduce bitmap creation overhead during parallel execution
    var bitmapCopies = new Bitmap[Math.Min(parallelOptions.MaxDegreeOfParallelism, totalTests)];
    try {
      for (var i = 0; i < bitmapCopies.Length; i++) {
        bitmapCopies[i] = CreateIndependentBitmapCopy(_testImage);
      }
    }
    catch (Exception ex) {
      // Clean up any created bitmaps
      foreach (var bitmap in bitmapCopies) {
        bitmap?.Dispose();
      }
      Invoke(new Action(() => MessageBox.Show($"Error creating test image copies: {ex.Message}", "Error")));
      return;
    }

    try {
      Parallel.For(0, testCombinations.Count, parallelOptions, i => {
        if (_backgroundWorker.CancellationPending) {
          return; // Exit this iteration
        }

        var (quantizerName, dithererName, metricName, ditherer, metric) = testCombinations[i];
        var result = results[i];

        try {
          // Update status in both ComparisonResult and DataTable (reduce UI thread contention)
          result.Status = "Processing...";
          
          // Use pre-created bitmap copy for better performance (round-robin assignment)
          var bitmapIndex = i % bitmapCopies.Length;
          var threadLocalImageCopy = bitmapCopies[bitmapIndex];
          
          // Create fresh quantizer instance for each test to avoid state contamination
          var freshQuantizer = CreateFreshQuantizer(quantizerName);
          TestSingleCombinationThreadSafe(result, freshQuantizer, ditherer, metric, threadLocalImageCopy);
          result.Status = "Completed";
        }
        catch (Exception ex) {
          result.Status = $"Error: {ex.Message}";
        }

        // Update progress thread-safely with less contention
        var currentCompleted = Interlocked.Increment(ref completedTests);
        
        // Batch UI updates to reduce thread contention
        if (currentCompleted % 5 == 0 || currentCompleted == totalTests) {
          try {
            var progress = currentCompleted * 100 / totalTests;
            var statusMessage = $"Completed {currentCompleted}/{totalTests} ({progress}%) - Latest: {quantizerName} + {dithererName}";
            _backgroundWorker.ReportProgress(progress, statusMessage);
            
            // Update multiple results at once to reduce UI thread calls
            Invoke(new Action(() => {
              // Update the last 5 results or all remaining results
              var startIdx = Math.Max(0, currentCompleted - 5);
              for (var j = startIdx; j < currentCompleted && j < results.Count; j++) {
                UpdateResultInDataTable(results[j], j);
              }
            }));
          }
          catch (InvalidOperationException) {
            // Handle case where form is being disposed during background operation
          }
        }
      });
    }
    catch (OperationCanceledException) {
      e.Cancel = true;
    }
    finally {
      // Clean up all pre-created bitmap copies
      foreach (var bitmap in bitmapCopies) {
        bitmap?.Dispose();
      }
      
      // Performance reporting
      var elapsed = DateTime.Now - startTime;
      var testsPerSecond = totalTests / elapsed.TotalSeconds;
      Invoke(new Action(() => {
        _statusLabel.Text = $"Completed {totalTests} tests in {elapsed.TotalSeconds:F1}s ({testsPerSecond:F1} tests/sec)";
      }));
    }
  }

  private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
    _progressBar.Value = e.ProgressPercentage;
    _statusLabel.Text = e.UserState?.ToString() ?? "Processing...";
  }

  private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
    _runTestsButton.Enabled = true;
    _progressBar.Value = 0;
    
    // Force final UI refresh to ensure all results are visible
    _resultsGrid.Refresh();
    
    if (e.Cancelled) {
      _statusLabel.Text = "Cancelled";
    } else if (e.Error != null) {
      _statusLabel.Text = $"Error: {e.Error.Message}";
    } else {
      var completed = _dataTable.AsEnumerable().Count(row => row["Status"].ToString() == "Completed");
      _statusLabel.Text = $"Completed {completed} tests. Click column headers to sort.";
    }
  }

  private void TestSingleCombination(ComparisonResult result, IQuantizer quantizer, IDitherer ditherer, IColorDistanceMetric metric) {
    if (_testImage == null) return;

    // Create palette
    var colors = new List<Color>();
    for (var y = 0; y < _testImage.Height; ++y)
    for (var x = 0; x < _testImage.Width; ++x) {
      colors.Add(_testImage.GetPixel(x, y));
    }

    var palette = quantizer.ReduceColorsTo((byte)_paletteSizeNumeric.Value, colors);

    // Measure performance
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    using var processedImage = ApplyDithering(_testImage, ditherer, palette);
    stopwatch.Stop();

    result.ExecutionTime_ms = stopwatch.Elapsed.TotalMilliseconds;
    result.PixelsPerSecond = _testImage.Width * _testImage.Height / stopwatch.Elapsed.TotalSeconds;

    // Calculate quality metrics
    result.PSNR = ImageQualityMetrics.CalculatePSNR(_testImage, processedImage);
    result.SSIM = ImageQualityMetrics.CalculateSSIM(_testImage, processedImage);
    result.SNR = ImageQualityMetrics.CalculateSNR(_testImage, processedImage);
    result.EdgePreservation = ImageQualityMetrics.CalculateEdgePreservation(_testImage, processedImage);
    result.Contrast = ImageQualityMetrics.CalculateContrast(processedImage);
    result.ColorCount = ImageQualityMetrics.CalculateColorCount(processedImage);
    
    // Additional quality metrics
    result.UniqueColors = ImageQualityMetrics.CalculateUniqueColorCount(processedImage);
    result.HistogramBins = ImageQualityMetrics.CalculateHistogramColorCount(processedImage);
    result.HistogramEntropy = ImageQualityMetrics.CalculateHistogramEntropy(processedImage);
    result.ColorSpread = ImageQualityMetrics.CalculateColorSpread(processedImage);
    result.ColorUniformity = ImageQualityMetrics.CalculateColorUniformity(processedImage);
    result.HistogramDifference = ImageQualityMetrics.CalculateHistogramDifference(_testImage, processedImage);
  }

  /// <summary>
  /// Creates a completely independent bitmap copy to avoid resource conflicts in parallel processing
  /// </summary>
  private static Bitmap CreateIndependentBitmapCopy(Bitmap source) {
    if (source == null) throw new ArgumentNullException(nameof(source));
    
    // Use Clone() with proper pixel format to create an independent copy
    return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format24bppRgb);
  }

  private void TestSingleCombinationThreadSafe(ComparisonResult result, IQuantizer quantizer, IDitherer ditherer, IColorDistanceMetric metric, Bitmap testImage) {
    if (testImage == null) return;

    int paletteSizeValue = 16; // Default value
    // Get palette size thread-safely
    Invoke(new Action(() => paletteSizeValue = (int)_paletteSizeNumeric.Value));

    // Create palette
    var colors = new List<Color>();
    for (var y = 0; y < testImage.Height; ++y)
    for (var x = 0; x < testImage.Width; ++x) {
      colors.Add(testImage.GetPixel(x, y));
    }

    var palette = quantizer.ReduceColorsTo((byte)paletteSizeValue, colors);

    // Measure performance
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    using var processedImage = ApplyDithering(testImage, ditherer, palette);
    stopwatch.Stop();

    result.ExecutionTime_ms = stopwatch.Elapsed.TotalMilliseconds;
    result.PixelsPerSecond = testImage.Width * testImage.Height / stopwatch.Elapsed.TotalSeconds;

    // Calculate quality metrics
    result.PSNR = ImageQualityMetrics.CalculatePSNR(testImage, processedImage);
    result.SSIM = ImageQualityMetrics.CalculateSSIM(testImage, processedImage);
    result.SNR = ImageQualityMetrics.CalculateSNR(testImage, processedImage);
    result.EdgePreservation = ImageQualityMetrics.CalculateEdgePreservation(testImage, processedImage);
    result.Contrast = ImageQualityMetrics.CalculateContrast(processedImage);
    result.ColorCount = ImageQualityMetrics.CalculateColorCount(processedImage);
    
    // Additional quality metrics
    result.UniqueColors = ImageQualityMetrics.CalculateUniqueColorCount(processedImage);
    result.HistogramBins = ImageQualityMetrics.CalculateHistogramColorCount(processedImage);
    result.HistogramEntropy = ImageQualityMetrics.CalculateHistogramEntropy(processedImage);
    result.ColorSpread = ImageQualityMetrics.CalculateColorSpread(processedImage);
    result.ColorUniformity = ImageQualityMetrics.CalculateColorUniformity(processedImage);
    result.HistogramDifference = ImageQualityMetrics.CalculateHistogramDifference(testImage, processedImage);
  }

  private static Bitmap ApplyDithering(Bitmap source, IDitherer ditherer, Color[] palette) {
    // Validate input parameters
    if (source == null || source.Width <= 0 || source.Height <= 0) {
      throw new ArgumentException("Invalid source bitmap");
    }
    if (ditherer == null) {
      throw new ArgumentNullException(nameof(ditherer));
    }
    if (palette == null || palette.Length == 0) {
      throw new ArgumentException("Invalid palette");
    }

    Bitmap target = null;
    BitmapData targetData = null;

    try {
      target = new Bitmap(source.Width, source.Height, PixelFormat.Format8bppIndexed);
      targetData = target.LockBits(
        new Rectangle(0, 0, source.Width, source.Height),
        ImageLockMode.WriteOnly,
        PixelFormat.Format8bppIndexed
      );

      using var locker = source.Lock();
      ditherer.Dither(locker, targetData, palette);
      
      return target;
    }
    catch (Exception) {
      // Clean up on error
      if (targetData != null && target != null) {
        try {
          target.UnlockBits(targetData);
        } catch { /* Ignore unlock errors during cleanup */ }
      }
      target?.Dispose();
      throw; // Re-throw the original exception
    }
    finally {
      // Always unlock bits if we got this far successfully
      if (targetData != null && target != null) {
        try {
          target.UnlockBits(targetData);
        } catch { /* Ignore unlock errors */ }
      }
    }
  }

  private void ExportResultsToCSV(string fileName) {
    using var writer = new StreamWriter(fileName);
    
    writer.WriteLine("Quantizer,Ditherer,Metric,PSNR,SSIM,SNR,EdgePreservation,Contrast,ColorCount," +
                    "UniqueColors,HistogramBins,HistogramEntropy,ColorSpread,ColorUniformity,HistogramDifference," +
                    "ExecutionTime_ms,PixelsPerSecond,Status");
    
    foreach (var result in _results) {
      writer.WriteLine($"{result.Quantizer},{result.Ditherer},{result.Metric}," +
                      $"{result.PSNR:F2},{result.SSIM:F4},{result.SNR:F2}," +
                      $"{result.EdgePreservation:F4},{result.Contrast:F2},{result.ColorCount}," +
                      $"{result.UniqueColors},{result.HistogramBins},{result.HistogramEntropy:F2}," +
                      $"{result.ColorSpread:F2},{result.ColorUniformity:F4},{result.HistogramDifference:F2}," +
                      $"{result.ExecutionTime_ms:F1},{result.PixelsPerSecond:F0},{result.Status}");
    }
  }

  private static Dictionary<string, IQuantizer> GetAllQuantizers() {
    // Return quantizer names mapped to sample instances for UI population
    var quantizers = new Dictionary<string, IQuantizer>();
    
    // Add core quantizers
    quantizers["OctreeQuantizer"] = new OctreeQuantizer();
    quantizers["MedianCutQuantizer"] = new MedianCutQuantizer();
    quantizers["WuQuantizer"] = new WuQuantizer();
    quantizers["VarianceBasedQuantizer"] = new VarianceBasedQuantizer();
    quantizers["BinarySplittingQuantizer"] = new BinarySplittingQuantizer();
    quantizers["VarianceCutQuantizer"] = new VarianceCutQuantizer();
    
    // Add wrapper quantizers that can enhance any base quantizer
    var baseQuantizer = new OctreeQuantizer(); // Use Octree as base for wrappers
    try {
      quantizers["PCA+OctreeQuantizer"] = new PcaQuantizerWrapper(baseQuantizer);
    } catch {
      // Skip if PCA wrapper fails to initialize
    }
    
    try {
      quantizers["AntRefinement+OctreeQuantizer"] = new AntRefinementWrapper(
        baseQuantizer, 3, Euclidean.Instance.Calculate); // 3 iterations for speed
    } catch {
      // Skip if Ant wrapper fails to initialize
    }
    
    return quantizers;
  }

  /// <summary>
  /// Creates a fresh quantizer instance for each test to avoid state contamination
  /// </summary>
  private static IQuantizer CreateFreshQuantizer(string name) {
    return name switch {
      "OctreeQuantizer" => new OctreeQuantizer(),
      "MedianCutQuantizer" => new MedianCutQuantizer(),
      "WuQuantizer" => new WuQuantizer(),
      "VarianceBasedQuantizer" => new VarianceBasedQuantizer(),
      "BinarySplittingQuantizer" => new BinarySplittingQuantizer(),
      "VarianceCutQuantizer" => new VarianceCutQuantizer(),
      "PCA+OctreeQuantizer" => new PcaQuantizerWrapper(new OctreeQuantizer()),
      "AntRefinement+OctreeQuantizer" => new AntRefinementWrapper(new OctreeQuantizer(), 3, Euclidean.Instance.Calculate),
      _ => throw new ArgumentException($"Unknown quantizer: {name}")
    };
  }

  private static Dictionary<string, IDitherer> GetAllDitherers() {
    var ditherers = new Dictionary<string, IDitherer>();
    
    // Matrix-based ditherers
    ditherers["Floyd-Steinberg"] = MatrixBasedDitherer.FloydSteinberg;
    ditherers["Jarvis-Judice-Ninke"] = MatrixBasedDitherer.JarvisJudiceNinke;
    ditherers["Stucki"] = MatrixBasedDitherer.Stucki;
    ditherers["Atkinson"] = MatrixBasedDitherer.Atkinson;
    ditherers["Sierra"] = MatrixBasedDitherer.Sierra;
    
    // Ordered ditherers
    ditherers["Bayer2x2"] = OrderedDitherer.Bayer2x2;
    ditherers["Bayer4x4"] = OrderedDitherer.Bayer4x4;
    ditherers["Bayer8x8"] = OrderedDitherer.Bayer8x8;
    
    // Noise ditherers
    ditherers["WhiteNoise"] = NoiseDitherer.White;
    ditherers["BlueNoise"] = NoiseDitherer.Blue;
    ditherers["BrownNoise"] = NoiseDitherer.Brown;
    
    // Advanced ditherers
    ditherers["Riemersma"] = RiemersmaDitherer.Default;
    ditherers["Knoll"] = KnollDitherer.Default;
    
    // No dithering baseline
    ditherers["NoDither"] = new NoDitherer();
    
    return ditherers;
  }

  private static Dictionary<string, IColorDistanceMetric> GetAllColorDistanceMetrics() {
    return new Dictionary<string, IColorDistanceMetric> {
      ["Euclidean"] = Euclidean.Instance,
      ["Manhattan"] = Manhattan.Instance,
      ["CIE DE2000"] = CieDe2000.Instance,
      ["CIE94-Textiles"] = Cie94.Textiles,
      ["CIE94-GraphicArts"] = Cie94.GraphicArts,
      ["WeightedEuclidean-BT709"] = WeightedEuclidean.BT709,
      ["WeightedEuclidean-Nommyde"] = WeightedEuclidean.Nommyde,
      ["WeightedManhattan-BT709"] = WeightedManhattan.BT709,
      ["WeightedManhattan-Nommyde"] = WeightedManhattan.Nommyde,
      ["WeightedYUV"] = WeightedYuv.Instance,
      ["WeightedYCbCr"] = WeightedYCbCr.Instance,
      ["PngQuant"] = PngQuant.Instance,
      ["CompuPhase"] = CompuPhase.Instance
    };
  }

  private Dictionary<string, IQuantizer> GetSelectedQuantizers() {
    var allQuantizers = GetAllQuantizers();
    var selected = new Dictionary<string, IQuantizer>();
    
    for (var i = 0; i < _quantizersList.Items.Count; i++) {
      if (_quantizersList.GetItemChecked(i)) {
        var name = _quantizersList.Items[i].ToString()!;
        if (allQuantizers.TryGetValue(name, out var quantizer)) {
          selected[name] = quantizer;
        }
      }
    }
    
    return selected.Count > 0 ? selected : allQuantizers.Take(1).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
  }

  private Dictionary<string, IDitherer> GetSelectedDitherers() {
    var allDitherers = GetAllDitherers();
    var selected = new Dictionary<string, IDitherer>();
    
    for (var i = 0; i < _ditherersList.Items.Count; i++) {
      if (_ditherersList.GetItemChecked(i)) {
        var name = _ditherersList.Items[i].ToString()!;
        if (allDitherers.TryGetValue(name, out var ditherer)) {
          selected[name] = ditherer;
        }
      }
    }
    
    return selected.Count > 0 ? selected : allDitherers.Take(1).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
  }

  private Dictionary<string, IColorDistanceMetric> GetSelectedColorMetrics() {
    var allMetrics = GetAllColorDistanceMetrics();
    var selected = new Dictionary<string, IColorDistanceMetric>();
    
    for (var i = 0; i < _metricsList.Items.Count; i++) {
      if (_metricsList.GetItemChecked(i)) {
        var name = _metricsList.Items[i].ToString()!;
        if (allMetrics.TryGetValue(name, out var metric)) {
          selected[name] = metric;
        }
      }
    }
    
    return selected.Count > 0 ? selected : allMetrics.Take(1).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
  }

  private static IQuantizer? GetQuantizer(string name) {
    try {
      return CreateFreshQuantizer(name);
    } catch {
      return null;
    }
  }

  private static IDitherer? GetDitherer(string name) {
    var ditherers = GetAllDitherers();
    return ditherers.TryGetValue(name, out var ditherer) ? ditherer : null;
  }

  protected override void Dispose(bool disposing) {
    if (disposing) {
      // Dispose of test images to prevent memory leaks
      _testImage?.Dispose();
      _originalPictureBox?.Image?.Dispose();
      _processedPictureBox?.Image?.Dispose();
      
      // Dispose background worker
      _backgroundWorker?.Dispose();
    }
    base.Dispose(disposing);
  }
}