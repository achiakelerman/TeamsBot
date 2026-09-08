using NAudio.Wave;

var outputDirectory = Path.Combine(Environment.CurrentDirectory, "output", "audio");
Directory.CreateDirectory(outputDirectory);
var output = Path.Combine(outputDirectory, $"{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}.wav");
using var capture = new WasapiLoopbackCapture();
using var writer = new WaveFileWriter(output, capture.WaveFormat);
using var stopped = new ManualResetEventSlim(false);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; capture.StopRecording(); };
capture.DataAvailable += (_, e) => writer.Write(e.Buffer, 0, e.BytesRecorded);
capture.RecordingStopped += (_, _) => stopped.Set();
Console.WriteLine("System audio capture started. Use Ctrl+C to stop.");
Console.WriteLine($"Writing: {output}");
capture.StartRecording();
stopped.Wait();
Console.WriteLine("Capture stopped.");
