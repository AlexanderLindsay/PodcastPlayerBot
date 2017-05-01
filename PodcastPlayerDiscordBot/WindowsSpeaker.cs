using Discord.Audio;
using NAudio.Wave;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace PodcastPlayerDiscordBot
{
    public class WindowsSpeaker : ISpeaker
    {
        private IAudioClient _client;

        private bool _isPlaying = false;

        public event EventHandler<FinishedPlayingEventArgs> FinishedPlaying = delegate { };

        public async Task StopAsync()
        {
            _isPlaying = false;

            if (_client != null)
            {
                await _client.StopAsync();
                FinishedPlaying.Invoke(this, new FinishedPlayingEventArgs(true));
            }
        }

        public Task<bool> IsPlayingAsync()
        {
            return Task.FromResult(_isPlaying);
        }

        public async Task PlayUrlAsync(Uri url, IAudioClient client)
        {
            _client = client;
            _client.SpeakingUpdated += OnSpeakingUpdated;
            _isPlaying = true;

            using (var ms = new MemoryStream())
            {
                using (Stream stream = WebRequest.Create(url.ToString())
                    .GetResponse().GetResponseStream())
                {
                    byte[] buffer = new byte[32768];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                    }
                }

                ms.Position = 0;
                using (WaveStream blockAlignedStream =
                    new BlockAlignReductionStream(
                        WaveFormatConversionStream.CreatePcmStream(
                            new Mp3FileReader(ms))))
                {
                    var audioStream = _client.CreatePCMStream(AudioApplication.Mixed, 1920);
                    await blockAlignedStream.CopyToAsync(audioStream);
                }
            }
        }

        private Task OnSpeakingUpdated(ulong userId, bool isPlaying)
        {
            _isPlaying = isPlaying;

            if (!isPlaying)
            {
                FinishedPlaying.Invoke(this, new FinishedPlayingEventArgs());
            }

            return Task.CompletedTask;
        }
    }
}