using LiveCharts;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ultrasonic_watermeter
{
    public partial class Page3 : Page
    {
        private MainWindow? _mainWindow;

        private double FCLKIN, FCALIBCLK, F1, FPT, F0, T1, T0;
        private int COARSECNTROVF, STOPMASK, CLOCKCNTROVF;
        private int TIMINGREG;

        int second, minute, hour, day, month, year;
        private DispatcherTimer _autoTimeTimer;

        private TaskCompletionSource<bool>? _ackTcs;
        public Page3()
        {
            InitializeComponent();
            Loaded += Page3_Loaded;
            LoadComboBox();

            // Khởi tạo timer
            _autoTimeTimer = new DispatcherTimer();
            _autoTimeTimer.Interval = TimeSpan.FromSeconds(1);
            _autoTimeTimer.Tick += AutoTimeTimer_Tick;
        }
        private void Page3_Loaded(object sender, RoutedEventArgs e)
        {
            _mainWindow = Window.GetWindow(this) as MainWindow;
        }
        // Hàm này được gọi từ MainWindow khi có dữ liệu
        public void UpdateData(byte[] data)
        {
            try
            {
                if (data.Length == 1 && data[0] == 0x06)
                {
                    // Đánh thức Task đang chờ
                    _ackTcs?.TrySetResult(true);
                }
                else
                {
                    //MessageBox.Show($"Received: {(BitConverter.ToString(data.ToArray()))}", "Debug");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Page4 error: {ex.Message}");
            }
        }
        private void LoadComboBox()
        {
            cmbTrig1.ItemsSource = new string[] { "Sườn lên", "Sườn xuống" };
            cmbTrig2.ItemsSource = new string[] { "Sườn lên", "Sườn xuống" };
            cmbStart.ItemsSource = new string[] { "Sườn lên", "Sườn xuống" };
            cmbStop.ItemsSource = new string[] { "Sườn lên", "Sườn xuống" };
            cmbMeasModeToF.ItemsSource = new string[] { "Từ 12 ns đến 500 ns", "Từ 250 ns đến 8 ms" };
            cmbCalibration2Periods.ItemsSource = new string[] { "Đo 2 chu kỳ", "Đo 10 chu kỳ", "Đo 20 chu kỳ", "Đo 40 chu kỳ" };
            cmbAvgCycles.ItemsSource = new string[] { "Đo 1 chu kỳ", "Đo 2 chu kỳ", "Đo 4 chu kỳ", "Đo 8 chu kỳ", "Đo 16 chu kỳ", "Đo 32 chu kỳ", "Đo 64 chu kỳ", "Đo 128 chu kỳ" };
            cmbNumStop.ItemsSource = new string[] { "1 xung STOP", "2 xung STOP", "3 xung STOP", "4 xung STOP", "5 xung STOP" };

            cmbTXFreqDiv.ItemsSource = new string[] { "Chia cho 2", "Chia cho 4", "Chia cho 8", "Chia cho 16", "Chia cho 32", "Chia cho 64", "Chia cho 128", "Chia cho 256" };
            cmbTempClkDiv.ItemsSource = new string[] { "Chia cho 8", "Dùng bộ chia của đầu dò" };
            cmbClockinDiv.ItemsSource = new string[] { "Chia cho 1", "Chia cho 2" };
            cmbNumTX.ItemsSource = new string[] { "0 xung", "1 xung", "2 xung", "3 xung", "4 xung", "5 xung", "6 xung", "7 xung", "8 xung", "9 xung", "10 xung", "11 xung", "12 xung", "13 xung", "14 xung", "15 xung", "16 xung", "17 xung", "18 xung", "19 xung", "20 xung", "21 xung", "22 xung", "23 xung", "24 xung", "25 xung", "26 xung", "27 xung", "28 xung", "29 xung", "30 xung", "31 xung" };
            cmbTXPHShiftPos.ItemsSource = new string[] { "Vị trí 0", "Vị trí 1", "Vị trí 2", "Vị trí 3", "Vị trí 4", "Vị trí 5", "Vị trí 6", "Vị trí 7", "Vị trí 8", "Vị trí 9", "Vị trí 10", "Vị trí 11", "Vị trí 12", "Vị trí 13", "Vị trí 14", "Vị trí 15", "Vị trí 16", "Vị trí 17", "Vị trí 18", "Vị trí 19", "Vị trí 20", "Vị trí 21", "Vị trí 22", "Vị trí 23", "Vị trí 24", "Vị trí 25", "Vị trí 26", "Vị trí 27", "Vị trí 28", "Vị trí 29", "Vị trí 30", "Vị trí 31" };
            cmbReceiveMode.ItemsSource = new string[] { "Đơn echo", "Đa echo" };
            cmbNumRX.ItemsSource = new string[] { "32 xung", "1 xung", "2 xung", "3 xung", "4 xung", "5 xung", "6 xung", "7 xung" };
            cmbNumAvg.ItemsSource = new string[] { "Đo 1 chu kỳ", "Đo 2 chu kỳ", "Đo 4 chu kỳ", "Đo 8 chu kỳ", "Đo 16 chu kỳ", "Đo 32 chu kỳ", "Đo 64 chu kỳ", "Đo 128 chu kỳ" };
            cmbToFMeasMode.ItemsSource = new string[] { "TX1 phát - RX2 thu, TX2 phát - RX1 thu", "TX1 phát - RX1 thu, TX2 phát - RX2 thu" };
            cmbTempMode.ItemsSource = new string[] { "Đo 2 kênh", "Đo kênh 1" };
            cmbTempRTD.ItemsSource = new string[] { "PT1000", "PT500" };
            cmbPgaGain.ItemsSource = new string[] { "0 dB", "3 dB", "6 dB", "9 dB", "12 dB", "15 dB", "18 dB", "21 dB" };
            cmbLnaFb.ItemsSource = new string[] { "Tụ điện", "Điện trở" };
            cmbEchoQualThld.ItemsSource = new string[] { "–35 mV", "–50 mV", "–75 mV", "–125 mV", "–220 mV", "–410 mV", "–775 mV", "–1500 mV" };
            cmbTimeToF.ItemsSource = new string[] { "Đo ToF ngắn", "Đo ToF tiêu chuẩn" };
            cmbShortToFBlankPeriod.ItemsSource = new int[] { 8, 16, 32, 64, 128, 256, 512, 1024 };
            cmbToFTimeoutCtrl.ItemsSource = new int[] { 128, 256, 512, 1024 };
            cmbAutoZeroPeriod.ItemsSource = new int[] { 64, 128, 256, 512 };

            cmbDay.ItemsSource = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 };
            cmbMonth.ItemsSource = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            cmbYear.ItemsSource = new List<int> { 2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027, 2028, 2029, 2030 };
            cmbHour.ItemsSource = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
            cmbMinute.ItemsSource = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59 };
            cmbSecond.ItemsSource = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59 };

            cmbMeasFlowCycle.ItemsSource = new string[] { "100ms", "200ms", "300ms", "400ms", "500ms", "600ms", "700ms", "800ms", "900ms", "1s", "2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "10s" };
            cmbWriteDataCycle.ItemsSource = new string[] { "1 giờ", "2 giờ", "3 giờ", "4 giờ" };
            cmbSendDataCycle.ItemsSource = new string[] { "1 ngày" };
        }

        private void sldFCalibClk_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double newValue = e.NewValue;
            double roundedValue = Math.Round(newValue);
            if (Math.Abs(newValue - roundedValue) <= 0.1)
            {
                if (Math.Abs(sldFCalibClk.Value - roundedValue) > 0.01)
                {
                    sldFCalibClk.Value = roundedValue;
                }
            }
            FCALIBCLK = sldFCalibClk.Value;
            if (txtFCalibClk != null)
            {
                txtFCalibClk.Text = $" = {FCALIBCLK:F3} MHz";
            }
            SyncFrequency();
            UpdateCoarseCntrOvf();
            UpdateClockCntrOvf();
            UpdateStopMask();
        }
        private void cmbMeasModeToF_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbMeasModeToF.SelectedIndex == 0)
            {
                spCoarseCntrOvf.IsHitTestVisible = true;
                spCoarseCntrOvf.Opacity = 1;
                spStopMask.IsHitTestVisible = false;
                spStopMask.Opacity = 0.5;
                spClockCntrOvf.IsHitTestVisible = false;
                spClockCntrOvf.Opacity = 0.5;
            }
            else if (cmbMeasModeToF.SelectedIndex == 1)
            {
                spCoarseCntrOvf.IsHitTestVisible = false;
                spCoarseCntrOvf.Opacity = 0.5;
                spStopMask.IsHitTestVisible = true;
                spStopMask.Opacity = 1;
                spClockCntrOvf.IsHitTestVisible = true;
                spClockCntrOvf.Opacity = 1;
            }
        }
        private void sldFClkIn_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double newValue = e.NewValue;
            double roundedValue = Math.Round(newValue);
            if (Math.Abs(newValue - roundedValue) <= 0.1)
            {
                if (Math.Abs(sldFClkIn.Value - roundedValue) > 0.01)
                {
                    sldFClkIn.Value = roundedValue;
                }
            }
            FCLKIN = sldFClkIn.Value;
            if (txtFClkIn != null)
            {
                txtFClkIn.Text = $" = {FCLKIN:F3} MHz";
            }
            UpdateF1();
            UpdateFPT();
            UpdateF0();
            UpdateCommonModeTime();
            UpdateAutoZeroPeriodTime();
            UpdateTransmitTime();
            UpdateShortToFBlankPeriodTime();
            UpdateToFTimeoutCtrlTime();
            UpdateEndTime();
            UpdateTimingRegTime();
        }

        private void cmbTXFreqDiv_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateF1();
            UpdateFPT();
            UpdateTransmitTime();
            UpdateEndTime();
        }
        private void cmbTempClkDiv_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFPT();
        }
        private void cmbClockinDiv_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateF0();
            UpdateCommonModeTime();
            UpdateAutoZeroPeriodTime();
            UpdateShortToFBlankPeriodTime();
            UpdateToFTimeoutCtrlTime();
            UpdateTimingRegTime();
        }
        private void chkSyncFrequency_Checked(object sender, RoutedEventArgs e)
        {
            SyncFrequency();
        }
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            // chuyển tab
            tabControl.SelectedItem = Tab1;
            sldFCalibClk.Focus();
            // focus vào control (sau khi chuyển tab)
        }
        private void cmbNumTX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTransmitTime();
        }
        private void fillCoarseCntrOvf_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCoarseCntrOvf();
        }

        private void fillClockCntrOvf_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateClockCntrOvf();
        }

        private void fillStopMask_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStopMask();
        }
        private void cmbTimeToF_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SortTimeList();
        }
        private void chkEnPowerBlank_Click(object sender, RoutedEventArgs e)
        {
            SortTimeList();
        }
        private void cmbAutoZeroPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAutoZeroPeriodTime();
        }
        private void cmbShortToFBlankPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateShortToFBlankPeriodTime();
        }
        private void cmbToFTimeoutCtrl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateToFTimeoutCtrlTime();
        }
        private void fillTimingReg_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTimingRegTime();
        }
        private void togAutoTime_Click(object sender, RoutedEventArgs e)
        {
            if (togAutoTime.IsChecked == true)
            {
                _autoTimeTimer.Start(); // Bật timer
                UpdateCurrentTime();
            }
            else
            {
                _autoTimeTimer.Stop(); // Tắt timer
            }
        }
        private void AutoTimeTimer_Tick(object? sender, EventArgs e)
        {
            UpdateCurrentTime();
        }
        private void cmbMonth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDaysInMonth();
        }

        private void cmbYear_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDaysInMonth();
        }
        private void btnDefault_Click(object sender, RoutedEventArgs e)
        {
            sldFCalibClk.Value = 8.000;

            togForceCal.IsChecked = false;
            togParity.IsChecked = false;
            cmbTrig1.SelectedItem = "Sườn lên";
            cmbTrig2.SelectedItem = "Sườn lên";
            cmbStart.SelectedItem = "Sườn lên";
            cmbStop.SelectedItem = "Sườn lên";
            cmbMeasModeToF.SelectedItem = "Từ 250 ns đến 8 ms";
            cmbCalibration2Periods.SelectedItem = "Đo 10 chu kỳ";
            cmbAvgCycles.SelectedItem = "Đo 1 chu kỳ";
            cmbNumStop.SelectedItem = "5 xung STOP";
            togCoarseOverflowInterrupt.IsChecked = true;
            togClockOverflowInterrupt.IsChecked = true;
            fillCoarseCntrOvf.Text = "65535";
            fillStopMask.Text = "396";
            fillClockCntrOvf.Text = "652";

            sldFClkIn.Value = 8.000;
            cmbTXFreqDiv.SelectedItem = "Chia cho 8";
            chkSyncFrequency.IsChecked = true;
            cmbTempClkDiv.SelectedItem = "Chia cho 8";
            cmbClockinDiv.SelectedItem = "Chia cho 1";
            cmbNumTX.SelectedItem = "5 xung";
            togDamping.IsChecked = false;
            cmbTXPHShiftPos.SelectedItem = "Vị trí 31";
            cmbReceiveMode.SelectedItem = "Đơn echo";
            cmbNumRX.SelectedItem = "5 xung";
            cmbNumAvg.SelectedItem = "Đo 1 chu kỳ";
            cmbToFMeasMode.SelectedItem = "TX1 phát - RX1 thu, TX2 phát - RX2 thu";
            cmbTempMode.SelectedItem = "Đo 2 kênh";
            cmbTempRTD.SelectedItem = "PT1000";
            cmbPgaGain.SelectedItem = "21 dB";
            togPgaCtrl.IsChecked = false;
            cmbLnaFb.SelectedItem = "Tụ điện";
            togLnaCtrl.IsChecked = false;
            cmbEchoQualThld.SelectedItem = "–125 mV";
            cmbTimeToF.SelectedItem = "Đo ToF tiêu chuẩn";
            chkEnPowerBlank.IsChecked = false;
            cmbShortToFBlankPeriod.SelectedItem = "64";
            cmbToFTimeoutCtrl.SelectedItem = 128;
            togToFTimeoutCtrl.IsChecked = false;
            cmbAutoZeroPeriod.SelectedItem = 128;
            cmbShortToFBlankPeriod.SelectedItem = 64; ;
            fillTimingReg.Text = "62";

            togAutoTime.IsChecked = true;
            _autoTimeTimer.Start();
            cmbMeasFlowCycle.SelectedItem = "1s";
            cmbWriteDataCycle.SelectedItem = "1 giờ";
            cmbSendDataCycle.SelectedItem = "1 ngày";
        }
        private async void btnUseConfigure_Click(object sender, RoutedEventArgs e)
        {
            MainWindow main = (MainWindow)Application.Current.MainWindow;

            if (!main.IsConnected)   // bạn cần expose property này
            {
                MessageBox.Show("Chưa kết nối cổng COM!", "Cảnh báo");
                return;
            }

            if (IsValidInput() == true)
            {
                for (int i = 0; i < 50; i++)
                {
                    // Tạo TaskCompletionSource mới
                    _ackTcs = new TaskCompletionSource<bool>();
                    // Gửi command
                    _mainWindow?.SendData(command((byte)MainWindow.CommandCode.USE_CONFIG).ToArray());
                    // Chờ ACK hoặc timeout 1 giây
                    var completedTask = await Task.WhenAny(_ackTcs.Task, Task.Delay(100));

                    if (completedTask == _ackTcs.Task && await _ackTcs.Task)
                    {
                        MessageBox.Show("Cấu hình đã được gửi thành công!", "Thành công",
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    await Task.Delay(50);
                }
                MessageBox.Show("Không nhận được phản hồi xác nhận từ thiết bị. Vui lòng kiểm tra kết nối và thử lại.",
                                "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

        private async void btnSaveConfigure_Click(object sender, RoutedEventArgs e)
        {
            MainWindow main = (MainWindow)Application.Current.MainWindow;

            if (!main.IsConnected)   // bạn cần expose property này
            {
                MessageBox.Show("Chưa kết nối cổng COM!", "Cảnh báo");
                return;
            }

            if (IsValidInput() == true)
            {
                for (int i = 0; i < 50; i++)
                {
                    // Tạo TaskCompletionSource mới
                    _ackTcs = new TaskCompletionSource<bool>();
                    // Gửi command
                    _mainWindow?.SendData(command((byte)MainWindow.CommandCode.SAVE_CONFIG).ToArray());
                    // Chờ ACK hoặc timeout 1 giây
                    var completedTask = await Task.WhenAny(_ackTcs.Task, Task.Delay(100));

                    if (completedTask == _ackTcs.Task && await _ackTcs.Task)
                    {
                        MessageBox.Show("Cấu hình đã được gửi thành công!", "Thành công",
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                MessageBox.Show("Không nhận được phản hồi xác nhận từ thiết bị. Vui lòng kiểm tra kết nối và thử lại.",
                                "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateF1()
        {
            if (cmbTXFreqDiv == null || txtF1 == null)
                return;

            if (cmbTXFreqDiv.SelectedIndex == -1)
                return;
            int divisor = (int)Math.Pow(2, cmbTXFreqDiv.SelectedIndex + 1);
            F1 = FCLKIN / divisor;
            txtF1.Text = $" = {F1:F3} MHz";
            T1 = 1000 / F1;
            if (T1 < 1000)
                txtT1.Text = $"T1 = {T1:F3} ns";
            else if (T1 < 1000000)
                txtT1.Text = $"T1 = {T1 / 1000:F3} μs";
            else
                txtT1.Text = $"T1 = {T1 / 1000000:F3} ms";
        }
        private void UpdateFPT()
        {
            if (cmbTempClkDiv == null || txtFPT == null)
                return;
            if (cmbTempClkDiv.SelectedIndex == -1)
                return;
            if (cmbTempClkDiv.SelectedItem.ToString() == "Chia cho 8")
            {
                FPT = FCLKIN / 8;
                txtFPT.Text = $" = {FPT:F3} MHz";
            }
            else if (cmbTempClkDiv.SelectedItem.ToString() == "Dùng bộ chia của đầu dò")
            {
                int divisor = (int)Math.Pow(2, cmbTXFreqDiv.SelectedIndex + 1);
                FPT = FCLKIN / divisor;
                txtFPT.Text = $" = {FPT:F3} MHz";
            }
        }
        private void UpdateF0()
        {
            if (cmbClockinDiv == null || txtF0 == null)
                return;
            if (cmbClockinDiv.SelectedIndex == -1)
                return;
            if (cmbClockinDiv.SelectedItem.ToString() == "Chia cho 1")
            {
                F0 = FCLKIN;
            }
            else if (cmbClockinDiv.SelectedItem.ToString() == "Chia cho 2")
            {
                F0 = FCLKIN / 2;
            }
            txtF0.Text = $" = {F0:F3} MHz";
            T0 = 1000 / F0;
            if (T0 < 1000)
                txtT0.Text = $"T0 = {T0:F3} ns";
            else if (T0 < 1000000)
                txtT0.Text = $"T0 = {T0 / 1000:F3} μs";
            else
                txtT0.Text = $"T0 = {T0 / 1000000:F3} ms";
        }
        private void SyncFrequency()
        {
            if (chkSyncFrequency == null ||
                sldFClkIn == null ||
                sldFCalibClk == null)
                return;

            if (chkSyncFrequency.IsChecked != true)
                return;

            FCLKIN = FCALIBCLK;

            if (txtFClkIn != null)
                txtFClkIn.Text = $" = {FCLKIN:F3} MHz";

            UpdateF1();
            UpdateFPT();
            UpdateF0();
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox? textBox = sender as TextBox;
            if (textBox != null && !string.IsNullOrEmpty(textBox.Text))
            {
                if (!textBox.Text.All(char.IsDigit))
                {
                    MessageBox.Show("Vui lòng chỉ nhập số tự nhiên!", "Lỗi nhập liệu",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    textBox.Text = string.Empty;
                    return;
                }
                if (long.Parse(textBox.Text) > 65535)
                {
                    MessageBox.Show("Giá trị phải nhỏ hơn hoặc bằng 65535!", "Lỗi nhập liệu",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    textBox.Text = string.Empty;
                    return;
                }
            }
        }
        private void fillTimingReg_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox? textBox = sender as TextBox;
            if (textBox != null && !string.IsNullOrEmpty(textBox.Text))
            {
                if (!textBox.Text.All(char.IsDigit))
                {
                    MessageBox.Show("Vui lòng chỉ nhập số tự nhiên!", "Lỗi nhập liệu",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    textBox.Text = string.Empty;
                }
                else
                {
                    long value = long.Parse(textBox.Text);
                    if (value > 1023)
                    {
                        MessageBox.Show("Giá trị phải nhỏ hơn hoặc bằng 1023!", "Lỗi nhập liệu",
                                        MessageBoxButton.OK, MessageBoxImage.Warning);
                        textBox.Text = string.Empty;
                    }
                    else if (value < 30)
                    {
                        var result = MessageBox.Show(
                            "Giá trị nhỏ hơn 30 thì sẽ chuyển sang chế độ Đo ToF ngắn!",
                            "Xác nhận",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        if (result == MessageBoxResult.No)
                        {
                            textBox.Text = string.Empty;
                        }
                        else
                        {
                            textBox.Text = string.Empty;
                            cmbTimeToF.SelectedItem = "Đo ToF ngắn";
                            cmbTimeToF.Focus();
                        }
                    }
                }
            }
        }
        private void UpdateCoarseCntrOvf()
        {
            if (fillCoarseCntrOvf == null)
            {
                return;
            }
            if (int.TryParse(fillCoarseCntrOvf.Text, out int value))
            {
                COARSECNTROVF = value;
            }
            if (string.IsNullOrWhiteSpace(fillCoarseCntrOvf.Text))
            {
                timeCoarseCntrOvf.Text = "x 63 x LSB = ";
                return;
            }
            else
            {
                double time = COARSECNTROVF * 63 * 0.055f;
                if (time < 1000)
                {
                    timeCoarseCntrOvf.Text = $"x 63 x LSB = {time:F3} ns";
                }
                else
                {
                    timeCoarseCntrOvf.Text = $"x 63 x LSB = {time / 1000:F3} μs";
                }
            }
        }
        private void UpdateClockCntrOvf()
        {
            if (fillClockCntrOvf == null)
            {
                return;
            }
            if (int.TryParse(fillClockCntrOvf.Text, out int value))
            {
                CLOCKCNTROVF = value;
            }
            if (string.IsNullOrWhiteSpace(fillClockCntrOvf.Text))
            {
                txtClockCntrOvf.Text = $" = ";
                return;
            }
            else
            {
                double CLOCKperiod = 1000f / FCALIBCLK;
                double time = CLOCKCNTROVF * CLOCKperiod;
                if (time < 1000)
                {
                    txtClockCntrOvf.Text = $" = {time:F3} ns";
                }
                else if (time < 1000000)
                {
                    txtClockCntrOvf.Text = $" = {time / 1000:F3} μs";
                }
                else
                {
                    txtClockCntrOvf.Text = $" = {time / 1000000:F3} ms";
                }
            }
        }
        private void UpdateStopMask()
        {
            if (fillStopMask == null)
            {
                return;
            }
            if (int.TryParse(fillStopMask.Text, out int value))
            {
                STOPMASK = value;
            }
            if (string.IsNullOrWhiteSpace(fillStopMask.Text))
            {
                txtStopMask.Text = $" = ";
                return;
            }
            else
            {
                double CLOCKperiod = 1000f / FCALIBCLK;
                double time = STOPMASK * CLOCKperiod;
                if (time < 1000)
                {
                    txtStopMask.Text = $" = {time:F3} ns";
                }
                else if (time < 1000000)
                {
                    txtStopMask.Text = $" = {time / 1000:F3} μs";
                }
                else
                {
                    txtStopMask.Text = $" = {time / 1000000:F3} ms";
                }
            }
        }
        private void SortTimeList()
        {
            if (cmbTimeToF == null || cmbTimeToF.SelectedIndex == -1)
                return;
            else
            {
                if (cmbTimeToF.SelectedIndex == 0)
                {
                    Grid.SetRow(spCommonMode, 0);
                    Grid.SetRow(spAutoZeroPeriod, 1);
                    Grid.SetRow(spTransmitTime, 2);
                    Grid.SetRow(spShortToFBlankPeriod, 3);
                    Grid.SetRow(spToFTimeoutCtrl, 4);
                    Grid.SetRow(spEnd, 5);
                    Grid.SetRow(spTimingReg, 6);
                    if (string.IsNullOrEmpty(fillTimingReg.Text))
                    {
                        fillTimingReg.Text = "0";
                    }
                    spTimingReg.Visibility = Visibility.Hidden;
                    spShortToFBlankPeriod.Visibility = Visibility.Visible;
                }
                else if (cmbTimeToF.SelectedIndex == 1)
                {
                    if (chkEnPowerBlank.IsChecked == false)
                    {
                        Grid.SetRow(spTransmitTime, 0);
                        Grid.SetRow(spCommonMode, 1);
                        Grid.SetRow(spAutoZeroPeriod, 2);
                        Grid.SetRow(spTimingReg, 3);
                        Grid.SetRow(spToFTimeoutCtrl, 4);
                        Grid.SetRow(spEnd, 5);
                        Grid.SetRow(spShortToFBlankPeriod, 6);
                        txtTimingReg.Text = "Thời gian chờ / nghe sóng nhận:";
                    }
                    else
                    {
                        Grid.SetRow(spTransmitTime, 0);
                        Grid.SetRow(spTimingReg, 1);
                        Grid.SetRow(spCommonMode, 2);
                        Grid.SetRow(spAutoZeroPeriod, 3);
                        Grid.SetRow(spToFTimeoutCtrl, 4);
                        Grid.SetRow(spEnd, 5);
                        Grid.SetRow(spShortToFBlankPeriod, 6);
                        txtTimingReg.Text = "Thời gian chờ quá trình nhận:";
                    }
                    if (cmbShortToFBlankPeriod.SelectedIndex < 0)
                    {
                        cmbShortToFBlankPeriod.SelectedIndex = 3;
                    }
                    spShortToFBlankPeriod.Visibility = Visibility.Hidden;
                    spTimingReg.Visibility = Visibility.Visible;
                }
            }
        }

        private void UpdateCommonModeTime()
        {
            if (cmbClockinDiv == null || cmbClockinDiv.SelectedIndex == -1)
                return;
            double time = 128 * T0;
            if (time < 1000)
                txtCommonModeTime.Text = $"128 x T0 = {time:F3} ns";
            else if (time < 1000000)
                txtCommonModeTime.Text = $"128 x T0 = {time / 1000:F3} μs";
            else
                txtCommonModeTime.Text = $"128 x T0 = {time / 1000000:F3} ms";
        }

        private void UpdateAutoZeroPeriodTime()
        {
            if (cmbClockinDiv == null || cmbClockinDiv.SelectedIndex == -1 || cmbAutoZeroPeriod == null || cmbAutoZeroPeriod.SelectedIndex == -1)
                return;
            double time = (int)cmbAutoZeroPeriod.SelectedItem * T0;
            if (time < 1000)
                txtAutoZeroPeriodTime.Text = $"x T0 = {time:F3} ns";
            else if (time < 1000000)
                txtAutoZeroPeriodTime.Text = $"x T0 = {time / 1000:F3} μs";
            else
                txtAutoZeroPeriodTime.Text = $"x T0 = {time / 1000000:F3} ms";

        }
        private void UpdateTransmitTime()
        {
            if (cmbTXFreqDiv == null || cmbTXFreqDiv.SelectedIndex == -1 || cmbNumTX == null || cmbNumTX.SelectedIndex == -1)
                return;
            double time = cmbNumTX.SelectedIndex * T1;
            if (time < 1000)
                txtTransmitTime.Text = $"{cmbNumTX.SelectedIndex} x T1 = {time:F3} ns";
            else if (time < 1000000)
                txtTransmitTime.Text = $"{cmbNumTX.SelectedIndex} x T1 = {time / 1000:F3} μs";
            else
                txtTransmitTime.Text = $"{cmbNumTX.SelectedIndex} x T1 = {time / 1000000:F3} ms";
        }
        private void UpdateShortToFBlankPeriodTime()
        {
            if (cmbClockinDiv == null || cmbClockinDiv.SelectedIndex == -1 || cmbShortToFBlankPeriod == null || cmbShortToFBlankPeriod.SelectedIndex == -1)
                return;
            double time = (int)cmbShortToFBlankPeriod.SelectedItem * T0;
            if (time < 1000)
                txtShortToFBlankPeriodTime.Text = $"x T0 = {time:F3} ns";
            else if (time < 1000000)
                txtShortToFBlankPeriodTime.Text = $"x T0 = {time / 1000:F3} μs";
            else
                txtShortToFBlankPeriodTime.Text = $"x T0 = {time / 1000000:F3} ms";
        }
        private void UpdateToFTimeoutCtrlTime()
        {
            if (cmbClockinDiv == null || cmbClockinDiv.SelectedIndex == -1 || cmbToFTimeoutCtrl == null || cmbToFTimeoutCtrl.SelectedIndex == -1)
                return;
            double time = (int)cmbToFTimeoutCtrl.SelectedItem * T0;
            if (time < 1000)
                txtToFTimeoutCtrlTime.Text = $"x T0 = {time:F3} ns";
            else if (time < 1000000)
                txtToFTimeoutCtrlTime.Text = $"x T0 = {time / 1000:F3} μs";
            else
                txtToFTimeoutCtrlTime.Text = $"x T0 = {time / 1000000:F3} ms";
        }
        private void UpdateEndTime()
        {
            if (cmbTXFreqDiv == null || cmbTXFreqDiv.SelectedIndex == -1)
                return;
            double time = T1;
            if (time < 1000)
                txtEndTime.Text = $"1 x T1 = {time:F3} ns";
            else if (time < 1000000)
                txtEndTime.Text = $"1 x T1 = {time / 1000:F3} μs";
            else
                txtEndTime.Text = $"1 x T1 = {time / 1000000:F3} ms";
        }
        private void UpdateTimingRegTime()
        {
            if (cmbClockinDiv == null || cmbClockinDiv.SelectedIndex == -1)
                return;

            if (fillTimingReg == null)
            {
                return;
            }
            if (int.TryParse(fillTimingReg.Text, out int value))
            {
                TIMINGREG = value;
            }
            if (string.IsNullOrWhiteSpace(fillTimingReg.Text))
            {
                txtTimingRegTime.Text = $"- 30 ) x 8 x T0 = ";
                return;
            }

            if (TIMINGREG >= 30)
            {
                double time = (TIMINGREG - 30) * 8 * T0;
                if (time < 1000)
                    txtTimingRegTime.Text = $"- 30 ) x 8 x T0 = {time:F3} ns";
                else if (time < 1000000)
                    txtTimingRegTime.Text = $"- 30 ) x 8 x T0 = {time / 1000:F3} μs";
                else
                    txtTimingRegTime.Text = $"- 30 ) x 8 x T0 = {time / 1000000:F3} ms";
            }
        }
        private void UpdateDaysInMonth()
        {
            if (cmbMonth.SelectedItem == null || cmbYear.SelectedItem == null)
                return;
            int month = (int)cmbMonth.SelectedItem;
            int year = (int)cmbYear.SelectedItem;

            int daysInMonth = DateTime.DaysInMonth(year, month);

            List<int> newDays = new List<int>();

            for (int i = 1; i <= daysInMonth; i++)
            {
                newDays.Add(i);
            }

            // Lưu lại giá trị ngày đang chọn (nếu có)
            int? currentDay = cmbDay.SelectedItem as int?;

            cmbDay.ItemsSource = newDays;
            // Khôi phục lựa chọn cũ nếu vẫn còn trong khoảng ngày mới
            if (currentDay.HasValue && currentDay.Value <= daysInMonth)
            {
                cmbDay.SelectedItem = currentDay.Value;
            }
            else
            {
                cmbDay.SelectedItem = 1;
            }
        }
        private void UpdateCurrentTime()
        {
            DateTime now = DateTime.Now;

            year = now.Year;
            month = now.Month;
            day = now.Day;
            hour = now.Hour;
            minute = now.Minute;
            second = now.Second;

            cmbYear.SelectedItem = year;
            cmbMonth.SelectedItem = month;
            cmbDay.SelectedItem = day;
            cmbHour.SelectedItem = hour;
            cmbMinute.SelectedItem = minute;
            cmbSecond.SelectedItem = second;
        }
        private bool IsValidInput()
        {
            var tabs_1 = new[]
            {
                new { Combos = new[] { cmbTrig1, cmbStart, cmbStop, cmbCalibration2Periods, cmbAvgCycles, cmbMeasModeToF, cmbNumStop }, TabIndex = 0 },
                new { Combos = new[] { cmbTXFreqDiv, cmbTempClkDiv, cmbClockinDiv, cmbTrig2, cmbNumTX, cmbTXPHShiftPos, cmbReceiveMode, cmbNumRX, cmbNumAvg, cmbToFMeasMode, cmbTempMode, cmbTempRTD, cmbLnaFb, cmbPgaGain, cmbEchoQualThld, cmbTimeToF, cmbShortToFBlankPeriod, cmbToFTimeoutCtrl, cmbAutoZeroPeriod }, TabIndex = 1 },
                new { Combos = new[] { cmbDay, cmbMonth, cmbYear, cmbHour, cmbMinute, cmbSecond, cmbMeasFlowCycle, cmbWriteDataCycle, cmbSendDataCycle }, TabIndex = 2 }
            };

            var invalid = tabs_1
                .SelectMany(tab => tab.Combos.Select(combo => new { combo, tab.TabIndex }))
                .FirstOrDefault(x => x.combo.SelectedIndex < 0);

            if (invalid != null)
            {
                MessageBox.Show("Vui lòng chọn đầy đủ cấu hình!", $"Lỗi {invalid.combo.Name}",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                tabControl.SelectedIndex = invalid.TabIndex;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    invalid.combo.BringIntoView();
                    invalid.combo.Focus();
                }), DispatcherPriority.Input);
                return false;
            }

            var tabs_2 = new[]
            {
                new { Fills = new[] { fillCoarseCntrOvf, fillClockCntrOvf, fillStopMask}, TabIndex = 0 },
                new { Fills = new[] { fillTimingReg }, TabIndex = 1 },
            };

            var invalidText = tabs_2
                .SelectMany(tab => tab.Fills.Select(text => new { text, tab.TabIndex }))
                .FirstOrDefault(x => string.IsNullOrWhiteSpace(x.text.Text));

            if (invalidText != null)
            {
                MessageBox.Show("Vui lòng điền đầy đủ thông tin!", $"Lỗi {invalidText.text.Name}",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                tabControl.SelectedIndex = invalidText.TabIndex;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    invalidText.text.BringIntoView();
                    invalidText.text.Focus();
                }), DispatcherPriority.Input);
                return false;
            }
            return true;
        }

        private List<byte> configs()
        {
            List<byte> command = new List<byte>();
            //tab0
            command.Add((byte)((byte)(togForceCal.IsChecked == true ? 1 << 7 : 0) | (byte)(togParity.IsChecked == true ? 1 << 6 : 0) | (byte)(cmbTrig1.SelectedIndex << 5) | (byte)(cmbStop.SelectedIndex << 4) | (byte)(cmbStart.SelectedIndex << 3) | (byte)(cmbMeasModeToF.SelectedIndex << 1)));
            command.Add((byte)((byte)(cmbCalibration2Periods.SelectedIndex << 6) | (byte)(cmbAvgCycles.SelectedIndex << 3) | (byte)(cmbNumStop.SelectedIndex)));
            command.Add(0x1F);
            command.Add((byte)((byte)(togClockOverflowInterrupt.IsChecked == true ? 1 << 2 : 0) | (byte)(togCoarseOverflowInterrupt.IsChecked == true ? 1 << 1 : 0) | 0x01));
            command.Add((byte)(COARSECNTROVF >> 8));
            command.Add((byte)(COARSECNTROVF & 0xFF));
            command.Add((byte)(CLOCKCNTROVF >> 8));
            command.Add((byte)(CLOCKCNTROVF & 0xFF));
            command.Add((byte)(STOPMASK >> 8));
            command.Add((byte)(STOPMASK & 0xFF));
            //tab1
            command.Add((byte)(cmbTXFreqDiv.SelectedIndex << 5 | (byte)(cmbNumTX.SelectedIndex)));
            command.Add((byte)(cmbNumAvg.SelectedIndex << 3 | (byte)(cmbNumRX.SelectedIndex)));
            command.Add((byte)((byte)(togDamping.IsChecked == true ? 1 << 5 : 0) | (byte)(cmbToFMeasMode.SelectedIndex)));
            command.Add((byte)((byte)(cmbTempMode.SelectedIndex << 6) | (byte)(cmbTempRTD.SelectedIndex << 5) | (byte)(cmbTempClkDiv.SelectedIndex << 4) | (byte)(chkEnPowerBlank.IsChecked == true ? 1 << 3 : 0) | (byte)(cmbEchoQualThld.SelectedIndex)));
            command.Add((byte)((byte)(cmbReceiveMode.SelectedIndex << 6) | (byte)(cmbTrig2.SelectedIndex << 5) | (byte)(cmbTXPHShiftPos.SelectedIndex)));
            command.Add((byte)(cmbPgaGain.SelectedIndex << 5 | (byte)(togPgaCtrl.IsChecked == true ? 1 << 4 : 0) | (byte)(togLnaCtrl.IsChecked == true ? 1 << 3 : 0) | (byte)(cmbLnaFb.SelectedIndex << 2) | (byte)(TIMINGREG >> 8)));
            command.Add((byte)(TIMINGREG & 0xFF));
            command.Add(0x07);
            command.Add((byte)(cmbTimeToF.SelectedIndex == 0 ? 1 << 6 : 0 | (byte)(cmbShortToFBlankPeriod.SelectedIndex << 3) | (byte)(togToFTimeoutCtrl.IsChecked == true ? 1 << 2 : 0) | (byte)(cmbToFTimeoutCtrl.SelectedIndex)));
            command.Add((byte)(cmbClockinDiv.SelectedIndex << 2 | (byte)(cmbAutoZeroPeriod.SelectedIndex)));
            //tab2
            byte cycle = (byte)(cmbMeasFlowCycle.SelectedIndex);
            if (cycle < 9)
            {
                command.Add((byte)(cmbMeasFlowCycle.SelectedIndex + 1));
            }
            else
            {
                command.Add((byte)((cmbMeasFlowCycle.SelectedIndex - 8) * 10));
            }

            command.Add((byte)(cmbWriteDataCycle.SelectedIndex + 1));
            command.Add((byte)(cmbSendDataCycle.SelectedIndex + 1));

            command.Add((byte)cmbDay.SelectedIndex);
            command.Add((byte)(cmbMonth.SelectedIndex + 1));
            command.Add((byte)(cmbYear.SelectedIndex + 20));
            command.Add((byte)cmbHour.SelectedIndex);
            command.Add((byte)cmbMinute.SelectedIndex);
            command.Add((byte)cmbSecond.SelectedIndex);

            return command;
        }

        private List<byte> command(byte commandCode)
        {
            List<byte> command = new List<byte>();
            //command code
            command.Add((byte)commandCode);
            command.AddRange(configs());
            return command;
        }
    }
}