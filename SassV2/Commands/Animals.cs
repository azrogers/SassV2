using Discord.Commands;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class AnimalCommands : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public AnimalCommands(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			names: new string[] { "kitty", "cat", "cats" }, 
			desc: "Responds with a random image of a cat.", 
			usage: "kitty", 
			category: "Animal")]
		[Command("kitty", RunMode = RunMode.Async)]
		[Alias("cat", "cats")]
		public async Task Kitty()
		{
			var data = JArray.Parse(await Util.GetURLAsync("http://shibe.online/api/cats"));
			var image = data.First.Value<string>();
			await ReplyAsync(image);
		}

		[SassCommand(
			names: new string[] { "shibe", "shiba" }, 
			desc: "Responds with a random image of a shiba inu.", 
			usage: "shibe", 
			category: "Animal")]
		[Command("shibe", RunMode = RunMode.Async)]
		[Alias("shiba")]
		public async Task Shibe()
		{
			var data = JArray.Parse(await Util.GetURLAsync("http://shibe.online/api/shibes"));
			var image = data.First.Value<string>();
			await ReplyAsync(image);
		}

		[SassCommand(
			names: new string[] { "bird", "birb" }, 
			desc: "Responds with a random image of a bird.", 
			usage: "bird", 
			category: "Animal")]
		[Command("bird", RunMode = RunMode.Async)]
		[Alias("birb")]
		public async Task Bird()
		{
			var data = JArray.Parse(await Util.GetURLAsync("http://shibe.online/api/birds"));
			var image = data.First.Value<string>();
			await ReplyAsync(image);
		}
	}
}
