using ClosedXML.Excel;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ultrasonic_watermeter
{
    public partial class Page2 : Page, INotifyPropertyChanged
    {
        private MainWindow? _mainWindow;
        private TaskCompletionSource<bool>? _ackTcs;
        private bool _isReceiving = false;
        private List<DataPoint> dataPoints = new List<DataPoint>();

        private double _currentValue;
        private double _minValue = double.MaxValue;
        private double _maxValue = double.MinValue;
        private double _averageValue;

        private PlotModel _plotModel = null!;
        private LineSeries _lineSeries = null!;

        public PlotModel PlotModel
        {
            get => _plotModel;
            set
            {
                _plotModel = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string TemperatureText
        {
            get => $"Nhiệt độ: {(dataPoints.Count > 0 ? dataPoints.Last()._temperatureData.ToString("F7") + " °C" : "--")}";
        }

        public string TimeUpText
        {
            get => $"Thời gian truyền xuôi: {(dataPoints.Count > 0 ? dataPoints.Last()._timeUpData.ToString("F7") + " ns" : "--")}";
        }

        public string TimeDownText
        {
            get => $"Thời gian truyền ngược: {(dataPoints.Count > 0 ? dataPoints.Last()._timeDownData.ToString("F7") + " ns" : "--")}";
        }
        public string CurrentFlowValueText
        {
            get => $"Giá trị lưu lượng hiện tại: {(dataPoints.Count > 0 ? (dataPoints.Last()._flowData * 3.6).ToString("F7") + " m3/h": "--")}";
        }

        public string MinFlowValueText
        {
            get => $"Giá trị lưu lượng nhỏ nhất: {(dataPoints.Count > 0 ? (dataPoints.Min(p => p._flowData) * 3.6).ToString("F7") + " m3/h " : "--")}";
        }

        public string MaxFlowValueText
        {
            get => $"Giá trị lưu lượng lớn nhất: {(dataPoints.Count > 0 ? (dataPoints.Max(p => p._flowData) * 3.6).ToString("F7") + " m3/h" : "--")}";
        }

        public string AvgFlowValueText
        {
            get => $"Giá trị lưu lượng trung bình: {(dataPoints.Count > 0 ? (dataPoints.Average(p => p._flowData) * 3.6).ToString("F7") + " m3/h" : "--")}";
        }

        public string TotalWaterUsageText
        {
            get
            {
                if (dataPoints == null || dataPoints.Count < 2)
                    return "Tổng lượng nước sử dụng: --";

                double volume = 0;

                for (int i = 1; i < dataPoints.Count; i++)
                {
                    var prev = dataPoints[i - 1];
                    var curr = dataPoints[i];

                    double dt = (curr._Time - prev._Time).TotalSeconds;
                    double avgFlow = (prev._flowData + curr._flowData) / 2;

                    volume += avgFlow * dt;
                }

                return $"Tổng lượng nước sử dụng: {volume:F7} l";
            }
        }

        public Page2()
        {
            InitializeComponent();
            InitializePlot();
            DataContext = this;
            Loaded += Page2_Loaded;
        }

        private void Page2_Loaded(object sender, RoutedEventArgs e)
        {
            _mainWindow = Window.GetWindow(this) as MainWindow;
        }
        public class DataPoint
        {
            public DateTime _Time { get; set; }
            public double _flowData { get; set; }
            public double _temperatureData { get; set; }
            public double _timeUpData { get; set; }
            public double _timeDownData { get; set; }
        }
        private void InitializePlot()
        {
            PlotModel = new PlotModel
            {
                Title = null,
                Background = OxyColors.White,
                TextColor = OxyColors.Black
            };

            var linearAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Lưu lượng m3/h",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                TextColor = OxyColors.Black
            };

            var timeAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Thời gian",
                StringFormat = "HH:mm:ss:fff",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                TextColor = OxyColors.Black
            };

            PlotModel.Axes.Add(linearAxis);
            PlotModel.Axes.Add(timeAxis);

            _lineSeries = new LineSeries
            {
                Title = "Lưu lượng nước",
                Color = OxyColors.Blue,
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                MarkerFill = OxyColors.Red,
                CanTrackerInterpolatePoints = false
            };

            PlotModel.Series.Add(_lineSeries);
        }

        public void UpdateData(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            // ✅ Quan trọng: Xử lý ACK trước, bất kể trạng thái nào
            if (data.Length == 1 && data[0] == 0x06)
            {
                _ackTcs?.TrySetResult(true);
                Dispatcher.Invoke(() => AddToTerminal("✅ Nhận được phản hồi"));
                return;
            }

            // Xử lý dữ liệu cảm biến (20 byte)
            if (_isReceiving)
            {
                if (data.Length == 20)
                {
                    ProcessSensorData(data);
                    return;
                }
                else
                {
                    Dispatcher.Invoke(() => AddToTerminal($"⚠️ Dữ liệu không hợp lệ: {BitConverter.ToString(data)}"));
                    return;
                }
            }
            else
            {
                string hexString = BitConverter.ToString(data);
                string textString = Encoding.ASCII.GetString(data).Trim();

                Dispatcher.Invoke(() =>
                {
                    AddToTerminal($"📨 Nhận: {textString} (Hex: {hexString})");
                });
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            MainWindow main = (MainWindow)Application.Current.MainWindow;

            if (!main.IsConnected)
            {
                AddToTerminal("❌ Chưa kết nối COM!");
                return;
            }

            if (_isReceiving)
            {
                AddToTerminal("⚠️ Đang trong chế độ nhận dữ liệu!");
                return;
            }

            AddToTerminal("📤 Gửi yêu cầu nhận tới thiết bị đo!");
            for (int i = 0; i < 50; i++)
            {
                _ackTcs = new TaskCompletionSource<bool>();
                _mainWindow?.SendData(new byte[] { (byte)MainWindow.CommandCode.ENABLE_DATA_REALTIME });

                var completed = await Task.WhenAny(_ackTcs.Task, Task.Delay(200));

                if (completed == _ackTcs.Task && await _ackTcs.Task)
                {
                    _isReceiving = true;
                    btnStart.IsEnabled = false;
                    btnStop.IsEnabled = true;
                    AddToTerminal("▶ Bắt đầu nhận dữ liệu thời gian thực");
                    return;
                }

                await Task.Delay(50);
            }

            AddToTerminal("❌ Không nhận được phản hồi từ slave!");
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            MainWindow main = (MainWindow)Application.Current.MainWindow;

            if (!main.IsConnected)
            {
                AddToTerminal("❌ Chưa kết nối COM!");
                return;
            }

            if (!_isReceiving)
            {
                AddToTerminal("⚠️ Không trong chế độ nhận dữ liệu!");
                return;
            }

            AddToTerminal("📤 Gửi yêu cầu dừng nhận dữ liệu!");
            for (int i = 0; i < 50; i++)
            {
                _ackTcs = new TaskCompletionSource<bool>();
                _mainWindow?.SendData(new byte[] { (byte)MainWindow.CommandCode.DISABLE_DATA_REALTIME });

                var completed = await Task.WhenAny(_ackTcs.Task, Task.Delay(100));

                if (completed == _ackTcs.Task && await _ackTcs.Task)
                {
                    _isReceiving = false;
                    btnStart.IsEnabled = true;
                    btnStop.IsEnabled = false;
                    AddToTerminal("⏹ Đã dừng nhận dữ liệu");
                    return;
                }

                await Task.Delay(50);
            }

            AddToTerminal("❌ Không nhận được phản hồi từ slave!");
        }
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ClearData();
            AddToTerminal("🔄 Đã đặt lại dữ liệu");
        }
        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            // Tạo hộp thoại chọn file Excel để mở
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx";
            openFileDialog.Title = "Chọn file Excel để import dữ liệu";
            openFileDialog.CheckFileExists = true;

            // Hiển thị hộp thoại
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                List<DataPoint> importedPoints = new List<DataPoint>();
                try
                {
                    using (var workbook = new XLWorkbook(filePath))
                    {
                        var worksheet = workbook.Worksheet(1); // Lấy sheet đầu tiên
                        int startRow = 2; // Dữ liệu bắt đầu từ dòng 2
                        int lastRow = (worksheet.LastRowUsed()?.RowNumber() ?? startRow) - 1;

                        // Duyệt từng dòng dữ liệu
                        for (int row = startRow; row <= lastRow; row++)
                        {
                            // Kiểm tra nếu dòng trống thì bỏ qua
                            var timeCell = worksheet.Cell(row, 1);
                            if (timeCell.IsEmpty() || string.IsNullOrWhiteSpace(timeCell.GetString()))
                                continue;
                            try
                            {
                                DataPoint point = new DataPoint();
                                // Đọc cột 1: Thời gian
                                if (timeCell.DataType == XLDataType.DateTime)
                                {
                                    point._Time = timeCell.GetDateTime();
                                }
                                else
                                {
                                    // Thử parse nếu là string
                                    DateTime parsedTime;
                                    if (DateTime.TryParseExact(timeCell.GetString(), "HH:mm dd/MM/yyyy",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out parsedTime))
                                    {
                                        point._Time = parsedTime;
                                    }
                                    else
                                    {
                                        point._Time = DateTime.MinValue;
                                    }
                                }

                                // Đọc cột 2: Lưu lượng
                                point._flowData = worksheet.Cell(row, 2).GetDouble();

                                // Đọc cột 3: Nhiệt độ
                                point._temperatureData = worksheet.Cell(row, 3).GetDouble();

                                // Đọc cột 4: Thời gian truyền xuôi
                                point._timeUpData = worksheet.Cell(row, 4).GetDouble();

                                // Đọc cột 5: Thời gian truyền ngược
                                point._timeDownData = worksheet.Cell(row, 5).GetDouble();

                                importedPoints.Add(point);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Lỗi đọc dòng {row}: {ex.Message}", "Lỗi",
                                                MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                    }

                    if (PreviewAndImport(importedPoints))
                    {
                        dataPoints.Clear();
                        dataPoints.AddRange(importedPoints);
                        MessageBox.Show($"Đã import thành công {dataPoints.Count} dòng dữ liệu!",
                                        "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                        // Vẽ lại chart
                        _lineSeries.Points.Clear();
                        foreach (var p in dataPoints)
                        {
                            _lineSeries.Points.Add(new OxyPlot.DataPoint(
                                DateTimeAxis.ToDouble(p._Time),
                                p._flowData));
                        }
                        PlotModel.InvalidatePlot(true);
                        // Cập nhật thống kê
                        UpdateStatistics();
                    }
                    else
                    {
                        MessageBox.Show("Đã hủy import dữ liệu.", "Thông báo",
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi đọc file: {ex.Message}", "Lỗi",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private bool PreviewAndImport(List<DataPoint> points)
        {
            if (points == null || points.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu để hiển thị! Vui lòng import dữ liệu trước.",
                                "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Tạo nội dung hiển thị
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===== DỮ LIỆU ĐÃ IMPORT =====\n");

            foreach (var point in points)
            {
                sb.AppendLine($"[{point._Time:HH:mm dd/MM/yyyy}]: Lưu lượng: {point._flowData,3:F7} L - Nhiệt độ: {point._temperatureData,3:F7} - Thời gian truyền xuôi: {point._timeUpData,3:F7} - Thời gian truyền ngược: {point._timeDownData,3:F7}");
            }

            sb.AppendLine($"\nTổng số dòng: {points.Count}");
            sb.AppendLine("============================");

            // Tạo cửa sổ
            Window previewWindow = new Window
            {
                Title = "Xem trước dữ liệu import",
                Width = 700,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            StackPanel mainPanel = new StackPanel();

            // TextBox hiển thị dữ liệu
            TextBox textBox = new TextBox
            {
                Text = sb.ToString(),
                IsReadOnly = true,
                Padding = new System.Windows.Thickness(10),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                Height = 380
            };

            // Panel chứa 2 nút
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(10)
            };

            Button btnApply = new Button
            {
                Content = "Nhập",
                Width = 150,
                Height = 35,
                Margin = new System.Windows.Thickness(10)
            };

            Button btnCancel = new Button
            {
                Content = "Hủy bỏ",
                Width = 100,
                Height = 35,
                Margin = new System.Windows.Thickness(10)
            };

            buttonPanel.Children.Add(btnApply);
            buttonPanel.Children.Add(btnCancel);

            mainPanel.Children.Add(textBox);
            mainPanel.Children.Add(buttonPanel);

            previewWindow.Content = mainPanel;

            bool result = false;

            // Xử lý sự kiện
            btnApply.Click += (sender2, e2) =>
            {
                result = true;
                previewWindow.Close();
            };

            btnCancel.Click += (sender2, e2) =>
            {
                result = false;
                previewWindow.Close();
            };

            previewWindow.ShowDialog();
            return result;
        }
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (dataPoints == null || dataPoints.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu để xuất!", "Thông báo",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Tạo hộp thoại chọn vị trí lưu
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Excel Files (*.xlsx)|*.xlsx";
            saveFileDialog.DefaultExt = "xlsx";
            saveFileDialog.FileName = $"Export_Data_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            saveFileDialog.Title = "Chọn vị trí lưu file Excel";

            // Hiển thị hộp thoại
            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;

                try
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Data");

                        // GHI HEADER (tên cột) ===
                        worksheet.Cell(1, 1).Value = "Thời gian";
                        worksheet.Cell(1, 2).Value = "Lưu lượng (l/s)";
                        worksheet.Cell(1, 3).Value = "Nhiệt độ (°C)";
                        worksheet.Cell(1, 4).Value = "Thời gian truyền xuôi (ns)";
                        worksheet.Cell(1, 5).Value = "Thời gian truyền ngược (ns)";

                        // Định dạng header (in đậm, màu nền)
                        var headerRange = worksheet.Range("A1:E1");
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        // GHI DỮ LIỆU ===
                        for (int i = 0; i < dataPoints.Count; i++)
                        {
                            var point = dataPoints[i];
                            int row = i + 2;

                            worksheet.Cell(row, 1).Value = point._Time;
                            worksheet.Cell(row, 2).Value = point._flowData;
                            worksheet.Cell(row, 3).Value = point._temperatureData;
                            worksheet.Cell(row, 4).Value = point._timeUpData;
                            worksheet.Cell(row, 5).Value = point._timeDownData;
                        }

                        // ĐỊNH DẠNG CỘT ===
                        // Cột thời gian: định dạng ngày giờ
                        worksheet.Column(1).Style.DateFormat.Format = "HH:mm:ss dd/MM/yyyy";
                        worksheet.Column(2).Style.NumberFormat.Format = "#,##0.0000000";
                        worksheet.Column(3).Style.NumberFormat.Format = "#,##0.0000000";
                        worksheet.Column(4).Style.NumberFormat.Format = "#,##0.0000000";
                        worksheet.Column(5).Style.NumberFormat.Format = "#,##0.0000000";
                        worksheet.Columns().AdjustToContents();

                        //THÊM BẢNG TÍNH TỔNG ===
                        int lastRow = dataPoints.Count + 1;
                        worksheet.Cell(lastRow + 1, 1).Value = "Trung bình:";
                        worksheet.Cell(lastRow + 1, 1).Style.Font.Bold = true;

                        worksheet.Cell(lastRow + 1, 2).FormulaA1 = $"=AVERAGE(B2:B{lastRow})";
                        worksheet.Cell(lastRow + 1, 2).Style.Font.Bold = true;
                        worksheet.Cell(lastRow + 1, 2).Style.NumberFormat.Format = "#,##0.0000000";

                        // LƯU FILE ===
                        workbook.SaveAs(filePath);
                    }

                    MessageBoxResult result = MessageBox.Show(
                        $"Đã xuất thành công {dataPoints.Count} dòng dữ liệu!\n\nFile: {filePath}\n\nBạn có muốn mở file ngay không?",
                        "Xuất Excel thành công",
                        MessageBoxButton.YesNo,  // Yes = Mở, No = Thoát
                        MessageBoxImage.Information,
                        MessageBoxResult.Yes);    // Nút mặc định là Yes (Mở)
                    if (result == MessageBoxResult.Yes)
                    {
                        // Mở file Excel
                        try
                        {
                            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Không thể mở file: {ex.Message}", "Lỗi",
                                            MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xuất file: {ex.Message}", "Lỗi",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            PlotModel.ResetAllAxes();
            PlotModel.InvalidatePlot(false);
        }
        private void ClearData()
        {
            Dispatcher.Invoke(() =>
            {
                dataPoints.Clear();
                _minValue = double.MaxValue;
                _maxValue = double.MinValue;

                _lineSeries.Points.Clear();
                PlotModel.InvalidatePlot(true);
                UpdateStatistics();
            });
        }
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearTerminal();
        }
        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            SendCommand();
        }

        private void ProcessSensorData(byte[] data)
        {
            try
            {
                float floatValue = BitConverter.ToSingle(data, 0);
                float temperatureValue = BitConverter.ToSingle(data, 4);
                float timeUpValue = BitConverter.ToSingle(data, 8);
                float timeDownValue = BitConverter.ToSingle(data, 12);
                uint timeMeasure = BitConverter.ToUInt32(data, 16);

                double doubleValue = Convert.ToDouble(floatValue);

                Dispatcher.Invoke(() =>
                {
                    DateTime currentTime = DateTime.Now;
                    dataPoints.Add(new DataPoint
                    {
                        _Time = currentTime,
                        _flowData = doubleValue,
                        _temperatureData = temperatureValue,
                        _timeUpData = timeUpValue,
                        _timeDownData = timeDownValue,
                    });

                    if (doubleValue < _minValue) _minValue = doubleValue;
                    if (doubleValue > _maxValue) _maxValue = doubleValue;

                    UpdateChart(dataPoints.Last());
                    UpdateStatistics();

                    AddToTerminal($"📊 Lưu lượng: {doubleValue,3:F7}, Nhiệt độ: {temperatureValue,3:F7} °C, Thời gian truyền xuôi: {timeUpValue,3:F7} ns, Thời gian truyền ngược: {timeDownValue,3:F7} ns, Thời gian đo: {timeMeasure} ms");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AddToTerminal($"❌ Lỗi xử lý dữ liệu: {ex.Message}"));
            }
        }

        private void UpdateChart(DataPoint newData)
        {
            Dispatcher.Invoke(() =>
            {
                _lineSeries.Points.Add(new OxyPlot.DataPoint(
                    DateTimeAxis.ToDouble(newData._Time),
                    newData._flowData * 3.6
                ));

                PlotModel.InvalidatePlot(true);
            });
        }

        private void UpdateStatistics()
        {
            Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(TemperatureText));
                OnPropertyChanged(nameof(TimeUpText));
                OnPropertyChanged(nameof(TimeDownText));
                OnPropertyChanged(nameof(CurrentFlowValueText));
                OnPropertyChanged(nameof(MinFlowValueText));
                OnPropertyChanged(nameof(MaxFlowValueText));
                OnPropertyChanged(nameof(AvgFlowValueText));
                OnPropertyChanged(nameof(TotalWaterUsageText));

                // Cập nhật lại các TextBlock hiển thị thống kê
                txtTemperature.Text = TemperatureText;
                txtTimeUp.Text = TimeUpText;
                txtTimeDown.Text = TimeDownText;
                txtCurrentFlowValue.Text = CurrentFlowValueText;
                txtMinFlowValue.Text = MinFlowValueText;
                txtMaxFlowValue.Text = MaxFlowValueText;
                txtAvgFlowValue.Text = AvgFlowValueText;
                txtTotalWaterUsage.Text = TotalWaterUsageText;
            });
        }

        private void AddToTerminal(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                lstTerminal.Items.Add($"[{timestamp}] {message}");

                if (lstTerminal.Items.Count > 0)
                    lstTerminal.ScrollIntoView(lstTerminal.Items[lstTerminal.Items.Count - 1]);

                while (lstTerminal.Items.Count > 1000)
                    lstTerminal.Items.RemoveAt(0);
            });
        }
        private void ClearTerminal()
        {
            Dispatcher.Invoke(() =>
            {
                lstTerminal.Items.Clear();
            });
        }

        private void TxtSendCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SendCommand();
        }

        private void SendCommand()
        {
            if (string.IsNullOrWhiteSpace(txtSendCommand.Text))
                return;

            string command = txtSendCommand.Text;
            AddToTerminal($"📤 Gửi lệnh: {command}");

            try
            {
                if (command.Contains(" ") || (command.Length % 2 == 0 && command.Length >= 2))
                {
                    var hexBytes = command.Split(' ')
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => Convert.ToByte(x, 16))
                        .ToArray();
                    _mainWindow?.SendData(hexBytes);
                    AddToTerminal($"📤 Dạng hex: {BitConverter.ToString(hexBytes)}");
                }
                else
                {
                    _mainWindow?.SendString(command);
                }
            }
            catch
            {
                _mainWindow?.SendString(command);
            }

            txtSendCommand.Clear();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}