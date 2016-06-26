using NAudio.Wave;
using System;
using System.Configuration;
using System.Threading;

namespace PodcastPlayerDiscordBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var token = ConfigurationManager.AppSettings["token"];

            using (var bot = new Bot("Podcast Bot"))
            {
                bot.Start(token);
            }

            //var url = "http://traffic.libsyn.com/theblacktapes/THE_BLACK_TAPES_EPISODE_207_-_Personal_Possessions.mp3";
            //var speaker = new Speaker(url);

            //BufferedWaveProvider bufferedWaveProvider = null;

            //ThreadPool.QueueUserWorkItem(delegate
            //{
            //    speaker.Play(
            //        () =>
            //        {
            //            return bufferedWaveProvider != null &&
            //               bufferedWaveProvider.BufferLength - bufferedWaveProvider.BufferedBytes
            //               < bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;
            //        },
            //        (b, offset, count, format) =>
            //        {
            //            if (bufferedWaveProvider == null)
            //            {
            //                bufferedWaveProvider = new BufferedWaveProvider(format);
            //                bufferedWaveProvider.BufferDuration = TimeSpan.FromSeconds(20);
            //            }
            //            bufferedWaveProvider.AddSamples(b, offset, count);
            //        },
            //        (message) =>
            //        {
            //            Console.WriteLine(message);
            //        });
            //});

            //Console.ReadLine();

            //using (var waveOut = new WaveOut(WaveCallbackInfo.FunctionCallback()))
            //{
            //    var volumeProvider = new VolumeWaveProvider16(bufferedWaveProvider);
            //    waveOut.Init(volumeProvider);
            //    waveOut.Play();
            //    while (waveOut.PlaybackState == PlaybackState.Playing)
            //    {
            //        Thread.Sleep(100);
            //    }
            //}
        }
    }
}
