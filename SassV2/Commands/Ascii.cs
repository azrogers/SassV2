using Discord.Commands;
using FIGlet.Net;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Ascii : ModuleBase<SocketCommandContext>
	{
		private Dictionary<string, string> _fonts = new Dictionary<string, string>();
		private DiscordBot _bot;

		public Ascii(DiscordBot bot)
		{
			_bot = bot;
			foreach (string file in Directory.EnumerateFiles("Fonts"))
			{
				_fonts[Path.GetFileNameWithoutExtension(file.ToLower().Replace(' ', '_'))] = file;
			}
		}

		[SassCommand(
			name: "ascii", 
			desc: "Uses <a href='http://www.figlet.org/'>FIGlet</a> to generate cool ASCII text.", 
			usage: "ascii <font> <text>", 
			example: "ascii roman This is a test.",
			category: "Spam")]
		[Command("ascii", RunMode = RunMode.Sync)]
		public void AsciiText(string font, [Remainder] string text)
		{
			if(!_fonts.ContainsKey(font))
			{
				
				ReplyAsync(Util.Locale("ascii.badFont"));
				return;
			}

			var fig = new Figlet();
			fig.LoadFont(_fonts[font]);
			ReplyAsync("```\n" + fig.ToAsciiArt(text).TrimEnd() + "\n```");
		}

		[SassCommand(
			name: "ascii fonts", 
			desc: "Responds with a list of available ASCII fonts.", 
			usage: "ascii fonts", 
			category: "Spam")]
		[Command("ascii fonts")]
		public async Task AsciiFonts()
		{
			await ReplyAsync(_bot.Config.URL + "fonts");
		}
	}
}
