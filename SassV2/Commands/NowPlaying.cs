using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Discord;

namespace SassV2.Commands
{
	public static class NowPlayingCommand
	{
		[Command(name: "now playing", desc: "print the currently playing song on remi radio", usage: "now playing")]
		public async static Task<string> NowPlaying(DiscordBot bot, Message msg, string args)
		{
			var xml = await Util.GetURLAsync("http://radio.anime.lgbt:8000/mpd.ogg.xspf");
			var reader = XmlReader.Create(new StringReader(xml));
			reader.ReadToFollowing("track");
			reader.ReadToFollowing("creator");
			reader.MoveToContent();
			var creator = reader.ReadElementContentAsString();
			reader.ReadToFollowing("title");
			reader.MoveToContent();
			var title = reader.ReadElementContentAsString();
			return creator + " - " + title;
		}
	}
}
