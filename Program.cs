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
        
        using var timer = new Timer(
            state => ((SkydimoLedDriver)state!).Fill(255, 0, 0),
            driver,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1)
        );

        Console.ReadLine();
    }
}