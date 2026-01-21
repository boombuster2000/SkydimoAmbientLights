using System.IO.Ports;


namespace SkydimoAmbientLights;

public struct ColorRgb(byte r, byte g, byte b)
{
    public readonly byte R = r;
    public readonly byte G = g;
    public readonly byte B = b;
}

public class SkydimoLedDriver : IDisposable
    {
        private readonly SerialPort _serialPort;
        private byte[] _ledBuffer;
        private const int HeaderSize = 6;

        public int LedCount { get; }

        public SkydimoLedDriver(string portName, int ledCount, int baudRate = 115200)
        {
            LedCount = ledCount;
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

        private void Close()
        {
            if (_serialPort?.IsOpen != true) return;
            _serialPort.Close();
            Console.WriteLine("Serial port closed");
        }

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
            _ledBuffer[5] = (byte)Math.Min(LedCount, 255);

            Console.WriteLine($"Skydimo header created: 0x{_ledBuffer[0]:X2} 0x{_ledBuffer[1]:X2} 0x{_ledBuffer[2]:X2} " +
                            $"0x{_ledBuffer[3]:X2} 0x{_ledBuffer[4]:X2} 0x{_ledBuffer[5]:X2}");
        }

        private bool WriteColors(ColorRgb[] colors)
        {
            if (colors.Length != LedCount)
            {
                Console.WriteLine($"LED count mismatch. Expected {LedCount}, got {colors.Length}");
                return false;
            }

            try
            {
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
                else
                {
                    Console.WriteLine("Serial port is not open");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to serial port: {ex.Message}");
                return false;
            }
        }

        public bool Fill(byte r, byte g, byte b)
        {
            var colors = new ColorRgb[LedCount];
            
            for (var i = 0; i < LedCount; i++)
            {
                colors[i] = new ColorRgb(r, g, b);
            }
            
            return WriteColors(colors);
        }

        public bool Fill(ColorRgb color)
        {
            return Fill(color.R, color.G, color.B);
        }

        public bool Clear()
        {
            return Fill(0, 0, 0);
        }

        public bool SetLed(int index, byte r, byte g, byte b)
        {
            if (index < 0 || index >= LedCount)
            {
                Console.WriteLine($"LED index {index} out of range (0-{LedCount - 1})");
                return false;
            }

            var colors = GetCurrentColors();
            colors[index] = new ColorRgb(r, g, b);
            return WriteColors(colors);
        }

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

        private ColorRgb[] GetCurrentColors()
        {
            var colors = new ColorRgb[LedCount];
            var offset = HeaderSize;
            for (var i = 0; i < LedCount; i++)
            {
                colors[i] = new ColorRgb(
                    _ledBuffer[offset++],
                    _ledBuffer[offset++],
                    _ledBuffer[offset++]
                );
            }
            return colors;
        }

        private ColorRgb HsvToRgb(float h, float s, float v)
        {
            var hi = (int)(h / 60f) % 6;
            var f = h / 60f - (int)(h / 60f);
            var p = v * (1 - s);
            var q = v * (1 - f * s);
            var t = v * (1 - (1 - f) * s);

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

        public void Dispose()
        {
            Close();
            _serialPort?.Dispose();
        }
    }