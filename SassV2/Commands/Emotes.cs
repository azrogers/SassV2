using Discord.Commands;
using System.IO;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Emotes : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public Emotes(DiscordBot bot) => _bot = bot;

		[Command("emote")]
		[SassCommand(
			name: "emote",
			desc: "returns an SA emote with an optional size",
			usage: "emotes <name> [size between 1 and 2000]",
			example: "emotes ironicat",
			category: "Spam"
		)]
		public async Task GetEmote(string name, int size = 200)
		{
			if(size < 1 || size > 2000)
			{
				await ReplyAsync("Image size must be between 1 and 2000.");
				return;
			}

			if(!EmoteManager.HasEmote(name))
			{
				await ReplyAsync($"Emote '{name}' doesn't exist.");
				return;
			}

			var data = EmoteManager.GetEmote(name, size);
			using(var stream = new MemoryStream(data.Item2))
			{
				await Context.Channel.SendFileAsync(stream, data.Item1, name + ":");
			}
		}
	}
}
