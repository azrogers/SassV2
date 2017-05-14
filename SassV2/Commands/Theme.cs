using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using System.Reactive.Linq;

namespace SassV2.Commands
{
	public class ThemeCommand
	{
		private static Regex _youtubeRegex = new Regex(@"(youtu\.be\/|youtube\.com\/(watch\?(.*&)?v=|(embed|v)\/))([^\?&""'>]+)");
		private static Regex _setThemeRegex = new Regex(@"(for\s|)(\s+)?(.+?)$");

		[Command(name: "theme", desc: "gets the theme for the current chat room.", usage: "theme", category: "Dumb")]
		public static string Theme(DiscordBot bot, IMessage msg, string args)
		{
			string theme = bot.Database(msg.ServerId()).GetObject<string>("theme:" + msg.Channel.Id);
			if(theme == null)
			{
				throw new CommandException("There's no theme for " + msg.Channel.Name + " (yet).");
			}
			
			return "Theme for #" + msg.Channel.Name + ": " + theme;
		}

		[Command(name: "set theme", desc: "sets the theme for the current chat room.", usage: "set theme <youtube URL>", category: "Dumb")]
		public static string SetTheme(DiscordBot bot, IMessage msg, string args)
		{
			var match = _setThemeRegex.Match(args.Trim());
			if(!match.Success || match.Groups.Count < 4)
			{
				throw new CommandException("That doesn't make any sense.");
			}

			var parts = match.Groups[3].Value.Split(' ');
			if(parts.Length < 1)
			{
				throw new CommandException("You need to provide a URL.");
			}

			var url = parts.Last();
			var urlMatch = _youtubeRegex.Match(url);
			if(!urlMatch.Success || urlMatch.Groups.Count < 6)
			{
				throw new CommandException("I suggest using YouTube.");
			}

			var videoId = urlMatch.Groups[5].Value;
			var finalUrl = "https://youtu.be/" + videoId;
			bot.Database(msg.ServerId()).InsertObject<string>("theme:" + msg.Channel.Id, finalUrl);
			return "Theme set.";
		}
	}
}
