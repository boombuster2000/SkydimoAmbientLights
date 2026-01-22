namespace SkydimoAmbientLights;

public static class Program
{
    private static void Main()
    {
        using var driver = new SkydimoLedDriver("/dev/ttyUSB0", 60);
        if (!driver.Open())
        {
            Console.WriteLine("Failed to open serial port");
            return;
        }

        Console.WriteLine("Skydimo LED Controller Ready!");
        Console.WriteLine($"Controlling {driver.LedCount} LEDs");
        
        // Keep sending the same frame periodically
        var keepAliveTimer = new System.Timers.Timer(1000);
        keepAliveTimer.Elapsed += (_, _) => driver.Fill(255, 0, 0);
        keepAliveTimer.Start();

        Console.ReadLine();
        keepAliveTimer.Stop();

    }
}