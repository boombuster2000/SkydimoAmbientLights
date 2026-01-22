using System.IO.Ports;

namespace SkydimoAmbientLights;

/// <summary>
/// Represents an RGB color with values from 0-255 for each channel.
/// </summary>
/// <param name="r">Red channel intensity (0-255)</param>
/// <param name="g">Green channel intensity (0-255)</param>
/// <param name="b">Blue channel intensity (0-255)</param>
public struct ColorRgb(byte r, byte g, byte b)
{
    /// <summary>Red channel intensity (0-255)</summary>
    public readonly byte R = r;
    
    /// <summary>Green channel intensity (0-255)</summary>
    public readonly byte G = g;
    
    /// <summary>Blue channel intensity (0-255)</summary>
    public readonly byte B = b;
}

/// <summary>
/// Driver for controlling addressable RGB LED strips using the Adalight protocol over serial communication.
/// </summary>
/// <remarks>
/// This driver supports LED strips that use the Adalight protocol, which is commonly used with
/// Arduino-based LED controllers. The protocol sends data as: 'Ada' header + LED count + RGB data.
/// 
/// <para><b>Basic Usage:</b></para>
/// <code>
/// using var driver = new SkydimoLedDriver("COM3", 60);
/// if (driver.Open())
/// {
///     driver.Fill(255, 0, 0);        // All LEDs red
///     driver.Rainbow();               // Rainbow effect
///     driver.Clear();                 // Turn off all LEDs
/// }
/// </code>
/// 
/// <para><b>Thread Safety:</b> This class is not thread-safe. External synchronization is required
/// if accessing from multiple threads.</para>
/// </remarks>
public class SkydimoLedDriver : IDisposable
{
    private readonly SerialPort _serialPort;
    private byte[] _ledBuffer = null!;
    private const int HeaderSize = 6;
    private readonly ColorRgb[] _currentColors;

    /// <summary>
    /// Gets the total number of LEDs this driver is configured to control.
    /// </summary>
    public int LedCount { get; }

