using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace PodcastPlayerDiscordBot
{
    public class Speaker
    {
        private object playerLock = new object();
        private object finishedLock = new object();

        private Uri source { get; set; }
        private bool IsPlaying { get; set; }
        private bool IsDone { get; set; } = false;

        private BufferedWaveProvider provider { get; set; } = null;

        public Speaker(string url) : this(new Uri(url)) { }

        public Speaker(Uri url)
        {
            source = url;
        }

        public void Stop()
        {
            lock (playerLock)
            {
                IsPlaying = false;
            }
        }

        public void Play(int channelCount, Action<byte[], int, int> addToBuffer)
        {
            var outFormat = new WaveFormat(48000, 16, channelCount);
            var keepPlaying = true;

            while(provider == null)
            {
                Thread.Sleep(500);
            }

            using (var resampler = new MediaFoundationResampler(provider, outFormat)) {
                resampler.ResamplerQuality = 60;

                do
                {
                    int blockSize = outFormat.AverageBytesPerSecond / 50;
                    byte[] adjustedBuffer = new byte[blockSize];
                    int byteCount;

                    if ((byteCount = resampler.Read(adjustedBuffer, 0, blockSize)) > 0)
                    {
                        if (byteCount < blockSize)
                        {
                            // Incomplete Frame
                            for (int i = byteCount; i < blockSize; i++)
                                adjustedBuffer[i] = 0;
                        }
                        addToBuffer(adjustedBuffer, 0, blockSize); // Send the buffer to Discord
                    }

                    lock (finishedLock)
                    {
                        keepPlaying = !IsDone;
                    }

                    keepPlaying = keepPlaying || provider.BufferedBytes > 0;

                } while (keepPlaying);
            }
        }

        public void Load(Action<string> reportError)
        {
            var webRequest = (HttpWebRequest)WebRequest.Create(source);
            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)webRequest.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Status != WebExceptionStatus.RequestCanceled)
                {
                    reportError(e.Message);
                }
                return;
            }
            var buffer = new byte[16384 * 4];

            lock (playerLock)
            {
                IsPlaying = true;
            }

            IMp3FrameDecompressor decompressor = null;

            try
            {
                using (var responseStream = resp.GetResponseStream())
                {
                    var readFullyStream = new ReadFullyStream(responseStream);

                    Mp3Frame frame;
                    List<Mp3Frame> frames = Enumerable.Range(0, 10).Select(i => Mp3Frame.LoadFromStream(readFullyStream)).ToList();

                    while (frames.Select(f => new { SampleRate = f.SampleRate, ChannelMode = f.ChannelMode }).Distinct().Count() != 1)
                    {
                        frames.RemoveAt(0);
                        frames.Add(Mp3Frame.LoadFromStream(readFullyStream));
                    }

                    bool keepPlaying;

                    do
                    {
                        if (ShouldPauseBuffering(provider))
                        {
                            Thread.Sleep(500);
                        }
                        else
                        {
                            try
                            {
                                if (frames.Any())
                                {
                                    frame = frames.First();
                                    frames.RemoveAt(0);
                                }
                                else
                                {
                                    frame = Mp3Frame.LoadFromStream(readFullyStream);
                                }
                            }
                            catch (EndOfStreamException)
                            {
                                // reached the end of the MP3 file / stream

                                lock (playerLock)
                                {
                                    IsPlaying = false;
                                }

                                lock (finishedLock)
                                {
                                    IsDone = true;
                                }
                                break;
                            }
                            catch (WebException)
                            {
                                // probably we have aborted download from the GUI thread

                                lock (finishedLock)
                                {
                                    IsDone = true;
                                }
                                break;
                            }
                            if (decompressor == null)
                            {
                                decompressor = CreateFrameDecompressor(frame);
                            }
                            int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                            if (provider == null)
                            {
                                provider = new BufferedWaveProvider(decompressor.OutputFormat);
                                provider.BufferDuration = TimeSpan.FromSeconds(20);
                            }
                            provider.AddSamples(buffer, 0, decompressed);
                        }

                        lock (playerLock)
                        {
                            keepPlaying = IsPlaying;
                        }

                    } while (keepPlaying);

                    // was doing this in a finally block, but for some reason
                    // we are hanging on response stream .Dispose so never get there
                    decompressor.Dispose();
                }
            }
            finally
            {
                if (decompressor != null)
                {
                    decompressor.Dispose();
                }
            }
        }

        private bool ShouldPauseBuffering(BufferedWaveProvider provider)
        {
            return provider != null &&
               provider.BufferLength - provider.BufferedBytes
               < provider.WaveFormat.AverageBytesPerSecond / 4;
        }

        private static IMp3FrameDecompressor CreateFrameDecompressor(Mp3Frame frame)
        {
            WaveFormat waveFormat = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                frame.FrameLength, frame.BitRate);
            return new AcmMp3FrameDecompressor(waveFormat);

        }
    }
}