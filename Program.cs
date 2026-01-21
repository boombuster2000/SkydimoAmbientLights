namespace SkydimoAmbientLights;

// Example usage
public static class Program
{
    private static void Main()
    {
        // Create driver for 60 LEDs on COM3
        using var skydimo = new SkydimoLedDriver("/dev/ttyUSB0", 60);
        if (!skydimo.Open())
        {
            Console.WriteLine("Failed to open serial port");
            return;
        }

        Console.WriteLine("Skydimo LED Controller Ready!");
        Console.WriteLine($"Controlling {skydimo.LedCount} LEDs");

        // Fill all LEDs with red
        Console.WriteLine("\nFilling with red...");
        skydimo.Fill(255, 0, 0);
        Thread.Sleep(1000);

        // Fill all LEDs with green
        Console.WriteLine("Filling with green...");
        skydimo.Fill(0, 255, 0);
        Thread.Sleep(1000);

        // Fill all LEDs with blue
        Console.WriteLine("Filling with blue...");
        skydimo.Fill(0, 0, 255);
        Thread.Sleep(1000);

        // Rainbow effect
        Console.WriteLine("Rainbow effect...");
        for (var i = 0; i < 100; i++)
        {
            skydimo.Rainbow(i * 3);
            Thread.Sleep(50);
        }

        // Gradient effect
        Console.WriteLine("Gradient effect...");
        skydimo.Gradient(new ColorRgb(255, 0, 0), new ColorRgb(0, 0, 255));
        Thread.Sleep(2000);

        // Turn off all LEDs
        Console.WriteLine("Clearing LEDs...");
        skydimo.Clear();

        Console.WriteLine("\nDemo complete!");
    }
}