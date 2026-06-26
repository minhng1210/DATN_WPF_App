using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Ultrasonic_watermeter
{
    public partial class MainWindow : Window
    {
        private SerialPort _serialPort = new SerialPort();
        public bool IsConnected = false;
        private DispatcherTimer portCheckTimer = null!;

        private List<byte> _rxBuffer = new List<byte>();
        private const byte HEADER = 0xAA;
        private bool _isCheckAck = false;

        // Sự kiện để thông báo cho Page hiện tại
        public event Action<byte[]>? DataReceived;
        public event Action<bool>? ConnectionChanged;

        // Khai báo các Page
        private Page1? _page1;
        private Page2? _page2;
        private Page3? _page3;

        public enum CommandCode : byte
        {
            READ_DATA_EPPROM = 1,
            ENABLE_DATA_REALTIME = 2,
            DISABLE_DATA_REALTIME = 3,
            USE_CONFIG = 4,
            SAVE_CONFIG = 5,
        }

        public MainWindow()
        {
            InitializeComponent();

            // Set kích thước window
            var workingArea = SystemParameters.WorkArea;
            this.Width = workingArea.Width * 0.8;
            this.Height = workingArea.Height * 0.8;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            LoadCombobox();

            LoadDefaultSettings();

            this.Loaded += MainWindow_Loaded;

            _serialPort.ErrorReceived += SerialPort_ErrorReceived;

            // Đăng ký sự kiện đóng cửa sổ
            this.Closing += MainWindow_Closing;
        }
       
        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // Lấy Page từ Frame
            var frame1 = (tabControl.Items[0] as TabItem)?.Content as Frame;
            _page1 = frame1?.Content as Page1;

            var frame2 = (tabControl.Items[1] as TabItem)?.Content as Frame;
            _page2 = frame2?.Content as Page2;

            var frame3 = (tabControl.Items[2] as TabItem)?.Content as Frame;
            _page3 = frame3?.Content as Page3;
        }   

        private void LoadCombobox()
        {
            InitPortMonitor();
            cmbBaud.ItemsSource = new int[] { 1200, 2400, 4800, 9600, 14400, 19200, 28800, 38400, 57600, 115200, 230400 };
            cmbDataBits.ItemsSource = new int[] { 5, 6, 7, 8 };
            cmbStopBits.ItemsSource = new string[] { "1", "1.5", "2" };
            cmbParity.ItemsSource = new string[] { "none", "odd", "even", "mark", "space" };
        }

        private void LoadDefaultSettings()
        {
            cmbBaud.SelectedItem = 115200;
            cmbDataBits.SelectedItem = 8;
            cmbStopBits.SelectedItem = "1";
            cmbParity.SelectedItem = "none";
        }

        private void InitPortMonitor()
        {
            portCheckTimer = new DispatcherTimer();
            portCheckTimer.Interval = TimeSpan.FromSeconds(1);
            string[] lastPorts = new string[0];

            portCheckTimer.Tick += (s, e) =>
            {
                string[] ports = SerialPort.GetPortNames();
                if (!IsConnected)
                {
                    // Chỉ cập nhật danh sách khi có thay đổi
                    if (!ports.SequenceEqual(lastPorts))
                    {
                        // Tìm cổng mới được thêm vào (có trong ports mới nhưng không có trong lastPorts)
                        string? newPort = ports.Except(lastPorts).FirstOrDefault();

                        // Tìm cổng bị mất đi (có trong lastPorts nhưng không có trong ports mới)
                        string? removedPort = lastPorts.Except(ports).FirstOrDefault();

                        lastPorts = ports;
                        string? current = cmbPort.SelectedItem?.ToString();
                        cmbPort.ItemsSource = ports;
                        // ƯU TIÊN 0: Lần đầu khởi động → chọn cổng cuối
                        if (current == null && lastPorts.Length == ports.Length)
                        {
                            if (ports.Length > 0)
                            {
                                var sorted = ports.OrderBy(p => int.Parse(p.Substring(3))).ToArray();
                                cmbPort.SelectedItem = sorted.Last();
                            }
                            return;
                        }
                        // ƯU TIÊN 1: Chọn cổng mới được thêm vào
                        if (newPort != null)
                        {
                            cmbPort.SelectedItem = newPort;
                            return;
                        }
                        // ƯU TIÊN 2: Giữ lại lựa chọn cũ nếu cổng đó vẫn còn
                        if (current != null && ports.Contains(current))
                        {
                            cmbPort.SelectedItem = current;
                            return;
                        }
                        cmbPort.SelectedItem = null;
                    }
                }

                // ========== TRƯỜNG HỢP 2: ĐÃ KẾT NỐI ==========
                else
                {
                    // Lấy tên cổng đang kết nối
                    string connectedPort = _serialPort?.PortName ?? "";
                    // Kiểm tra cổng đang kết nối có còn trong danh sách không
                    if (!string.IsNullOrEmpty(connectedPort) && !ports.Contains(connectedPort))
                    {
                        // Cổng đã bị rút ra! -> Ngắt kết nối
                        if (_serialPort != null && _serialPort.IsOpen)
                            _serialPort.Close();

                        IsConnected = false;
                        btnConnect.Content = "Kết nối";
                        btnConnect.Background = System.Windows.Media.Brushes.LightGreen;

                        panelSerialConfig.IsEnabled = true;
                        panelSerialConfig.Opacity = 1.0;

                        MessageBox.Show($"Cổng {connectedPort} đã mất kết nối!", "Thông báo",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            };
            portCheckTimer.Start();
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsConnected)
                {
                    // Kiểm tra null
                    if (cmbPort.SelectedItem == null || cmbBaud.SelectedItem == null || cmbDataBits.SelectedItem == null || cmbStopBits.SelectedItem == null || cmbParity.SelectedItem == null)
                    {
                        MessageBox.Show("Thiếu cấu hình!", "Lỗi",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    else
                    {
                        _serialPort.PortName = cmbPort.SelectedItem?.ToString();
                        _serialPort.BaudRate = (int)cmbBaud.SelectedItem;
                        _serialPort.DataBits = (int)cmbDataBits.SelectedItem;
                        switch (cmbStopBits.SelectedItem?.ToString())
                        {
                            case "1": _serialPort.StopBits = StopBits.One; break;
                            case "1.5": _serialPort.StopBits = StopBits.OnePointFive; break;
                            case "2": _serialPort.StopBits = StopBits.Two; break;
                        }
                        switch (cmbParity.SelectedItem?.ToString())
                        {
                            case "none": _serialPort.Parity = Parity.None; break;
                            case "odd": _serialPort.Parity = Parity.Odd; break;
                            case "even": _serialPort.Parity = Parity.Even; break;
                            case "mark": _serialPort.Parity = Parity.Mark; break;
                            case "space": _serialPort.Parity = Parity.Space; break;
                        }

                        _serialPort.DataReceived += SerialPort_DataReceived;
                        _serialPort.Open();

                        IsConnected = true;
                        btnConnect.Content = "Ngắt kết nối";
                        btnConnect.Background = System.Windows.Media.Brushes.Red;
                        btnConnect.Foreground = System.Windows.Media.Brushes.Black;

                        txtSerialStatus.Text = "Đã kết nối";
                        txtSerialStatus.Foreground = System.Windows.Media.Brushes.Green;

                        panelSerialConfig.IsEnabled = false;
                        panelSerialConfig.Opacity = 0.5;
                    }
                }
                else
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                        _serialPort.Close();

                    IsConnected = false;
                    btnConnect.Content = "Kết nối";
                    btnConnect.Background = System.Windows.Media.Brushes.LightGreen;
                    btnConnect.Foreground = System.Windows.Media.Brushes.Black;

                    txtSerialStatus.Text = "Chưa kết nối";
                    txtSerialStatus.Foreground = System.Windows.Media.Brushes.Red;

                    panelSerialConfig.IsEnabled = true;
                    panelSerialConfig.Opacity = 1.0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kết nối: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            int bytes = _serialPort.BytesToRead;
            byte[] temp = new byte[bytes];
            _serialPort.Read(temp, 0, bytes);

            lock (_rxBuffer)
            {
                _rxBuffer.AddRange(temp);
            }

            ParseBuffer();
        }

        private void ParseBuffer()
        {
            lock (_rxBuffer)
            {
                while (_rxBuffer.Count >= 5) // Cần ít nhất HEADER + LEN2 + CRC2
                {
                    // Tìm HEADER (ví dụ: 0xAA)
                    int headerIndex = _rxBuffer.IndexOf(HEADER);
                    if (headerIndex == -1)
                    {
                        // Không tìm thấy header, xóa toàn bộ buffer
                        _rxBuffer.Clear();
                        return;
                    }
                    // Xóa các byte rác trước header (nếu có)
                    if (headerIndex > 0)
                    {
                        _rxBuffer.RemoveRange(0, headerIndex);
                        continue;
                    }
                    // Bây giờ _rxBuffer[0] chắc chắn là HEADER
                    if (_rxBuffer.Count < 5) return;

                    int len = _rxBuffer[1] | _rxBuffer[2] << 8; // Lấy độ dài data
                    
                    int frameLength = 5 + len; // Tính tổng frame length: HEADER(1) + LEN(2) + DATA(len)
                    // Chưa đủ dữ liệu, chờ thêm
                    if (_rxBuffer.Count < frameLength) 
                        return;

                    byte[] frame = _rxBuffer.Take(frameLength).ToArray(); // Lấy toàn bộ frame

                    byte[] data = new byte[len]; // Lấy phần data (bỏ qua header, len, crc16)
                    Array.Copy(frame, 3, data, 0, len);

                    ushort crc_calc = ModbusCRC(data, len);
                    ushort crc_rx = (ushort)((frame[frameLength - 2] & 0xFF) | ((frame[frameLength - 1] & 0xFF) << 8));
                    if (crc_rx != crc_calc)
                    {
                        // CRC lỗi

                        byte[] crc_calc_bytes = BitConverter.GetBytes(crc_calc);
                        byte[] crc_rx_bytes = BitConverter.GetBytes(crc_rx);

                        MessageBox.Show($"CRC lỗi" + Environment.NewLine + 
                                        $"Frame: {(BitConverter.ToString(frame))}" + Environment.NewLine +
                                        $"Calculated CRC: {(BitConverter.ToString(crc_calc_bytes))}" + Environment.NewLine +
                                        $"Received CRC: {(BitConverter.ToString(crc_rx_bytes))}", "Debug");
                        return;
                    }
                    else
                    {
                        //MessageBox.Show($"CRC ok : {(BitConverter.ToString(frame))}", "Debug");
                    }

                    // Xử lý data trên UI thread
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SendDataToCurrentTab(data);
                        //MessageBox.Show($"Received data: {(BitConverter.ToString(data))}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    });

                    // Xóa frame đã xử lý khỏi buffer
                    _rxBuffer.RemoveRange(0, frameLength);
                }
            }
        }

        private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            switch (e.EventType)
            {
                case SerialError.RXParity:
                    Debug.WriteLine("Lỗi parity!");
                    break;
                case SerialError.Frame:
                    Debug.WriteLine("Lỗi khung (framing error)!");
                    break;
                case SerialError.RXOver:
                    Debug.WriteLine("Tràn bộ đệm đầu vào!");
                    break;
                case SerialError.Overrun:
                    Debug.WriteLine("Tràn bộ đệm ký tự!");
                    break;
                case SerialError.TXFull:
                    Debug.WriteLine("Bộ đệm đầu ra đầy!");
                    break;
            }
        }

        private void SendDataToCurrentTab(byte[] data)
        {
            switch (tabControl.SelectedIndex)
            {
                case 0: // Tab Thống kê
                    _page1?.UpdateData(data);
                    break;
                case 1: // Tab Dữ liệu tức thời
                    _page2?.UpdateData(data);
                    break;
                case 2: // Tab Cấu hình
                    _page3?.UpdateData(data);
                    break;
            }
        }

        // Hàm gửi dữ liệu từ Page gọi đến
        public void SendData(byte[] data)
        {
            if (!IsConnected || _serialPort == null || !_serialPort.IsOpen)
            {
                MessageBox.Show("Chưa kết nối cổng COM!", "Cảnh báo");
                return;
            }

            ushort len = (ushort)data.Length;
            List<byte> frame = new List<byte>();
            frame.Add(HEADER);
            frame.Add((byte)(len & 0xFF));
            frame.Add((byte)((len >> 8) & 0xFF));
            frame.AddRange(data);
            ushort crc = ModbusCRC(data, len);
            // Add CRC low byte then high byte
            frame.Add((byte)(crc & 0xFF));          
            frame.Add((byte)((crc >> 8) & 0xFF));

            // Gửi đi
            try
            {
                _serialPort.Write(frame.ToArray(), 0, frame.Count);
                //MessageBox.Show($"Sending: {(BitConverter.ToString(frame.ToArray()))}", "Debug");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi gửi dữ liệu: {ex.Message}", "Lỗi");
            }
        }
        // Hàm gửi string
        public void SendString(string text)
        {
            byte[] data = Encoding.ASCII.GetBytes(text);
            SendData(data);
        }
        public static ushort ModbusCRC(byte[] data, int length)
        {
            ushort crc = 0xFFFF;

            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            return crc;
        }
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }
        }
    }
}