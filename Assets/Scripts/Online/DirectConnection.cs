using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectLike.Online
{
    // No Unity API on worker threads. Frames are length-prefixed, compressed UTF-8.
    // A single writer preserves ordering; obsolete world snapshots are replaced.
    public sealed class DirectConnection : IDisposable
    {
        public const int MaximumFrame = 8 * 1024 * 1024;
        readonly TcpClient socket;
        readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        readonly SemaphoreSlim wake = new SemaphoreSlim(0);
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        string latestOutgoing, latestIncoming;
        int queuedBytes, receivedBytes, closed;
        public bool Connected => Volatile.Read(ref closed) == 0;
        public string Error { get; private set; }

        public DirectConnection(TcpClient client)
        {
            socket = client; socket.NoDelay = true;
            _ = ReadLoop(); _ = WriteLoop();
        }
        public void Send(string json, bool snapshot = false)
        {
            if (!Connected) return;
            if (json == null || json.Length > MaximumFrame) { Fail("Mensaje demasiado grande"); return; }
            if (snapshot) Interlocked.Exchange(ref latestOutgoing, json);
            else
            {
                if (Interlocked.Add(ref queuedBytes, json.Length) > MaximumFrame * 2) { Fail("Conexion demasiado lenta"); return; }
                outgoing.Enqueue(json);
            }
            if (wake.CurrentCount == 0) wake.Release();
        }
        public bool TryReceive(out string json)
        {
            if (incoming.TryDequeue(out json)) { Interlocked.Add(ref receivedBytes, -json.Length); return true; }
            json = Interlocked.Exchange(ref latestIncoming, null);
            return json != null;
        }
        async Task ReadLoop()
        {
            try
            {
                var stream = socket.GetStream(); var header = new byte[4];
                while (Connected)
                {
                    await ReadExactly(stream, header, stop.Token).ConfigureAwait(false);
                    int length = header[0] | header[1] << 8 | header[2] << 16 | header[3] << 24;
                    if (length < 1 || length > MaximumFrame) throw new InvalidDataException("Longitud de paquete invalida");
                    var payload = new byte[length];
                    await ReadExactly(stream, payload, stop.Token).ConfigureAwait(false);
                    string json = Decode(payload);
                    if (json.StartsWith("{\"kind\":\"state\",", StringComparison.Ordinal)) Interlocked.Exchange(ref latestIncoming, json);
                    else
                    {
                        if (Interlocked.Add(ref receivedBytes, json.Length) > MaximumFrame * 2) throw new InvalidDataException("Demasiados mensajes pendientes");
                        incoming.Enqueue(json);
                    }
                }
            }
            catch (Exception error) { if (Connected) Fail(error is EndOfStreamException ? "El otro jugador se desconecto" : error.Message); }
        }
        async Task WriteLoop()
        {
            try
            {
                var stream = socket.GetStream();
                while (Connected)
                {
                    await wake.WaitAsync(stop.Token).ConfigureAwait(false);
                    while (Connected)
                    {
                        if (outgoing.TryDequeue(out var json)) Interlocked.Add(ref queuedBytes, -json.Length);
                        else json = Interlocked.Exchange(ref latestOutgoing, null);
                        if (json == null) break;
                        byte[] data = Encode(json);
                        byte[] header = { (byte)data.Length, (byte)(data.Length >> 8), (byte)(data.Length >> 16), (byte)(data.Length >> 24) };
                        await stream.WriteAsync(header, 0, 4, stop.Token).ConfigureAwait(false);
                        await stream.WriteAsync(data, 0, data.Length, stop.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception error) { if (Connected) Fail(error.Message); }
        }
        static async Task ReadExactly(Stream stream, byte[] buffer, CancellationToken token)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int count = await stream.ReadAsync(buffer, offset, buffer.Length - offset, token).ConfigureAwait(false);
                if (count == 0) throw new EndOfStreamException();
                offset += count;
            }
        }
        public static byte[] Encode(string json)
        {
            byte[] raw = System.Text.Encoding.UTF8.GetBytes(json);
            if (raw.Length > MaximumFrame) throw new InvalidDataException("Paquete demasiado grande");
            using (var output = new MemoryStream())
            {
                using (var zip = new DeflateStream(output, CompressionLevel.Fastest, true)) zip.Write(raw, 0, raw.Length);
                return output.ToArray();
            }
        }
        public static string Decode(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var zip = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192]; int count;
                while ((count = zip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > MaximumFrame) throw new InvalidDataException("Paquete descomprimido demasiado grande");
                    output.Write(buffer, 0, count);
                }
                return System.Text.Encoding.UTF8.GetString(output.ToArray());
            }
        }
        void Fail(string message) { Error = message; Dispose(); }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) != 0) return;
            stop.Cancel(); socket.Close();
        }
    }
}
