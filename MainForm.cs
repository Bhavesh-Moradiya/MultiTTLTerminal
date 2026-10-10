using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Windows.Forms;
using MultiTTLTerminal.Engine;

namespace MultiTTLTerminal
{
    public class MainForm : Form
    {
        private RadioButton rdbModeSerial;
        private RadioButton rdbModeTcp;

        // Serial Controls
        private GroupBox grpSerial;
        private ComboBox cmbPorts;
        private ComboBox cmbBaud;
        private ComboBox cmbDataBits;
        private ComboBox cmbStopBits;
        private ComboBox cmbParity;
        private ComboBox cmbFlowControl;
        private CheckBox chkDtr;
        private CheckBox chkRts;
        private Button btnRefresh;

        // TCP/IP Controls
        private GroupBox grpTcp;
        private TextBox txtHost;
        private NumericUpDown numTcpPort;
        private RadioButton rdbTcpRaw;
        private RadioButton rdbTcpTelnet;
        private RadioButton rdbTcpSsl;

        private RadioButton rdbTcpUdp;

        private Button btnOpenSession;

        public MainForm()
        {
            InitializeComponent();
            RefreshPorts();
            ToggleConnectionMode();
        }

        private void InitializeComponent()
        {
            this.Text = "Multi-Terminal Launcher (Serial & Network) v1.0";
            this.Size = new System.Drawing.Size(500, 520);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // --- ABOUT PANEL ---
            GroupBox grpAbout = new GroupBox
            {
                Text = "About & Credits",
                Location = new System.Drawing.Point(15, 10),
                Size = new System.Drawing.Size(455, 70)
            };
            Label lblAuthor = new Label { Text = "Author: Bhavesh M Moradiya", Location = new System.Drawing.Point(15, 20), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) };
            LinkLabel lblEmail = new LinkLabel { Text = "Email: Bhavesh.moradi@live.com", Location = new System.Drawing.Point(15, 40), AutoSize = true };
            lblEmail.LinkClicked += (s, e) => { try { Process.Start(new ProcessStartInfo("mailto:Bhavesh.moradi@live.com") { UseShellExecute = true }); } catch { } };
            grpAbout.Controls.Add(lblAuthor);
            grpAbout.Controls.Add(lblEmail);

            // --- MODE SELECTION ---
            rdbModeSerial = new RadioButton { Text = "Serial / TTL Connection", Location = new System.Drawing.Point(20, 90), AutoSize = true, Checked = true };
            rdbModeTcp = new RadioButton { Text = "TCP / IP Network Connection", Location = new System.Drawing.Point(220, 90), AutoSize = true };

            rdbModeSerial.CheckedChanged += (s, e) => ToggleConnectionMode();
            rdbModeTcp.CheckedChanged += (s, e) => ToggleConnectionMode();

