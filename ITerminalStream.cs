using System;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MultiTTLTerminal
{
    public interface ITerminalStream : IDisposable
    {
        string Name { get; }
        bool IsConnected { get; }
        event EventHandler<byte[]> DataReceived;
        event EventHandler ServerClosed; // Added to interface contract
        void Write(byte[] buffer, int offset, int count);
        void Open();
        void Close();
    }

    // --- SERIAL PORT IMPLEMENTATION ---
    public class SerialTerminalStream : ITerminalStream
    {
        private SerialPort _sp;
        public string Name => _sp != null ? _sp.PortName : "COM Port";
        public bool IsConnected => _sp != null && _sp.IsOpen;

        public event EventHandler<byte[]> DataReceived;
        public event EventHandler ServerClosed; // Satisfies interface (unused for serial)

        public SerialTerminalStream(string portName, int baud, Parity parity, int dataBits, StopBits stopBits, Handshake flow, bool dtr, bool rts)
        {
            _sp = new SerialPort(portName, baud, parity, dataBits, stopBits)
            {
                Handshake = flow,
                DtrEnable = dtr,
                RtsEnable = rts
            };

            _sp.DataReceived += (s, e) =>
            {
                try
                {
                    if (_sp == null || !_sp.IsOpen) return;
                    int bytesToRead = _sp.BytesToRead;
                    if (bytesToRead <= 0) return;

                    byte[] temp = new byte[bytesToRead];
                    int bytesRead = _sp.Read(temp, 0, bytesToRead);
                    if (bytesRead > 0)
                    {
                        byte[] actualData = new byte[bytesRead];
                        Array.Copy(temp, actualData, bytesRead);
                        DataReceived?.Invoke(this, actualData);
                    }
                }
                catch
                {
                    // Catch stream read interruptions during port closure
                }
            };
        }

        public void Open()
        {
            if (_sp == null) throw new InvalidOperationException("Serial port is not initialized.");
            if (!_sp.IsOpen)
            {
                _sp.Open();
                // Flush stale hardware buffers on connect
                _sp.DiscardInBuffer();
                _sp.DiscardOutBuffer();
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (_sp != null && _sp.IsOpen)
            {
                _sp.Write(buffer, offset, count);
            }
        }

        public void Close()
        {
            if (_sp != null && _sp.IsOpen)
            {
                _sp.Close();
            }
        }

        public void Dispose()
        {
            Close();
            _sp?.Dispose();
        }
    }

    // --- TCP / TELNET / SSL IMPLEMENTATION ---
    public class NetworkTerminalStream : ITerminalStream
    {
        private TcpClient _client;
        private Stream _stream;
        private bool _isListening;
        private string _host;
        private int _port;
        private bool _useSsl;

        public string Name => $"{_host}:{_port}" + (_useSsl ? " (SSL)" : "");
        public bool IsConnected => _client != null && _client.Connected;

        public event EventHandler<byte[]> DataReceived;
        public event EventHandler ServerClosed; // Fired when remote server closes socket (FIN)

        public NetworkTerminalStream(string host, int port, bool useSsl)
        {
            _host = host;
            _port = port;
            _useSsl = useSsl;
        }

        public void Open()
        {
            _client = new TcpClient();

            // 3-second connection timeout
            var connectTask = _client.ConnectAsync(_host, _port);
            if (!connectTask.Wait(3000))
            {
                _client.Close();
                throw new TimeoutException($"Connection to {_host}:{_port} timed out.");
            }

            if (_useSsl)
            {
                var sslStream = new SslStream(_client.GetStream(), false, (sender, cert, chain, errors) => true);
                sslStream.AuthenticateAsClient(_host);
                _stream = sslStream;
            }
            else
            {
                _stream = _client.GetStream();
            }

            _isListening = true;
            Task.Run(ListenLoop);
        }

        private async Task ListenLoop()
        {
            byte[] buffer = new byte[4096];
            while (_isListening && _client != null && _client.Connected)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);

                    // Server sent FIN packet (0 bytes read) or dropped connection
                    if (bytesRead <= 0)
                    {
                        if (_isListening)
                        {
                            ServerClosed?.Invoke(this, EventArgs.Empty);
                        }
                        break;
                    }

                    byte[] packet = new byte[bytesRead];
                    Array.Copy(buffer, packet, bytesRead);
                    DataReceived?.Invoke(this, packet);
                }
                catch
                {
                    if (_isListening)
                    {
                        ServerClosed?.Invoke(this, EventArgs.Empty);
                    }
                    break;
                }
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (_stream != null && IsConnected)
            {
                _stream.Write(buffer, offset, count);
                _stream.Flush();
            }
        }

        public void Close()
        {
            _isListening = false;
            _stream?.Close();
            _client?.Close();
        }

        public void Dispose()
        {
            Close();
            _stream?.Dispose();
            _client?.Dispose();
        }
    }

    // --- UDP / NB-IOT IMPLEMENTATION (IPv4 & IPv6) ---
    public class UdpTerminalStream : ITerminalStream
    {
        private UdpClient _udpClient;
        private IPEndPoint _remoteEndPoint;
        private bool _isListening;
        private string _host;
        private int _port;

        public string Name => $"UDP [{_host}]:{_port}";
        public bool IsConnected => _udpClient != null;

        public event EventHandler<byte[]> DataReceived;
        public event EventHandler ServerClosed; // Satisfies interface (UDP is connectionless)

        public UdpTerminalStream(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Open()
        {
            IPAddress targetAddress = IPAddress.Parse(_host);
            _remoteEndPoint = new IPEndPoint(targetAddress, _port);

            _udpClient = new UdpClient(targetAddress.AddressFamily);

            if (targetAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                _udpClient.Client.DualMode = true;
            }

            _udpClient.Connect(_remoteEndPoint);
            _isListening = true;

            Task.Run(ListenLoop);
        }

        private async Task ListenLoop()
        {
            while (_isListening && _udpClient != null)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();
                    if (result.Buffer != null && result.Buffer.Length > 0)
                    {
                        DataReceived?.Invoke(this, result.Buffer);
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (_udpClient != null)
            {
                byte[] payload = new byte[count];
                Array.Copy(buffer, offset, payload, 0, count);
                _udpClient.Send(payload, payload.Length);
            }
        }

        public void Close()
        {
            _isListening = false;
            _udpClient?.Close();
            _udpClient = null;
        }

        public void Dispose()
        {
            Close();
        }
    }
}