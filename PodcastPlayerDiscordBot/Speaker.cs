using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PodcastPlayerDiscordBot
{
    public class Speaker
    {
        private object playerLock = new object();

        private Uri source { get; set; }
        private bool IsPlaying { get; set; }

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

        public void Play(Func<bool> shouldPauseBuffering, Action<byte[], int, int, WaveFormat> addToBuffer, Action<string> reportError)
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
                        if (shouldPauseBuffering())
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
                                lock (playerLock)
                                {
                                    IsPlaying = false;
                                }
                                // reached the end of the MP3 file / stream
                                break;
                            }
                            catch (WebException)
                            {
                                // probably we have aborted download from the GUI thread
                                break;
                            }
                            if (decompressor == null)
                            {
                                decompressor = CreateFrameDecompressor(frame);
                            }
                            int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                            addToBuffer(buffer, 0, decompressed, decompressor.OutputFormat);
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

        private static IMp3FrameDecompressor CreateFrameDecompressor(Mp3Frame frame)
        {
            WaveFormat waveFormat = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                frame.FrameLength, frame.BitRate);
            return new AcmMp3FrameDecompressor(waveFormat);

        }
    }
}