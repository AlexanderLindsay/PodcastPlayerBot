using Discord.Audio;
using PodcastPlayerDiscordBot;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace BotJob
{
    public class AzureSpeaker : ISpeaker
    {
        private string _path = "tempstream.temp";
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

            await DownloadFile(url, _path);
            var ffmpeg = CreateStream(_path);
            var output = ffmpeg.StandardOutput.BaseStream;
            var discord = client.CreatePCMStream(AudioApplication.Mixed, 1920);
            await output.CopyToAsync(discord);
        }

        private async Task DownloadFile(Uri url, string path)
        {
            File.Delete(path);

            using (var source = WebRequest.Create(url.ToString())
                   .GetResponse().GetResponseStream())
            {
                using (var dest = File.OpenWrite(path))
                {
                    await source.CopyToAsync(dest);
                }
            }
        }

        private Process CreateStream(string path)
        {
            var ffmpeg = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i {path} -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            return Process.Start(ffmpeg);
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
