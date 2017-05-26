using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace SassV2.Commands
{
	public class AnimalCommands : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public AnimalCommands(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(names: new string[] { "kitty", "cat", "cats" }, desc: "get a cat", usage: "kitty", category: "Spam")]
		[Command("kitty", RunMode = RunMode.Async)]
		[Alias("cat", "cats")]
		public async Task Kitty()
		{
			var data = JArray.Parse(await Util.GetURLAsync("http://shibe.online/api/cats"));
			var image = data.First.Value<string>();
			await ReplyAsync(image);
		}

		[SassCommand(names: new string[] { "shibe", "shiba" }, desc: "get a shiba inu", usage: "shibe", category: "Spam")]
		[Command("shibe", RunMode = RunMode.Async)]
		[Alias("shiba")]
		public async Task Shibe()
		{
			var data = JArray.Parse(await Util.GetURLAsync("http://shibe.online/api/shibes"));
			var image = data.First.Value<string>();
			await ReplyAsync(image);
		}

		[SassCommand(names: new string[] { "bird", "birb" }, desc: "get a bird", usage: "bird", category: "Spam")]
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
