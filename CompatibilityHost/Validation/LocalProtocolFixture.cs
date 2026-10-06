using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation
{
    // Tests the real HTTP client and controller using an isolated loopback port;
    // never starts the installed audio service or changes the user's profile.
    internal sealed class LocalProtocolFixture : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentDictionary<string, int> _requests = new();
        private int _missingAuthentication;
        internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        internal bool MissingAuthentication => _missingAuthentication != 0;
        internal bool FailTests { get; set; }
        internal int Count(string path) => _requests.TryGetValue(path, out int count) ? count : 0;
        internal LocalProtocolFixture() { _listener.Start(); _ = AcceptAsync(); }
        private async Task AcceptAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    _ = RespondAsync(client);
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (_stop.IsCancellationRequested) { }
        }
        private async Task RespondAsync(TcpClient client)
        {
            using (client)
            try
            {
                using NetworkStream stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);
                string request = await reader.ReadLineAsync(_stop.Token);
                if (request == null) return;
                string path = request.Split(' ')[1].Split('?')[0];
                int length = 0; bool authenticated = false;
                for (string line = await reader.ReadLineAsync(_stop.Token); !string.IsNullOrEmpty(line); line = await reader.ReadLineAsync(_stop.Token))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) int.TryParse(line.Substring(15).Trim(), out length);
                    if (line.StartsWith("x-killconfirm-token:", StringComparison.OrdinalIgnoreCase)) authenticated = true;
                    if (line.Equals("Expect: 100-continue", StringComparison.OrdinalIgnoreCase)) await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"), _stop.Token);
                }
                char[] body = new char[length];
                int offset = 0;
                while (offset < body.Length) { int count = await reader.ReadAsync(body.AsMemory(offset), _stop.Token); if (count == 0) break; offset += count; }
                if (!authenticated) Interlocked.Exchange(ref _missingAuthentication, 1);
                _requests.AddOrUpdate(path, 1, (key, count) => count + 1);
                if (path == "/events") await Task.Delay(200, _stop.Token);
                string json = path == "/events" ? "{\"cursor\":0,\"events\":[],\"dropped\":0}"
                    : path == "/gsi-status" ? "{\"posts\":1,\"last_post_age_ms\":100,\"parse_errors\":0}" : "{}";
                string responseStatus = FailTests && path.StartsWith("/test/") ? "500 Internal Server Error" : "200 OK";
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 " + responseStatus + "\r\nContent-Type: application/json\r\nConnection: close\r\nContent-Length: " + json.Length + "\r\n\r\n" + json);
                await stream.WriteAsync(response, _stop.Token);
            }
            catch (IOException) { }
            catch (OperationCanceledException) { }
        }
        public void Dispose() { _stop.Cancel(); _listener.Stop(); _stop.Dispose(); }
    }
}
