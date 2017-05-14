using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using FIGlet.Net;
using System.IO;

namespace SassV2.Commands
{
	public static class Ascii
	{
		private static Dictionary<string, string> _fonts = new Dictionary<string, string>();

		static Ascii()
		{
			foreach(string file in Directory.EnumerateFiles("Fonts"))
			{
				_fonts[Path.GetFileNameWithoutExtension(file.ToLower().Replace(' ', '_'))] = file;
			}
		}

		[Command(name: "ascii", desc: "generate some sweet ascii text, yo", usage: "ascii <font> <text>", category: "Spam")]
		public static string AsciiText(DiscordBot bot, IMessage msg, string args)
		{
			if(string.IsNullOrWhiteSpace(args))
			{
				throw new CommandException(Util.Locale("ascii.noFont"));
			}
			var parts = args.Trim().Split(' ');
			if(parts.Length == 1)
			{
				throw new CommandException(Util.Locale("ascii.noText"));
			}
			// check if the font exists
			if(!_fonts.ContainsKey(parts[0]))
			{
				throw new CommandException(Util.Locale("ascii.badFont"));
			}

			var fig = new Figlet();
			fig.LoadFont(_fonts[parts[0]]);
			var text = string.Join(" ", parts.Skip(1).ToArray());
			return "```\n" + fig.ToAsciiArt(text).TrimEnd() + "\n```";
		}

		[Command(name: "ascii fonts", desc: "list some ascii fonts yo.", usage: "ascii fonts", category: "Spam")]
		public static string AsciiFonts(DiscordBot bot, IMessage msg, string args)
		{
			return bot.Config.URL + "fonts";
		}
	}
}
