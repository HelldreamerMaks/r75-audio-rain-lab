using HidSharp;
using NAudio.Wave;
using System.Diagnostics;

internal static class Program
{
    private const int VendorId = 0x342D;
    private const int ProductId = 0xE483;

    private const byte ViaGetProtocolVersion = 0x01;
    private const byte SignalRgbGetProtocolVersion = 0x22;

    // Verified in snakkarike/qmk_firmware OpenSignalRGB-2026/quantum/signalrgb.h.
    private const byte ExpectedSignalRgbMajor = 1;
    private const byte ExpectedSignalRgbMinor = 0;
    private const byte ExpectedSignalRgbPatch = 5;

    // Private command handled only by the matching v12.1 keymap overlay.
    private const byte AudioRainGateCommand = 0x29;
    private const byte AudioRainMagic = 0xA7;
    private const byte AudioRainProtocolVersion = 0x01;

    // 10 keepalives/s. Firmware fails closed after 350 ms without a valid packet.
    private const int ControlPeriodMs = 100;

    // Single behavior only: general system audio present => allow NEW drops.
    // No FFT, no bass/beat, no brightness/speed/color control.
    private const double GateOnDbFs = -56.0;
    private const double GateOffDbFs = -64.0;
    private const int SilenceConfirmMs = 100;

    public static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("R75 Audio-Spawn Digital Rain — v12.1 fork-corrected");
        Console.WriteLine("ОДНА функция: общий звук разрешает появление НОВЫХ капель.");
        Console.WriteLine("Тишина запрещает новые капли; уже созданные продолжают движение внутри QMK.");
        Console.WriteLine("Нет Brightness/Speed/Color control, FFT, bass/beat, EEPROM, DFU или per-LED streaming.");
        Console.WriteLine();

        using AudioRainKeyboard? keyboard = FindAndOpenRawHid();
        if (keyboard is null)
        {
            Console.WriteLine("STOP: проверенный Raw HID интерфейс R75 342D:E483 не найден.");
            WaitForExit();
            return;
        }

        try
        {
            ushort viaProtocol = keyboard.GetViaProtocolVersion();
            Console.WriteLine($"Устройство: {keyboard.Device.GetProductName()}");
            Console.WriteLine($"VID:PID = {VendorId:X4}:{ProductId:X4}");
            Console.WriteLine($"VIA protocol = 0x{viaProtocol:X4}");

            if (viaProtocol != 0x000C)
                throw new IOException("Safety gate: ожидался VIA protocol 0x000C.");

            // IMPORTANT for this exact fork:
            // 0x22 is a normal SignalRGB GET command. The fork's VIA router uses it to
            // switch routing away from default OpenRGB mode, after which private 0x29
            // reaches via_command_kb() in our keymap-level audio_rain.c.
            var srgb = keyboard.GetSignalRgbProtocolVersion();
            Console.WriteLine($"SignalRGB protocol = {srgb.Major}.{srgb.Minor}.{srgb.Patch}");

            if (srgb != (ExpectedSignalRgbMajor, ExpectedSignalRgbMinor, ExpectedSignalRgbPatch))
                throw new IOException(
                    $"Safety gate: ожидался SignalRGB {ExpectedSignalRgbMajor}.{ExpectedSignalRgbMinor}.{ExpectedSignalRgbPatch} именно из OpenSignalRGB-2026 fork.");

            // Safest first private command: spawning OFF.
            keyboard.SetAudioRainGate(false);
            Console.WriteLine("Audio-Rain protocol v1 ACK получен. Старт: NEW DROPS OFF.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"STOP: {ex.Message}");
            Console.WriteLine("На заводской firmware timeout на SignalRGB/0x29 ожидаем: host требует matching custom firmware.");
            WaitForExit();
            return;
        }

#pragma warning disable CS0618
        using var capture = new WasapiLoopbackCapture();
#pragma warning restore CS0618
        var analyzer = new RmsAnalyzer();
        capture.DataAvailable += (_, e) => analyzer.Process(e.Buffer, e.BytesRecorded, capture.WaveFormat);

        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            Console.WriteLine($"Audio format: {capture.WaveFormat}");
            Console.WriteLine($"Gate ON >= {GateOnDbFs:F0} dBFS | OFF <= {GateOffDbFs:F0} dBFS for {SilenceConfirmMs} ms");
            Console.WriteLine("ENTER/Ctrl+C — stop. Stop sends SPAWN=OFF; RGB effect itself is NOT turned off.");
            Console.WriteLine();

            capture.StartRecording();
            Task loopTask = RunAudioGateAsync(keyboard, analyzer, cts.Token);
            Task inputTask = Task.Run(Console.ReadLine);
            Task cancelTask = WaitForCancellationAsync(cts.Token);
            await Task.WhenAny(inputTask, cancelTask);
            cts.Cancel();
            capture.StopRecording();
            await IgnoreCancellation(loopTask);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;

            // Best effort. Even if these fail, firmware watchdog closes SPAWN automatically.
            for (int i = 0; i < 3; i++)
            {
                try { keyboard.SetAudioRainGate(false); }
                catch { }
                await Task.Delay(25);
            }

            Console.WriteLine("\nNEW DROPS OFF. Existing drops remain firmware-owned and may finish naturally.");
        }

