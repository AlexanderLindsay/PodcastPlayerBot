# [Podcast Player Bot](https://github.com/AlexanderLindsay/PodcastPlayerBot)

by Alexander Lindsay

Podcast player bot allows you to play podcasts in your [Discord](https://discordapp.com) server voice chat. You can add rss feeds to the bot to make it easier to share your favorite podcast with your friends on discord.

You are free to copy, modify, and distribute <PROJECT NAME> with attribution under the terms of the MIT license. See the LICENSE file for details.

## How to create your own bot

This bot is built in C# and uses the [Discord.Net](https://github.com/RogueException/Discord.Net) to interface with Discord. It will require Windows to run.

#### Create a discord bot
1. Navigate to your discord application page [here](https://discordapp.com/developers/applications/me).
2. Add a new application
3. Add an app bot user
4. Take the client id of your newly created bot and put it in this url: `https://discordapp.com/oauth2/authorize?client_id=clientidhere&scope=bot&permissions=0`
5. Add the bot to a server you own

#### Run Podcast Player Bot
1. Clone or download the source code
2. Add a secrets.config file to the PodcastPlayerDiscordBot folder that contains the token from your bot user and a file location to save the feeds too

```xml
<?xml version="1.0" encoding="utf-8" ?>
<appSettings>
  <add key="token" value="YourTokenHere"/>
  <add key="feedFile" value="C:\Your\feedFile\Path"/>
</appSettings>
```

3. Compile the program (Visual Studio will make this easy and the [community edition](https://www.visualstudio.com/products/visual-studio-community-vs) is free)
4. Install FFMPEG. You can either put it into the bin folder after compiling, or add the folder that it's in to your PATH.
5. Run, and the bot should join your server!

#### Interacting with PodcastPlayerBot

Interacting with the bot is easy, just preface your command with $pod. 

The first command to try is `$pod help`. This will list all the other commands that are available. The `help` command can also be used to get furthur information on the other commands. For example, `$pod help play url` would provide help information on the `play url` command.

To get the bot to play an episode of your favorite podcast, say [Alice isn't Dead](http://www.nightvalepresents.com/aliceisntdead/), use the following commands.
1. First get the rss feed url from the podcast site, http://aliceisntdead.libsyn.com/rss
1. Then use the `rss add` command. 

`$pod rss add aliceisntdead http://aliceisntdead.libsyn.com/rss`

3. Move into a voice channel if you aren't in one already.
3. Then use the `play episode` command.

`$pod rss play episode aliceisntdead 1`

![Podcast Description](PodcastDescription.PNG?raw=true)

Once an episode is playing you can use `$pod what` to have the bot give your more information on what it is currently playing.

To stop the bot from playing a pod cast use `$pod stop`. To get the bot to leave the voice channel use `$pod leave`.
