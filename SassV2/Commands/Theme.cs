using Discord.Commands;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class ThemeCommand : ModuleBase<SocketCommandContext>
	{
		private Regex _youtubeRegex = new Regex(@"(youtu\.be\/|youtube\.com\/(watch\?(.*&)?v=|(embed|v)\/))([^\?&""'>]+)");
		private DiscordBot _bot;

		public ThemeCommand(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "theme", 
			desc: "Gets the theme for the current chat room.", 
			usage: "theme", 
			category: "Dumb")]
		[Command("theme")]
		public async Task Theme()
		{
			string theme = _bot.Database(Context.Guild.Id).GetObject<string>("theme:" + Context.Channel.Id);
			if(theme == null)
			{
				await ReplyAsync("There's no theme for " + Context.Channel.Name + " (yet).");
				return;
			}
			
			await ReplyAsync("Theme for #" + Context.Channel.Name + ": " + theme);
		}

		[SassCommand(
			name: "set theme", 
			desc: "Sets the theme for the current chat room.", 
			usage: "set theme <youtube URL>", 
			example: "set theme https://www.youtube.com/watch?v=hkI3RymKUJo",
			category: "Dumb")]
		[Command("set theme")]
		public async Task SetTheme(string url)
		{
			var urlMatch = _youtubeRegex.Match(url);
			if(!urlMatch.Success || urlMatch.Groups.Count < 6)
			{
				await ReplyAsync("I suggest using YouTube.");
				return;
			}

			var videoId = urlMatch.Groups[5].Value;
			var finalUrl = "https://youtu.be/" + videoId;
			_bot.Database(Context.Guild.Id).InsertObject<string>("theme:" + Context.Channel.Id, finalUrl);
			await ReplyAsync("Theme set.");
		}
	}
}