        WaitForExit();
    }

    // The only effect behavior exposed to the user.
    private static async Task RunAudioGateAsync(
        AudioRainKeyboard keyboard,
        RmsAnalyzer analyzer,
        CancellationToken token)
    {
        bool gate = false;
        long belowOffSince = -1;
        var clock = Stopwatch.StartNew();

        while (!token.IsCancellationRequested)
        {
            double rms = analyzer.GetRms();
            double db = 20.0 * Math.Log10(Math.Max(rms, 1e-9));

            if (!gate)
            {
                if (db >= GateOnDbFs)
                {
                    gate = true;
                    belowOffSince = -1;
                }
            }
            else if (db <= GateOffDbFs)
            {
                if (belowOffSince < 0)
                    belowOffSince = clock.ElapsedMilliseconds;

                if (clock.ElapsedMilliseconds - belowOffSince >= SilenceConfirmMs)
                {
                    gate = false;
                    belowOffSince = -1;
                }
            }
            else
            {
                belowOffSince = -1;
            }

            // Keepalive carries only one bit: may NEW drops spawn? yes/no.
            keyboard.SetAudioRainGate(gate);
            Console.Write($"\rAudio {db,7:F1} dBFS | NEW DROPS {(gate ? "ON " : "OFF")}                              ");
            await Task.Delay(ControlPeriodMs, token);
        }
    }

    private static AudioRainKeyboard? FindAndOpenRawHid()
    {
        List<HidDevice> devices = DeviceList.Local.GetHidDevices(VendorId, ProductId).ToList();
        Console.WriteLine($"HID interfaces {VendorId:X4}:{ProductId:X4}: {devices.Count}");

        foreach (HidDevice device in devices)
        {
            try
            {
                if (device.GetMaxInputReportLength() < 33 || device.GetMaxOutputReportLength() < 33)
                    continue;

                var candidate = new AudioRainKeyboard(device);
                try
                {
                    candidate.Open();
                    if (candidate.GetViaProtocolVersion() == 0x000C)
                        return candidate;
                }
                catch { }

                candidate.Dispose();
            }
            catch { }
        }

        return null;
    }

    private sealed class RmsAnalyzer
    {
        private readonly object _lock = new();
        private double _rms;

        public double GetRms()
        {
            lock (_lock) return _rms;
        }

        public void Process(byte[] buffer, int bytesRecorded, WaveFormat format)
        {
            if (bytesRecorded <= 0) return;

            int channels = Math.Max(1, format.Channels);
            int bytesPerSample = format.BitsPerSample / 8;
            int frameBytes = bytesPerSample * channels;
            if (bytesPerSample <= 0 || frameBytes <= 0) return;

            double sumSquares = 0.0;
            int frames = 0;

            for (int frame = 0; frame + frameBytes <= bytesRecorded; frame += frameBytes)
            {
                double mono = 0.0;
                bool valid = true;

                for (int ch = 0; ch < channels; ch++)
                {
                    int offset = frame + ch * bytesPerSample;
                    double sample;

                    if (format.BitsPerSample == 32)
                    {
                        float value = BitConverter.ToSingle(buffer, offset);
                        if (!float.IsFinite(value)) { valid = false; break; }
                        sample = Math.Clamp(value, -1f, 1f);
                    }
                    else if (format.BitsPerSample == 16)
                    {
                        sample = BitConverter.ToInt16(buffer, offset) / 32768.0;
                    }
                    else
                    {
                        return;
                    }

                    mono += sample;
                }

                if (!valid) continue;
                mono /= channels;
                sumSquares += mono * mono;
                frames++;
            }

            if (frames == 0) return;
            double blockRms = Math.Sqrt(sumSquares / frames);

            lock (_lock)
            {
                // Fast attack; light release smoothing only to avoid threshold chatter.
                double alpha = blockRms > _rms ? 0.70 : 0.35;
                _rms += (blockRms - _rms) * alpha;
            }
        }
    }

    private sealed class AudioRainKeyboard : IDisposable
    {
        public HidDevice Device { get; }
        private readonly object _ioLock = new();
        private HidStream? _stream;

        public AudioRainKeyboard(HidDevice device) => Device = device;

        public void Open()
        {
            _stream = Device.Open();
            _stream.ReadTimeout = 1000;
            _stream.WriteTimeout = 1000;
        }

        public ushort GetViaProtocolVersion()
        {
            byte[] request = new byte[32];
            request[0] = ViaGetProtocolVersion;
            byte[] response = ExchangePacket(request, ViaGetProtocolVersion);
            return (ushort)((response[1] << 8) | response[2]);
        }

        public (byte Major, byte Minor, byte Patch) GetSignalRgbProtocolVersion()
        {
            byte[] request = new byte[32];
            request[0] = SignalRgbGetProtocolVersion;
            byte[] response = ExchangePacket(request, SignalRgbGetProtocolVersion);
            return (response[1], response[2], response[3]);
        }

        public void SetAudioRainGate(bool enabled)
        {
            byte[] request = new byte[32];
            request[0] = AudioRainGateCommand;
            request[1] = AudioRainMagic;
            request[2] = AudioRainProtocolVersion;
            request[3] = enabled ? (byte)1 : (byte)0;

            byte[] response = ExchangePacket(request, AudioRainGateCommand);
            if (response[0] != AudioRainGateCommand ||
                response[1] != 0 ||
                response[2] != AudioRainMagic ||
                response[3] != AudioRainProtocolVersion ||
                response[4] != request[3])
            {
                throw new IOException($"Audio-Rain ACK invalid: {BitConverter.ToString(response, 0, 8)}");
            }
        }

        private byte[] ExchangePacket(byte[] payload32, byte expectedCommand)
        {
            if (_stream is null) throw new InvalidOperationException("HID stream не открыт.");
            if (payload32.Length != 32) throw new ArgumentException("Raw HID payload должен быть 32 bytes.");

            lock (_ioLock)
            {
                int outputLength = Device.GetMaxOutputReportLength();
                int inputLength = Device.GetMaxInputReportLength();
                if (outputLength < 33 || inputLength < 33)
                    throw new IOException($"Unexpected HID report size: in={inputLength}, out={outputLength}");

                byte[] output = new byte[outputLength];
                output[0] = 0; // Report ID
                Array.Copy(payload32, 0, output, 1, 32);
                _stream.Write(output);

                var deadline = Stopwatch.StartNew();
                while (deadline.ElapsedMilliseconds < 1000)
                {
                    byte[] input = new byte[inputLength];
                    int read = _stream.Read(input, 0, input.Length);
                    if (read <= 0) continue;

                    int offset = input[0] == 0 ? 1 : 0;
                    if (read - offset <= 0) continue;

                    byte[] response = new byte[32];
                    Array.Copy(input, offset, response, 0, Math.Min(32, read - offset));
                    if (response[0] == expectedCommand)
                        return response;
                }

                throw new TimeoutException($"Нет matching Raw HID response для 0x{expectedCommand:X2}.");
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    private static async Task WaitForCancellationAsync(CancellationToken token)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = token.Register(() => tcs.TrySetResult());
        await tcs.Task;
    }

    private static async Task IgnoreCancellation(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
    }

    private static void WaitForExit()
    {
        Console.WriteLine("Нажми ENTER для выхода.");
        Console.ReadLine();
    }
}
