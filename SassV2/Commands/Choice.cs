using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace SassV2.Commands
{
	public class Choice : ModuleBase<SocketCommandContext>
	{
		[SassCommand(
			name: "choice", 
			desc: "Chooses between several space-delineated options. Use quotes for multi-word options.", 
			usage: "choice <a whole bunch of things>", 
			example: "choice a \"thing b\" c d",
			category: "Useful")]
		[Command("choice")]
		public async void Choices([Remainder] string args)
		{
			if(args.Trim() == "meme")
			{
				await ReplyAsync(Util.Locale("choice.memes"));
			}

			var parts = Util.SplitQuotedString(args);
			var random = new Random();
			await ReplyAsync("I choose: " + parts[random.Next(parts.Length)]);
		}
	}
}
