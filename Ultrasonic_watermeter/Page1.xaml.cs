using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Wordprocessing;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using LiveCharts.Wpf.Charts.Base;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ultrasonic_watermeter
{
    public partial class Page1 : Page
    {
        private MainWindow? _mainWindow;
        private TaskCompletionSource<bool>? _ackTcs;
        private TaskCompletionSource<byte[]>? _dataTcs;

        private List<byte> _rxBuffer = new List<byte>();

        private const int PACK_SIZE = 13;
        private const int TOTAL_PACK = 180;
        private const int TOTAL_SIZE = PACK_SIZE * TOTAL_PACK;

        private bool _isReceivingData = false;

        private string type_chart = "";

        private List<DataPoint> dataPoints = new List<DataPoint>();

        public Page1()
        {
            InitializeComponent();
            ClearChart();
            Loaded += Page1_Loaded;
        }
        private void Page1_Loaded(object sender, RoutedEventArgs e)
        {
            _mainWindow = Window.GetWindow(this) as MainWindow;
        }
        public class DataPoint
        {
            public DateTime Time { get; set; }
            public uint Used { get; set; }
            public uint Index { get; set; }
        }
        // Hàm này được gọi từ MainWindow khi có dữ liệu
        public void UpdateData(byte[] data)
        {
            try
            {
                // Nếu đang ở mode nhận DATA
                if (_isReceivingData)
                {
                    _rxBuffer.AddRange(data);

                    if (_rxBuffer.Count >= TOTAL_SIZE)
                    {
                        _isReceivingData = false;
                        _dataTcs?.TrySetResult(_rxBuffer.Take(TOTAL_SIZE).ToArray());
                    }

                    return;
                }

                // Nếu chưa nhận data → check ACK
                if (data.Length == 1 && data[0] == 0x06)
                {
                    _ackTcs?.TrySetResult(true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateData error: {ex.Message}");
            }
        }

        private void BtnMeterReading_Click(object sender, RoutedEventArgs e)
        {
            type_chart = "meter_reading";
            DrawChart();
        }

        private void BtnWaterUsed_Click(object sender, RoutedEventArgs e)
        {
            type_chart = "water_used";
            DrawChart();
        }

        private async void btnReadData_Click(object sender, RoutedEventArgs e)
        {
            MainWindow main = (MainWindow)Application.Current.MainWindow;

            if (!main.IsConnected)
            {
                MessageBox.Show("Chưa kết nối COM!");
                return;
            }

            for (int i = 0; i < 50; i++)
            {
                _ackTcs = new TaskCompletionSource<bool>();

                _mainWindow?.SendData(new byte[]
                {
                    (byte)MainWindow.CommandCode.READ_DATA_EPPROM
                });

                var completed = await Task.WhenAny(_ackTcs.Task, Task.Delay(100));

                if (completed == _ackTcs.Task && await _ackTcs.Task)
                {
                    break;
                }

                await Task.Delay(20);
            }

            if (_ackTcs == null || !_ackTcs.Task.IsCompleted)
            {
                MessageBox.Show("Không có phản hồi!");
                return;
            }

            // ====== STEP 2: Bắt đầu nhận DATA ======
            _rxBuffer.Clear();
            _dataTcs = new TaskCompletionSource<byte[]>();
            _isReceivingData = true;

            var dataCompleted = await Task.WhenAny(_dataTcs.Task, Task.Delay(3000));

            if (dataCompleted != _dataTcs.Task)
            {
                _isReceivingData = false;

                int receivedBytes = _rxBuffer.Count;
                int receivedPack = receivedBytes / PACK_SIZE;

                MessageBox.Show(
                    $"Timeout!\n" +
                    $"Đã nhận: {receivedPack}/{TOTAL_PACK} pack\n" +
                    $"({receivedBytes} bytes)",
                    "Cảnh báo");

                // 👉 Parse phần nhận được luôn để debug
                if (receivedPack > 0)
                {
                    var partial = _rxBuffer.Take(receivedPack * PACK_SIZE).ToArray();
                    var parses = ParseData(partial);

                    MessageBox.Show($"Đã parse được {parses.Count} mẫu (partial)");
                }

                return;
            }

            byte[] raw = await _dataTcs.Task;

            // ====== STEP 3: Parse ======
            var parsed = ParseData(raw);

            dataPoints.Clear();
            dataPoints.AddRange(parsed);

            DrawChart();
            txtSumWaterUsed.Text = $"Tổng lượng nước sử dụng: {dataPoints.Sum(dp => dp.Used)} l";
        }
        private List<DataPoint> ParseData(byte[] raw)
        {
            var list = new List<DataPoint>();
            var sb = new StringBuilder();   // 👈 gom log

            int totalPack = raw.Length / PACK_SIZE;

            sb.AppendLine($"Nhận OK: {totalPack} mẫu!");

            for (int i = 0; i < totalPack; i++)
            {
                int offset = i * PACK_SIZE;

                byte[] packet = new byte[PACK_SIZE];
                Array.Copy(raw, offset, packet, 0, PACK_SIZE);

                byte hour = packet[0];
                byte min = packet[1];
                byte day = packet[2];
                byte month = packet[3];
                byte year = packet[4];

                ushort used = BitConverter.ToUInt16(packet, 5);
                uint index = BitConverter.ToUInt32(packet, 7);

                ushort crc_rx = BitConverter.ToUInt16(packet, 11);
                ushort crc_calc = MainWindow.ModbusCRC(packet, 11);
                bool crc_check = false;
                if (crc_rx == crc_calc)
                {
                    crc_check = true;
                }
                try
                {
                    DateTime time = new DateTime(2000 + year, month, day, hour, min, 0);

                    string line = $"[{hour:D2}:{min:D2} {day:D2}/{month:D2}/20{year:D2}]: Đã sử dụng: {used:D5} L - Chỉ số công tơ: {index:D7} - CRC: {(crc_check ? "OK" : "FAIL")} {crc_rx:X4} vs {crc_calc:X4}";

                    sb.AppendLine(line);   // 👈 cộng dồn

                    list.Add(new DataPoint
                    {
                        Time = time,
                        Used = used,
                        Index = index
                    });
                }
                catch
                {
                    sb.AppendLine($"[PACK {i}] ERROR:{(BitConverter.ToString(packet.ToArray()))}");
                }
            }

            var window = new Window
            {
                Title = "Thông báo",
                Width = 600,
                Height = 400
            };

            var textBox = new TextBox
            {
                Text = sb.ToString(),
                IsReadOnly = true,
                Padding = new System.Windows.Thickness(10),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };

            window.Content = textBox;
            window.Show();

            //MessageBox.Show(sb.ToString(), "Thông báo", MessageBoxButton.OK);

            return list;
        }

        private void btnImportData_Click(object sender, RoutedEventArgs e)
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
                                    point.Time = timeCell.GetDateTime();
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
                                        point.Time = parsedTime;
                                    }
                                    else
                                    {
                                        point.Time = DateTime.MinValue;
                                    }
                                }

                                // Đọc cột 2: Đã sử dụng (Used) - uint
                                var usedCell = worksheet.Cell(row, 2);
                                if (usedCell.DataType == XLDataType.Number)
                                {
                                    point.Used = (ushort)usedCell.GetDouble();
                                }
                                else
                                {
                                    ushort parsedUsed;
                                    if (ushort.TryParse(usedCell.GetString(), out parsedUsed))
                                    {
                                        point.Used = parsedUsed;
                                    }
                                    else
                                    {
                                        point.Used = 0;
                                    }
                                }

                                // Đọc cột 3: Chỉ số công tơ (Index) - uint
                                var indexCell = worksheet.Cell(row, 3);
                                if (indexCell.DataType == XLDataType.Number)
                                {
                                    point.Index = (uint)indexCell.GetDouble();
                                }
                                else
                                {
                                    uint parsedIndex;
                                    if (uint.TryParse(indexCell.GetString(), out parsedIndex))
                                    {
                                        point.Index = parsedIndex;
                                    }
                                    else
                                    {
                                        point.Index = 0;
                                    }
                                }
                                importedPoints.Add(point);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Lỗi đọc dòng {row}: {ex.Message}", "Lỗi",
                                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }

                    if (PreviewAndImport(importedPoints))
                    {
                        dataPoints.Clear();
                        dataPoints.AddRange(importedPoints);
                        MessageBox.Show($"Đã import thành công {dataPoints.Count} dòng dữ liệu!",
                                        "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                        DrawChart();
                        txtSumWaterUsed.Text = $"Tổng lượng nước sử dụng: {dataPoints.Sum(dp => dp.Used)} l";
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
                sb.AppendLine($"[{point.Time:HH:mm dd/MM/yyyy}]: Đã sử dụng: {point.Used,5} L - Chỉ số công tơ: {point.Index,7}");
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

            // TextBox hiển thị dữ liệu
            TextBox textBox = new TextBox
            {
                Text = sb.ToString(),
                IsReadOnly = true,
                Padding = new System.Windows.Thickness(10),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Height = 380
            };

            // Panel chứa 2 nút
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
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

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(textBox);
            Grid.SetRow(textBox, 0);

            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 1);

            previewWindow.Content = grid;

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


        private void btnExportData_Click(object sender, RoutedEventArgs e)
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
                        worksheet.Cell(1, 2).Value = "Đã sử dụng (L)";
                        worksheet.Cell(1, 3).Value = "Chỉ số công tơ";

                        // Định dạng header (in đậm, màu nền)
                        var headerRange = worksheet.Range("A1:C1");
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        // GHI DỮ LIỆU ===
                        for (int i = 0; i < dataPoints.Count; i++)
                        {
                            var point = dataPoints[i];
                            int row = i + 2;

                            worksheet.Cell(row, 1).Value = point.Time;
                            worksheet.Cell(row, 2).Value = point.Used;
                            worksheet.Cell(row, 3).Value = point.Index;
                        }

                        // ĐỊNH DẠNG CỘT ===
                        // Cột thời gian: định dạng ngày giờ
                        worksheet.Column(1).Style.DateFormat.Format = "HH:mm dd/MM/yyyy";
                        worksheet.Column(2).Style.NumberFormat.Format = "#,##0";
                        worksheet.Column(3).Style.NumberFormat.Format = "#,##0";
                        worksheet.Columns().AdjustToContents();

                        //THÊM BẢNG TÍNH TỔNG ===
                        int lastRow = dataPoints.Count + 1;
                        worksheet.Cell(lastRow + 1, 1).Value = "Tổng:";
                        worksheet.Cell(lastRow + 1, 1).Style.Font.Bold = true;

                        worksheet.Cell(lastRow + 1, 2).FormulaA1 = $"=SUM(B2:B{lastRow})";
                        worksheet.Cell(lastRow + 1, 2).Style.Font.Bold = true;
                        worksheet.Cell(lastRow + 1, 2).Style.NumberFormat.Format = "#,##0";

                        worksheet.Cell(lastRow + 1, 3).FormulaA1 = $"=C{lastRow} - C2 + B2";
                        worksheet.Cell(lastRow + 1, 3).Style.Font.Bold = true;
                        worksheet.Cell(lastRow + 1, 3).Style.NumberFormat.Format = "#,##0";

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

        private void DrawChartWaterUsed()
        {
            var values = new ChartValues<DateTimePoint>(
                dataPoints.Select(x => new DateTimePoint(x.Time, x.Used))
            );

            WaterChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Values = values,
                    LineSmoothness = 0,
                    PointGeometry = null
                }
            };

            ChartTitle.Text = $"Lượng nước sử dụng";

            // Trục X = thời gian
            WaterChart.AxisX.Clear();
            WaterChart.AxisX.Add(new Axis
            {
                LabelFormatter = value => new DateTime((long)value).ToString("HH:mm dd/MM/yyyy"),
                Title = "Thời gian"
            });

            // Trục Y
            WaterChart.AxisY.Clear();
            WaterChart.AxisY.Add(new Axis
            {
                Title = "Lượng nước (l)",
                MinValue = 0,
            });
        }

        private void DrawChartMeterReading()
        {
            var values = new ChartValues<DateTimePoint>(
                dataPoints.Select(x => new DateTimePoint(x.Time, x.Index))
            );

            WaterChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Values = values,
                    LineSmoothness = 0,
                    PointGeometry = null
                }
            };

            ChartTitle.Text = $"Chỉ số đồng hồ nước";

            // Trục X = thời gian
            WaterChart.AxisX.Clear();
            WaterChart.AxisX.Add(new Axis
            {
                LabelFormatter = value => new DateTime((long)value).ToString("HH:mm dd/MM/yyyy"),
                Title = "Thời gian"
            });

            // Trục Y
            WaterChart.AxisY.Clear();
            WaterChart.AxisY.Add(new Axis
            {
                Title = "Lượng nước (l)"
            });
        }

        private void ClearChart()
        {
            ChartTitle.Text = "";
            WaterChart.Series.Clear();
            WaterChart.AxisX.Clear();
            WaterChart.AxisY.Clear();
        }

        private void DrawChart()
        {
            ClearChart();
            if (type_chart == "water_used")
            {
                DrawChartWaterUsed();
            }
            else if (type_chart == "meter_reading")
            {
                DrawChartMeterReading();
            }
            else
            {
                ClearChart();
            }
        }
    }
}
