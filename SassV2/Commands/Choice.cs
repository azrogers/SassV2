using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	/// <summary>
	/// Chooses a value from a given set.
	/// </summary>
	public class Choice : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public Choice(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "choice",
			desc: "Chooses between several space-delineated options. Use quotes for multi-word options.",
			usage: "choice <a whole bunch of things>",
			example: "choice a \"thing b\" c d",
			category: "Useful")]
		[Command("choice")]
		public async Task Choices([Remainder] string args)
		{
			if(args.ToLower().Trim() == "meme" || args.ToLower().Trim() == "memes")
			{
				await ReplyAsync(Util.Locale(_bot.Language(Context.Guild?.Id), "choice.memes"));
			}

			var parts = Util.SplitQuotedString(args);
			var random = new Random();
			await ReplyAsync("I choose: " + parts[random.Next(parts.Length)]);
		}
	}
}
