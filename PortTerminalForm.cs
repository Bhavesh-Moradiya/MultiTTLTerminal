using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using MultiTTLTerminal.Engine;

namespace MultiTTLTerminal
{
    // Data model class for history tracking
    public class LogEntry
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty; // "RX" or "TX"
        public byte[] RawData { get; set; } = Array.Empty<byte>();
    }

    public class PortTerminalForm : Form
    {
        private ITerminalStream _stream;
        private string _logFilePath = string.Empty;

        private List<byte> _rxBuffer = new List<byte>();
        private List<LogEntry> _history = new List<LogEntry>();
        private System.Windows.Forms.Timer _rxTimer;
        private readonly object _lockObj = new object();

        private RichTextBox rtbLog = null!;
        private RadioButton rdbAsciiView = null!;
        private RadioButton rdbHexView = null!;
        private Button btnClearScreen = null!;
        private TrackBar tbOpacity = null!;
        private Label lblOpacity = null!;

        private TextBox txtInput = null!;
        private Button btnSend = null!;
        private CheckBox chkAutoCRLF = null!;

        private CheckBox chkLogToFile = null!;
        private TextBox txtLogPath = null!;
        private Button btnBrowseLog = null!;
        private Button btnResetLogFile = null!;

        private StatusStrip statusStrip = null!;
        private ToolStripStatusLabel lblStatus = null!;

        public PortTerminalForm(ITerminalStream stream)
        {
            _stream = stream;
            InitializeComponent();
            this.Text = $"Terminal Session - {_stream.Name}";

            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            txtLogPath.Text = _logFilePath;

            _rxTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _rxTimer.Tick += RxTimer_Tick;

            // Defer InitStream until Window Handle is fully created and shown
            this.Shown += (s, e) => InitStream();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(880, 650);
            this.MinimumSize = new Size(880, 650);

            // Display Mode
            GroupBox grpDisplayMode = new GroupBox { Text = "Display Mode", Location = new Point(12, 10), Size = new Size(270, 48) };
            rdbAsciiView = new RadioButton { Text = "ASCII/Text", Location = new Point(10, 18), AutoSize = true, Checked = true };
            rdbHexView = new RadioButton { Text = "HEX Format", Location = new Point(100, 18), AutoSize = true };
            btnClearScreen = new Button { Text = "Clear Screen", Location = new Point(185, 15), Width = 80, Height = 25 };

            rdbAsciiView.CheckedChanged += DisplayMode_CheckedChanged;
            rdbHexView.CheckedChanged += DisplayMode_CheckedChanged;
            btnClearScreen.Click += (s, e) => { _history.Clear(); rtbLog.Clear(); };
            grpDisplayMode.Controls.Add(rdbAsciiView); grpDisplayMode.Controls.Add(rdbHexView); grpDisplayMode.Controls.Add(btnClearScreen);

            // Opacity
            GroupBox grpTransparency = new GroupBox { Text = "Opacity", Location = new Point(288, 10), Size = new Size(130, 48) };
            lblOpacity = new Label { Text = "100%", Location = new Point(88, 20), AutoSize = true };
            tbOpacity = new TrackBar { Location = new Point(5, 12), Width = 80, Minimum = 2, Maximum = 10, Value = 10, TickStyle = TickStyle.None };
            tbOpacity.ValueChanged += (s, e) => { this.Opacity = tbOpacity.Value / 10.0; lblOpacity.Text = $"{tbOpacity.Value * 10}%"; };
            grpTransparency.Controls.Add(tbOpacity); grpTransparency.Controls.Add(lblOpacity);

            // Logging Settings
            GroupBox grpLogging = new GroupBox { Text = "Log File Settings (.log)", Location = new Point(424, 10), Size = new Size(428, 48) };
            chkLogToFile = new CheckBox { Text = "Enable", Checked = false, Location = new Point(8, 20), AutoSize = true };
            txtLogPath = new TextBox { Location = new Point(75, 18), Width = 185, ReadOnly = true, Enabled = false };
            btnBrowseLog = new Button { Text = "Browse...", Location = new Point(265, 16), Width = 70, Height = 25, Enabled = false };
            btnResetLogFile = new Button { Text = "Reset Log", Location = new Point(340, 16), Width = 80, Height = 25, Enabled = false };

            chkLogToFile.CheckedChanged += ChkLogToFile_CheckedChanged;
            btnBrowseLog.Click += BtnBrowseLog_Click;
            btnResetLogFile.Click += (s, e) => { if (!string.IsNullOrEmpty(_logFilePath)) File.WriteAllText(_logFilePath, string.Empty); };

            grpLogging.Controls.Add(chkLogToFile); grpLogging.Controls.Add(txtLogPath);
            grpLogging.Controls.Add(btnBrowseLog); grpLogging.Controls.Add(btnResetLogFile);

            // Rich Terminal Output
            rtbLog = new RichTextBox { Location = new Point(12, 68), Size = new Size(840, 390), ReadOnly = true, BackColor = Color.FromArgb(248, 249, 250), Font = new Font("Consolas", 10F) };

            // TX Window
            Label lblInput = new Label { Text = "TX Payload:", Location = new Point(12, 470), AutoSize = true };
            txtInput = new TextBox { Location = new Point(12, 490), Width = 580, Height = 55, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10F) };

            // Real-time conversion and focus leave event subscriptions
            txtInput.KeyPress += TxtInput_KeyPress;
            txtInput.Leave += TxtInput_Leave;

            chkAutoCRLF = new CheckBox { Text = "Auto \\r\\n", Checked = true, Location = new Point(602, 505), AutoSize = true };
            btnSend = new Button { Text = "SEND", Location = new Point(720, 490), Width = 132, Height = 55, Font = new Font("Segoe UI", 11F, FontStyle.Bold), BackColor = Color.LightSteelBlue };
            btnSend.Click += (s, e) => SendData();

            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel("Status: Connecting...");
            statusStrip.Items.Add(lblStatus);

            this.Controls.Add(grpDisplayMode); this.Controls.Add(grpTransparency); this.Controls.Add(grpLogging);
            this.Controls.Add(rtbLog); this.Controls.Add(lblInput); this.Controls.Add(txtInput);
            this.Controls.Add(chkAutoCRLF); this.Controls.Add(btnSend); this.Controls.Add(statusStrip);

            this.FormClosing += PortTerminalForm_FormClosing;
        }

        private void InitStream()
        {
            try
            {
                _stream.DataReceived += Stream_DataReceived;
                _stream.ServerClosed += Stream_ServerClosed;
                _stream.Open();

                if (this.IsHandleCreated)
                {
                    this.BeginInvoke((Action)delegate
                    {
                        lblStatus.Text = $"Status: Connected to {_stream.Name}";
                    });
                }
            }
            catch (Exception ex)
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke((Action)delegate
                    {
                        lblStatus.Text = "Status: Connection Failed";
                    });
                }

                MessageBox.Show($"Failed to connect to {_stream.Name}:\n{ex.Message}",
                                "Connection Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        private void Stream_ServerClosed(object sender, EventArgs e)
        {
            if (this.IsHandleCreated)
            {
                this.BeginInvoke((Action)delegate
                {
                    lblStatus.Text = $"Status: Connection Closed by Server ({_stream.Name})";

                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                    rtbLog.SelectionStart = rtbLog.TextLength;
                    rtbLog.SelectionLength = 0;
                    rtbLog.SelectionColor = Color.DimGray;
                    rtbLog.AppendText($"[{timestamp}] ");

                    rtbLog.SelectionColor = Color.Red;
                    rtbLog.SelectionFont = new Font(rtbLog.Font, FontStyle.Bold);
                    rtbLog.AppendText("[SYSTEM] - Connection closed by remote server (FIN received)\n");

                    rtbLog.ScrollToCaret();
                });
            }
        }

        private void Stream_DataReceived(object sender, byte[] buffer)
        {
            lock (_lockObj)
            {
                _rxBuffer.AddRange(buffer);
            }

            if (this.IsHandleCreated)
            {
                this.BeginInvoke((Action)delegate
                {
                    _rxTimer.Stop();
                    _rxTimer.Start();
                });
            }
        }

        private void RxTimer_Tick(object sender, EventArgs e)
        {
            _rxTimer.Stop();
            byte[] completePacket;
            lock (_lockObj)
            {
                if (_rxBuffer.Count == 0) return;
                completePacket = _rxBuffer.ToArray();
                _rxBuffer.Clear();
            }
            RecordAndDisplayData(completePacket, "RX");
        }

        /// <summary>
        /// Converts any raw string (ASCII characters) to space-separated HEX bytes.
        /// Example: "G" -> "47", "Hello" -> "48 65 6C 6C 6F"
        /// </summary>
        private string ConvertTextToHexString(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string clean = text.Replace(" ", "").Replace("0x", "").Replace("0X", "");
            if (System.Text.RegularExpressions.Regex.IsMatch(clean, @"\A\b[0-9a-fA-F]+\b\Z") && clean.Length % 2 == 0)
            {
                return text; // Keep existing hex string intact
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return BitConverter.ToString(bytes).Replace("-", " ");
        }

        /// <summary>
        /// Converts space-separated HEX string back to ASCII text.
        /// Example: "47" -> "G", "48 65 6C 6C 6F" -> "Hello"
        /// </summary>
        private string ConvertHexStringToText(string hex)
        {
            try
            {
                byte[] bytes = ParseHexOrTextStringToBytes(hex);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return hex; // Fallback to raw text if parsing fails
            }
        }

        private void TxtInput_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (rdbHexView.Checked)
            {
                if (char.IsControl(e.KeyChar)) return;

                // If key pressed is NOT a valid hex digit (0-9, A-F, a-f) and not a space
                if (!Uri.IsHexDigit(e.KeyChar) && e.KeyChar != ' ')
                {
                    e.Handled = true; // Suppress raw ASCII character input

                    // Convert key directly to 2-digit HEX representation
                    byte keyByte = (byte)e.KeyChar;
                    string hexChar = keyByte.ToString("X2") + " ";

                    int selectionStart = txtInput.SelectionStart;
                    txtInput.Text = txtInput.Text.Insert(selectionStart, hexChar);
                    txtInput.SelectionStart = selectionStart + hexChar.Length;
                }
            }
        }

        private void TxtInput_Leave(object sender, EventArgs e)
        {
            if (rdbHexView.Checked && !string.IsNullOrEmpty(txtInput.Text))
            {
                txtInput.Text = ConvertTextToHexString(txtInput.Text);
            }
        }

        private void SendData()
        {
            if (_stream == null || !_stream.IsConnected)
            {
                MessageBox.Show("Connection is not active.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                byte[] payload;
                string input = txtInput.Text.Trim();
                if (string.IsNullOrEmpty(input)) return;

                if (rdbHexView.Checked)
                {
                    string hexPayload = input;

                    // Append \r\n (0D 0A in HEX) if Auto CRLF is checked
                    if (chkAutoCRLF.Checked)
                    {
                        hexPayload += " 0D 0A";
                    }

                    payload = ParseHexOrTextStringToBytes(hexPayload);
                }
                else
                {
                    string textToSend = input;
                    if (chkAutoCRLF.Checked) textToSend += "\r\n";
                    payload = Encoding.UTF8.GetBytes(textToSend);
                }

                if (payload.Length > 0)
                {
                    _stream.Write(payload, 0, payload.Length);
                    RecordAndDisplayData(payload, "TX");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Send Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private byte[] ParseHexOrTextStringToBytes(string input)
        {
            string cleanInput = input.Replace(" ", "")
                                     .Replace("0x", "")
                                     .Replace("0X", "")
                                     .Replace("-", "")
                                     .Replace(",", "")
                                     .Replace("\r", "")
                                     .Replace("\n", "");

            if (string.IsNullOrEmpty(cleanInput)) return Array.Empty<byte>();

            bool isPureHex = System.Text.RegularExpressions.Regex.IsMatch(cleanInput, @"\A\b[0-9a-fA-F]+\b\Z");

            if (isPureHex)
            {
                if (cleanInput.Length % 2 != 0) cleanInput = "0" + cleanInput;

                byte[] bytes = new byte[cleanInput.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(cleanInput.Substring(i * 2, 2), 16);
                }
                return bytes;
            }
            else
            {
                string textToSend = input;
                if (chkAutoCRLF.Checked) textToSend += "\r\n";
                return Encoding.UTF8.GetBytes(textToSend);
            }
        }

        private void ChkLogToFile_CheckedChanged(object sender, EventArgs e)
        {
            if (chkLogToFile.Checked)
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "Log Files (*.log)|*.log|All Files (*.*)|*.*";
                    sfd.DefaultExt = "log";
                    sfd.FileName = Path.GetFileName(_logFilePath);

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        _logFilePath = sfd.FileName;
                        txtLogPath.Text = _logFilePath;
                        txtLogPath.Enabled = btnBrowseLog.Enabled = btnResetLogFile.Enabled = true;
                    }
                    else
                    {
                        chkLogToFile.Checked = false;
                    }
                }
            }
            else
            {
                txtLogPath.Enabled = btnBrowseLog.Enabled = btnResetLogFile.Enabled = false;
            }
        }

        private void DisplayMode_CheckedChanged(object sender, EventArgs e)
        {
            chkAutoCRLF.Enabled = true; // Auto CRLF remains available in both modes

            if (!string.IsNullOrEmpty(txtInput.Text))
            {
                if (rdbHexView.Checked)
                {
                    txtInput.Text = ConvertTextToHexString(txtInput.Text);
                }
                else
                {
                    txtInput.Text = ConvertHexStringToText(txtInput.Text);
                }
            }

            RenderTerminalView();
        }

        private void RecordAndDisplayData(byte[] data, string direction)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            LogEntry entry = new LogEntry
            {
                Timestamp = timestamp,
                Direction = direction,
                RawData = data
            };

            _history.Add(entry);

            RenderEntry(entry);

            if (chkLogToFile.Checked && !string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    if (rdbHexView.Checked)
                    {
                        string hexStr = BitConverter.ToString(data).Replace("-", " ");
                        File.AppendAllText(_logFilePath, $"[{timestamp}] [{direction}] - {hexStr}" + Environment.NewLine);
                    }
                    else
                    {
                        string rawText = Encoding.UTF8.GetString(data).Replace("\r", "");
                        string[] lines = rawText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string line in lines)
                        {
                            File.AppendAllText(_logFilePath, $"[{timestamp}] [{direction}] - {line}" + Environment.NewLine);
                        }
                    }
                }
                catch
                {
                    // Catch transient log write contention
                }
            }
        }

        private void RenderTerminalView()
        {
            rtbLog.Clear();
            foreach (var entry in _history) RenderEntry(entry);
        }

        private void RenderEntry(LogEntry entry)
        {
            if (rdbHexView.Checked)
            {
                AppendColoredLine(entry.Timestamp, entry.Direction, BitConverter.ToString(entry.RawData).Replace("-", " "));
            }
            else
            {
                string rawText = Encoding.UTF8.GetString(entry.RawData).Replace("\r", "");
                foreach (string line in rawText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    AppendColoredLine(entry.Timestamp, entry.Direction, line);
            }
        }

        private void AppendColoredLine(string timestamp, string direction, string content)
        {
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = Color.DimGray;
            rtbLog.AppendText($"[{timestamp}] ");

            rtbLog.SelectionColor = (direction == "RX") ? Color.DarkGreen : Color.DarkBlue;
            rtbLog.SelectionFont = new Font(rtbLog.Font, FontStyle.Bold);
            rtbLog.AppendText($"[{direction}] - ");

            rtbLog.SelectionColor = Color.Black;
            rtbLog.SelectionFont = new Font(rtbLog.Font, FontStyle.Regular);
            rtbLog.AppendText(content + Environment.NewLine);

            rtbLog.ScrollToCaret();
        }

        private void BtnBrowseLog_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Log Files (*.log)|*.log|All Files (*.*)|*.*";
                sfd.DefaultExt = "log";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _logFilePath = sfd.FileName;
                    txtLogPath.Text = _logFilePath;
                }
            }
        }

        private void PortTerminalForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_stream != null && _stream.IsConnected)
            {
                DialogResult dr = MessageBox.Show("Are you sure you want to close this session?", "Confirm Close Session", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            _rxTimer?.Stop();
            _rxTimer?.Dispose();
            _stream?.Dispose();
        }
    }
}