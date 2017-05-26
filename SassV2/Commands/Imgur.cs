using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class ImgurCommand : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public ImgurCommand(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(name: "imgur", desc: "get a random image from an imgur subreddit", usage: "imgur <subreddit>", category: "Spam")]
		[Command("imgur")]
		public async Task Imgur(string subreddit)
		{
			using (var client = new HttpClient())
			{
				client.BaseAddress = new Uri("https://api.imgur.com");
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _bot.Config.ImgurAccessToken);
				var result = client.GetAsync("/3/gallery/r/" + subreddit).Result;
				var data = JObject.Parse(await result.Content.ReadAsStringAsync());
				var images = data["data"] as JArray;
				if (images.Count == 0)
				{
					throw new CommandException(Util.Locale("imgur.notfound"));
				}

				var image = images[new Random().Next(0, images.Count)]["link"].Value<string>();
				await ReplyAsync(image);
			}
		}
	}
}