            // --- SERIAL GROUP ---
            grpSerial = new GroupBox { Text = "Serial UART Configuration", Location = new System.Drawing.Point(15, 115), Size = new System.Drawing.Size(455, 210) };
            Label lblPort = new Label { Text = "COM Port:", Location = new System.Drawing.Point(15, 25), AutoSize = true };
            cmbPorts = new ComboBox { Location = new System.Drawing.Point(115, 22), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            btnRefresh = new Button { Text = "Refresh", Location = new System.Drawing.Point(325, 20), Width = 110, Height = 25 };
            btnRefresh.Click += (s, e) => RefreshPorts();

            Label lblBaud = new Label { Text = "Baud Rate:", Location = new System.Drawing.Point(15, 55), AutoSize = true };
            cmbBaud = new ComboBox { Location = new System.Drawing.Point(115, 52), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBaud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200", "230400", "921600" });
            cmbBaud.SelectedIndex = 7;

            Label lblDataBits = new Label { Text = "Data Bits:", Location = new System.Drawing.Point(15, 85), AutoSize = true };
            cmbDataBits = new ComboBox { Location = new System.Drawing.Point(115, 82), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDataBits.Items.AddRange(new object[] { "5", "6", "7", "8" });
            cmbDataBits.SelectedIndex = 3;

            Label lblStopBits = new Label { Text = "Stop Bits:", Location = new System.Drawing.Point(15, 115), AutoSize = true };
            cmbStopBits = new ComboBox { Location = new System.Drawing.Point(115, 112), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStopBits.Items.AddRange(Enum.GetNames(typeof(StopBits)));
            cmbStopBits.SelectedItem = StopBits.One.ToString();

            Label lblParity = new Label { Text = "Parity:", Location = new System.Drawing.Point(15, 145), AutoSize = true };
            cmbParity = new ComboBox { Location = new System.Drawing.Point(115, 142), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbParity.Items.AddRange(Enum.GetNames(typeof(Parity)));
            cmbParity.SelectedItem = Parity.None.ToString();

            Label lblFlow = new Label { Text = "Flow Control:", Location = new System.Drawing.Point(15, 175), AutoSize = true };
            cmbFlowControl = new ComboBox { Location = new System.Drawing.Point(115, 172), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFlowControl.Items.AddRange(Enum.GetNames(typeof(Handshake)));
            cmbFlowControl.SelectedItem = Handshake.None.ToString();

            chkDtr = new CheckBox { Text = "DTR", Location = new System.Drawing.Point(325, 82), AutoSize = true, Checked = true };
            chkRts = new CheckBox { Text = "RTS", Location = new System.Drawing.Point(325, 112), AutoSize = true, Checked = true };

            grpSerial.Controls.Add(lblPort); grpSerial.Controls.Add(cmbPorts); grpSerial.Controls.Add(btnRefresh);
            grpSerial.Controls.Add(lblBaud); grpSerial.Controls.Add(cmbBaud); grpSerial.Controls.Add(lblDataBits);
            grpSerial.Controls.Add(cmbDataBits); grpSerial.Controls.Add(lblStopBits); grpSerial.Controls.Add(cmbStopBits);
            grpSerial.Controls.Add(lblParity); grpSerial.Controls.Add(cmbParity); grpSerial.Controls.Add(lblFlow);
            grpSerial.Controls.Add(cmbFlowControl); grpSerial.Controls.Add(chkDtr); grpSerial.Controls.Add(chkRts);

            // --- TCP GROUP ---
            grpTcp = new GroupBox { Text = "Network TCP / IP Configuration", Location = new System.Drawing.Point(15, 335), Size = new System.Drawing.Size(455, 85) };
            Label lblHost = new Label { Text = "Host / IP:", Location = new System.Drawing.Point(15, 25), AutoSize = true };
            txtHost = new TextBox { Text = "127.0.0.1", Location = new System.Drawing.Point(85, 22), Width = 180 };

            Label lblTcpPort = new Label { Text = "Port:", Location = new System.Drawing.Point(280, 25), AutoSize = true };
            numTcpPort = new NumericUpDown { Location = new System.Drawing.Point(320, 22), Width = 115, Minimum = 1, Maximum = 65535, Value = 23 };

            rdbTcpRaw = new RadioButton { Text = "Raw TCP", Location = new System.Drawing.Point(85, 52), AutoSize = true, Checked = true };
            rdbTcpTelnet = new RadioButton { Text = "Telnet", Location = new System.Drawing.Point(175, 52), AutoSize = true };
            rdbTcpSsl = new RadioButton { Text = "SSL / TLS", Location = new System.Drawing.Point(250, 52), AutoSize = true };

            rdbTcpUdp = new RadioButton { Text = "UDP (NB-IoT)", Location = new System.Drawing.Point(335, 52), AutoSize = true };

            grpTcp.Controls.Add(rdbTcpUdp);
            grpTcp.Controls.Add(lblHost); grpTcp.Controls.Add(txtHost);
            grpTcp.Controls.Add(lblTcpPort); grpTcp.Controls.Add(numTcpPort);
            grpTcp.Controls.Add(rdbTcpRaw); grpTcp.Controls.Add(rdbTcpTelnet); grpTcp.Controls.Add(rdbTcpSsl);

            // --- OPEN SESSION BUTTON ---
            btnOpenSession = new Button { Text = "Open Terminal Session", Location = new System.Drawing.Point(135, 430), Width = 220, Height = 40, Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold) };
            btnOpenSession.Click += BtnOpenSession_Click;

            this.Controls.Add(grpAbout);
            this.Controls.Add(rdbModeSerial);
            this.Controls.Add(rdbModeTcp);
            this.Controls.Add(grpSerial);
            this.Controls.Add(grpTcp);
            this.Controls.Add(btnOpenSession);

            this.FormClosing += MainForm_FormClosing;
        }

        private void ToggleConnectionMode()
        {
            grpSerial.Enabled = rdbModeSerial.Checked;
            grpTcp.Enabled = rdbModeTcp.Checked;
        }

        private void RefreshPorts()
        {
            cmbPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            cmbPorts.Items.AddRange(ports);
            if (cmbPorts.Items.Count > 0) cmbPorts.SelectedIndex = 0;
        }

        private void BtnOpenSession_Click(object sender, EventArgs e)
        {
            ITerminalStream stream;

            if (rdbModeSerial.Checked)
            {
                if (cmbPorts.SelectedItem == null)
                {
                    MessageBox.Show("No COM port selected.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string selectedPort = cmbPorts.SelectedItem.ToString();
                int baudRate = int.Parse(cmbBaud.SelectedItem.ToString());
                int dataBits = int.Parse(cmbDataBits.SelectedItem.ToString());
                StopBits stopBits = (StopBits)Enum.Parse(typeof(StopBits), cmbStopBits.SelectedItem.ToString());
                Parity parity = (Parity)Enum.Parse(typeof(Parity), cmbParity.SelectedItem.ToString());
                Handshake flowControl = (Handshake)Enum.Parse(typeof(Handshake), cmbFlowControl.SelectedItem.ToString());

                stream = new SerialTerminalStream(selectedPort, baudRate, parity, dataBits, stopBits, flowControl, chkDtr.Checked, chkRts.Checked);
            }
            else
            {
                string host = txtHost.Text.Trim();

                // 1. If markdown link format like [text](url), extract just the domain or URL inside
                if (host.Contains("](") && host.Contains(")"))
                {
                    int start = host.IndexOf("](") + 2;
                    int length = host.LastIndexOf(")") - start;
                    if (length > 0)
                    {
                        host = host.Substring(start, length);
                    }
                }

                // 2. Strip standard URL prefixes, paths, and brackets
                host = host.Replace("https://", "")
                           .Replace("http://", "")
                           .Replace("[", "")
                           .Replace("]", "");

                // 3. Remove trailing path slashes (e.g. www.google.com/ -> www.google.com)
                if (host.Contains("/"))
                {
                    host = host.Split('/')[0];
                }

                host = host.Trim();

                int port = (int)numTcpPort.Value;
                bool useSsl = rdbTcpSsl.Checked;

                if (string.IsNullOrEmpty(host))
                {
                    MessageBox.Show("Please specify a valid hostname or IP address.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (rdbTcpUdp.Checked)
                {
                    // Resolves IPv4 or IPv6 and initiates persistent UDP socket session
                    stream = new UdpTerminalStream(host, port);
                }
                else
                {
                    stream = new NetworkTerminalStream(host, port, useSsl);
                }
            }

            PortTerminalForm terminalWindow = new PortTerminalForm(stream);
            terminalWindow.Show();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Application.OpenForms.Count > 1)
            {
                DialogResult dr = MessageBox.Show(
                    "Active terminal windows are open. Close all active sessions?",
                    "Confirm Exit",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (dr == DialogResult.No)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}