    /// <summary>
    /// Initializes a new instance of the SkydimoLedDriver for controlling addressable LED strips.
    /// </summary>
    /// <param name="portName">The serial port name (e.g., "COM3" on Windows, "/dev/ttyUSB0" on Linux)</param>
    /// <param name="ledCount">The total number of LEDs in the strip (maximum 255)</param>
    /// <param name="baudRate">The baud rate for serial communication (default: 115200). Must match the controller's configuration.</param>
    /// <exception cref="ArgumentException">Thrown when portName is null or empty</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when ledCount is less than 1</exception>
    /// <remarks>
    /// The constructor does not open the serial port. Call <see cref="Open"/> to establish the connection.
    /// Common baud rates: 9600, 57600, 115200. Higher rates allow faster LED updates.
    /// </remarks>
    public SkydimoLedDriver(string portName, int ledCount, int baudRate = 115200)
    {
        LedCount = ledCount;
        _currentColors = new ColorRgb[ledCount];
        
        _serialPort = new SerialPort(portName, baudRate)
        {
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        CreateHeader();
    }

    /// <summary>
    /// Creates the Adalight protocol header for LED communication.
    /// </summary>
    /// <remarks>
    /// The Adalight protocol header format is:
    /// - Bytes 0-2: 'Ada' magic word identifier
    /// - Bytes 3-4: High byte and low byte of LED count minus 1 (currently set to 0)
    /// - Byte 5: XOR checksum of bytes 3-4 XOR 0x55 (currently simplified to LED count)
    /// 
    /// This method is called automatically during construction.
    /// </remarks>
    private void CreateHeader()
    {
        var bufferSize = HeaderSize + (LedCount * 3);
        _ledBuffer = new byte[bufferSize];

        // Adalight protocol header
        _ledBuffer[0] = (byte)'A';
        _ledBuffer[1] = (byte)'d';
        _ledBuffer[2] = (byte)'a';
        _ledBuffer[3] = 0;
        _ledBuffer[4] = 0;
        _ledBuffer[5] = (byte)Math.Min(LedCount, 255); // Number of LEDs

        Console.WriteLine($"Skydimo header created: 0x{_ledBuffer[0]:X2} 0x{_ledBuffer[1]:X2} 0x{_ledBuffer[2]:X2} " +
                          $"0x{_ledBuffer[3]:X2} 0x{_ledBuffer[4]:X2} 0x{_ledBuffer[5]:X2}");
    }
    
    /// <summary>
    /// Opens the serial port connection to the LED controller.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the port was opened successfully or was already open; 
    /// <c>false</c> if opening failed due to an error.
    /// </returns>
    /// <remarks>
    /// <para>Common failure reasons:</para>
    /// <list type="bullet">
    /// <item>Port is already in use by another application</item>
    /// <item>Port name is invalid or device is not connected</item>
    /// <item>Insufficient permissions to access the port</item>
    /// </list>
    /// <para>Always check the return value before attempting to control LEDs.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var driver = new SkydimoLedDriver("COM3", 60);
    /// if (!driver.Open())
    /// {
    ///     Console.WriteLine("Failed to open port. Check if another app is using it.");
    ///     return;
    /// }
    /// </code>
    /// </example>
    public bool Open()
    {
        try
        {
            if (_serialPort.IsOpen) return true;
            
            _serialPort.Open();
            Console.WriteLine($"Opened port {_serialPort.PortName} for {LedCount} LEDs");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening serial port: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Closes the serial port connection.
    /// </summary>
    /// <remarks>
    /// This method is called automatically by <see cref="Dispose"/>.
    /// After closing, you must call <see cref="Open"/> again to resume communication.
    /// </remarks>
    private void Close()
    {
        if (!_serialPort.IsOpen) return;
        
        _serialPort.Close();
        Console.WriteLine("Serial port closed");
    }
    
    /// <summary>
    /// Sends color data to the LED strip.
    /// </summary>
    /// <param name="colors">Array of RGB colors, one per LED. Must match the configured LED count.</param>
    /// <returns><c>true</c> if the data was sent successfully; <c>false</c> otherwise.</returns>
    /// <remarks>
    /// This is the core method that all other color-setting methods use internally.
    /// It updates the internal color state and transmits the data via serial port.
    /// The serial port must be open before calling this method.
    /// </remarks>
    private bool WriteColors(ColorRgb[] colors)
    {
        if (colors.Length != LedCount)
        {
            Console.WriteLine($"LED count mismatch. Expected {LedCount}, got {colors.Length}");
            return false;
        }

        try
        {
            // Update current color state
            Array.Copy(colors, _currentColors, LedCount);
            
            var offset = HeaderSize;
            for (var i = 0; i < colors.Length; i++)
            {
                _ledBuffer[offset++] = colors[i].R;
                _ledBuffer[offset++] = colors[i].G;
                _ledBuffer[offset++] = colors[i].B;
            }

            if (_serialPort.IsOpen)
            {
                _serialPort.Write(_ledBuffer, 0, _ledBuffer.Length);
                return true;
            }

            Console.WriteLine("Serial port is not open");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to serial port: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Sets all LEDs to the same color.
    /// </summary>
    /// <param name="r">Red channel intensity (0-255)</param>
    /// <param name="g">Green channel intensity (0-255)</param>
    /// <param name="b">Blue channel intensity (0-255)</param>
    /// <returns><c>true</c> if successful; <c>false</c> otherwise.</returns>
    /// <example>
    /// <code>
    /// driver.Fill(255, 0, 0);      // All LEDs red
    /// driver.Fill(0, 255, 0);      // All LEDs green
    /// driver.Fill(128, 128, 255);  // All LEDs light blue
    /// </code>
    /// </example>
    public bool Fill(byte r, byte g, byte b)
    {
        var colors = new ColorRgb[LedCount];
        
        for (var i = 0; i < LedCount; i++)
            colors[i] = new ColorRgb(r, g, b);
        
        return WriteColors(colors);
    }

    /// <summary>
    /// Sets all LEDs to the same color using a ColorRgb struct.
    /// </summary>
    /// <param name="color">The color to apply to all LEDs</param>
    /// <returns><c>true</c> if successful; <c>false</c> otherwise.</returns>
    /// <example>
    /// <code>
    /// var purple = new ColorRgb(128, 0, 128);
    /// driver.Fill(purple);
    /// </code>
    /// </example>
    public bool Fill(ColorRgb color)
    {
        return Fill(color.R, color.G, color.B);
    }

    /// <summary>
    /// Turns off all LEDs by setting them to black (0, 0, 0).
    /// </summary>
    /// <returns><c>true</c> if successful; <c>false</c> otherwise.</returns>
    /// <example>
    /// <code>
    /// driver.Clear();  // All LEDs off
    /// </code>
    /// </example>
    public bool Clear()
    {
        return Fill(0, 0, 0);
    }

    /// <summary>
    /// Sets a specific LED to a color while preserving the colors of all other LEDs.
    /// </summary>
    /// <param name="index">Zero-based index of the LED to set (0 to LedCount-1)</param>
    /// <param name="r">Red channel intensity (0-255)</param>
    /// <param name="g">Green channel intensity (0-255)</param>
    /// <param name="b">Blue channel intensity (0-255)</param>
    /// <returns><c>true</c> if successful; <c>false</c> if index is out of range or write fails.</returns>
    /// <example>
    /// <code>
    /// driver.Clear();
    /// driver.SetLed(0, 255, 0, 0);   // First LED red
    /// driver.SetLed(5, 0, 255, 0);   // Sixth LED green
    /// </code>
    /// </example>
    public bool SetLed(int index, byte r, byte g, byte b)
    {
        if (index < 0 || index >= LedCount)
        {
            Console.WriteLine($"LED index {index} out of range (0-{LedCount - 1})");
            return false;
        }

        ColorRgb[] colors = GetCurrentColors();
        colors[index] = new ColorRgb(r, g, b);
        return WriteColors(colors);
    }

    /// <summary>
    /// Creates a rainbow effect across all LEDs using full saturation and brightness.
    /// </summary>
    /// <param name="offset">Hue rotation offset. Increment this value over time to animate the rainbow.</param>
    /// <returns><c>true</c> if successful; <c>false</c> otherwise.</returns>
    /// <remarks>
    /// The rainbow spans the entire 360-degree hue spectrum evenly across all LEDs.
    /// Each LED's hue is calculated as: (LED_index + offset) / LED_count * 360 degrees.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Static rainbow
    /// driver.Rainbow();
    /// 
    /// // Animated rainbow (call in a loop)
    /// for (int i = 0; i &lt; 360; i++)
    /// {
    ///     driver.Rainbow(i);
    ///     Thread.Sleep(50);
    /// }
    /// </code>
    /// </example>
    public bool Rainbow(int offset = 0)
    {
        var colors = new ColorRgb[LedCount];
        
        for (var i = 0; i < LedCount; i++)
        {
            var hue = ((float)(i + offset) / LedCount) * 360f;
            colors[i] = HsvToRgb(hue, 1.0f, 1.0f);
        }
        
        return WriteColors(colors);
    }

    /// <summary>
    /// Creates a smooth color gradient from one color to another across all LEDs.
    /// </summary>
    /// <param name="startColor">The color of the first LED</param>
    /// <param name="endColor">The color of the last LED</param>
    /// <returns><c>true</c> if successful; <c>false</c> otherwise.</returns>
    /// <remarks>
    /// Colors are interpolated linearly in RGB space. Each LED's color is calculated by
    /// blending the start and end colors based on the LED's position in the strip.
    /// </remarks>
    /// <example>
    /// <code>
    /// var red = new ColorRgb(255, 0, 0);
    /// var blue = new ColorRgb(0, 0, 255);
    /// driver.Gradient(red, blue);  // Smooth red-to-blue gradient
    /// </code>
    /// </example>
    public bool Gradient(ColorRgb startColor, ColorRgb endColor)
    {
        var colors = new ColorRgb[LedCount];
        
        for (var i = 0; i < LedCount; i++)
        {
            var ratio = (float)i / (LedCount - 1);
            
            var r = (byte)(startColor.R + (endColor.R - startColor.R) * ratio);
            var g = (byte)(startColor.G + (endColor.G - startColor.G) * ratio);
            var b = (byte)(startColor.B + (endColor.B - startColor.B) * ratio);
            
            colors[i] = new ColorRgb(r, g, b);
        }
        
        return WriteColors(colors);
    }
    
    /// <summary>
    /// Gets a copy of the current color state of all LEDs.
    /// </summary>
    /// <returns>An array containing the current color of each LED.</returns>
    /// <remarks>
    /// Returns a clone to prevent external modification of the internal state.
    /// This reflects the last successfully written color state, not necessarily
    /// what the physical LEDs are showing if there was a communication error.
    /// </remarks>
    private ColorRgb[] GetCurrentColors()
    {
        return (ColorRgb[])_currentColors.Clone();
    }

    /// <summary>
    /// Converts HSV (Hue, Saturation, Value) color space to RGB.
    /// </summary>
    /// <param name="h">Hue in degrees (0-360)</param>
    /// <param name="s">Saturation (0.0-1.0)</param>
    /// <param name="v">Value/Brightness (0.0-1.0)</param>
    /// <returns>RGB color equivalent</returns>
    /// <remarks>
    /// HSV color space is more intuitive for creating color effects:
    /// - Hue: Color type (0=red, 120=green, 240=blue, 360=red again)
    /// - Saturation: Color intensity (0=gray, 1=vibrant)
    /// - Value: Brightness (0=black, 1=full brightness)
    /// </remarks>
    private static ColorRgb HsvToRgb(float h, float s, float v)
    {
        int hi = (int)(h / 60f) % 6;
        float f = h / 60f - (int)(h / 60f);
        float p = v * (1 - s);
        float q = v * (1 - f * s);
        float t = v * (1 - (1 - f) * s);

        float r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return new ColorRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    /// <summary>
    /// Releases all resources used by the SkydimoLedDriver, including closing the serial port.
    /// </summary>
    /// <remarks>
    /// This method is called automatically when using the 'using' statement.
    /// After disposal, the driver instance should not be used further.
    /// </remarks>
    public void Dispose()
    {
        Close();
        _serialPort.Dispose();
    }
}