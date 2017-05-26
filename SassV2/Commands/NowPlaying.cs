using Discord.Commands;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace SassV2.Commands
{
	public class NowPlayingCommand : ModuleBase<SocketCommandContext>
	{
		[SassCommand(name: "now playing", desc: "print the currently playing song on remi radio", usage: "now playing")]
		[Command("now playing")]
		public async Task NowPlaying()
		{
			var xml = await Util.GetURLAsync("http://radio.anime.lgbt:8000/mpd.ogg.xspf");
			var reader = XmlReader.Create(new StringReader(xml));
			reader.ReadToFollowing("track");
			reader.ReadToFollowing("creator");
			reader.MoveToContent();
			if(reader.NodeType == XmlNodeType.None)
			{
				await ReplyAsync("nothing");
				return;
			}
			var creator = reader.ReadElementContentAsString();
			reader.ReadToFollowing("title");
			reader.MoveToContent();
			var title = reader.ReadElementContentAsString();
			await ReplyAsync(creator + " - " + title);
		}
	}
}
