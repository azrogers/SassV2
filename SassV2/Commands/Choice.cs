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
		[SassCommand(name: "choice", desc: "gives you a random thing out of the options you give it.", usage: "choice <a whole bunch of things>", category: "Useful")]
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
