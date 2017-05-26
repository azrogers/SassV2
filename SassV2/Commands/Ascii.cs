using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using FIGlet.Net;
using System.IO;
using Discord.Commands;

namespace SassV2.Commands
{
	public class Ascii : ModuleBase<SocketCommandContext>
	{
		private Dictionary<string, string> _fonts = new Dictionary<string, string>();
		private DiscordBot _bot;

		public Ascii(DiscordBot bot)
		{
			_bot = bot;
			foreach(string file in Directory.EnumerateFiles("Fonts"))
			{
				_fonts[Path.GetFileNameWithoutExtension(file.ToLower().Replace(' ', '_'))] = file;
			}
		}

		[SassCommand(name: "ascii", desc: "generate some sweet ascii text, yo", usage: "ascii <font> <text>", category: "Spam")]
		[Command("ascii")]
		public async Task AsciiText(string font, [Remainder] string text)
		{
			if(!_fonts.ContainsKey(font))
			{
				await ReplyAsync(Util.Locale("ascii.badFont"));
				return;
			}

			var fig = new Figlet();
			fig.LoadFont(_fonts[font]);
			await ReplyAsync("```\n" + fig.ToAsciiArt(text).TrimEnd() + "\n```");
		}

		[SassCommand(name: "ascii fonts", desc: "list some ascii fonts yo.", usage: "ascii fonts", category: "Spam")]
		[Command("ascii fonts")]
		public async Task AsciiFonts()
		{
			await ReplyAsync(_bot.Config.URL + "fonts");
		}
	}
}
