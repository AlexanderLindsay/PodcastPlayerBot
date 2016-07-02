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
        private object dowloadingLock = new object();
        private object finishedLock = new object();
        private object playingLock = new object();

        private bool IsDownloading { get; set; }
        private bool IsDoneDownloading { get; set; } = false;
        private bool IsPlaying { get; set; }

        private BufferedWaveProvider provider { get; set; } = null;

        public Speaker() { }

        public void Stop()
        {
            provider.ClearBuffer();

            lock (dowloadingLock)
            {
                IsDownloading = false;
            }
            lock(playingLock)
            {
                IsPlaying = false;
            }
        }

        public bool IsSpeaking()
        {
            var temp = false;

            lock (playingLock)
            {
                temp = IsPlaying;
            }

            return temp;
        }

        public void Play(int channelCount, Action<byte[], int, int> addToBuffer)
        {
            lock (playingLock)
            {
                if (IsPlaying)
                    return;

                IsPlaying = true;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                var outFormat = new WaveFormat(48000, 16, channelCount);
                var keepPlaying = true;

                while (provider == null)
                {
                    Thread.Sleep(500);
                }

                using (var resampler = new MediaFoundationResampler(provider, outFormat))
                {
                    resampler.ResamplerQuality = 60;

                    do
                    {
                        lock (playingLock)
                        {
                            if (!IsPlaying)
                            {
                                break;
                            }
                        }

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

                            lock (playingLock)
                            {
                                if (IsPlaying)
                                {
                                    addToBuffer(adjustedBuffer, 0, blockSize); // Send the buffer to Discord
                                }
                            }
                        }

                        lock (finishedLock)
                        {
                            keepPlaying = !IsDoneDownloading;
                        }

                        keepPlaying = keepPlaying || provider.BufferedBytes > 0;

                    } while (keepPlaying);
                }
            });
        }

        public void Load(string url, Action<string> reportError)
        {
            Load(new Uri(url), reportError);
        }

        public void Load(Uri url, Action<string> reportError)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(url);
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

                lock (dowloadingLock)
                {
                    IsDownloading = true;
                }

                IMp3FrameDecompressor decompressor = null;

                try
                {
                    using (var responseStream = resp.GetResponseStream())
                    {
                        var readFullyStream = new ReadFullyStream(responseStream);

                        Mp3Frame frame;
                        List<Mp3Frame> frames = Enumerable.Range(0, 10).Select(i => Mp3Frame.LoadFromStream(readFullyStream)).ToList();

                        while (frames
                            .Where(f => f != null)
                            .Select(f => new { SampleRate = f.SampleRate, ChannelMode = f.ChannelMode })
                            .Distinct().Count() != 1)
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

                                    lock (dowloadingLock)
                                    {
                                        IsDownloading = false;
                                    }

                                    lock (finishedLock)
                                    {
                                        IsDoneDownloading = true;
                                    }
                                    break;
                                }
                                catch (WebException)
                                {
                                    // probably we have aborted download from the GUI thread

                                    lock (finishedLock)
                                    {
                                        IsDoneDownloading = true;
                                    }
                                    break;
                                }
                                if (decompressor == null)
                                {
                                    decompressor = CreateFrameDecompressor(frame);
                                }

                                if(frame == null)
                                {
                                    lock (finishedLock)
                                    {
                                        IsDoneDownloading = true;
                                    }

                                    lock (dowloadingLock)
                                    {
                                        IsDownloading = false;
                                    }
                                    break;
                                }

                                int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                                if (provider == null)
                                {
                                    provider = new BufferedWaveProvider(decompressor.OutputFormat);
                                    provider.BufferDuration = TimeSpan.FromSeconds(20);
                                }
                                provider.AddSamples(buffer, 0, decompressed);
                            }

                            lock (dowloadingLock)
                            {
                                keepPlaying = IsDownloading;
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
            });
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