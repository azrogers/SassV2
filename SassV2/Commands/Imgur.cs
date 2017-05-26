using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace SassV2.Commands
{
	public class ImgurCommand
	{
		[Command(name: "imgur", desc: "get a random image from an imgur subreddit", usage: "imgur <subreddit>", category: "Spam")]
		public async static Task<string> Imgur(DiscordBot bot, IMessage msg, string args)
		{
			if(string.IsNullOrWhiteSpace(args))
			{
				throw new CommandException(Util.Locale("imgur.needsubreddit"));
			}

			using (var client = new HttpClient())
			{
				client.BaseAddress = new Uri("https://api.imgur.com");
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bot.Config.ImgurAccessToken);
				var result = client.GetAsync("/3/gallery/r/" + args).Result;
				var data = JObject.Parse(await result.Content.ReadAsStringAsync());
				var images = data["data"] as JArray;
				if (images.Count == 0)
				{
					throw new CommandException(Util.Locale("imgur.notfound"));
				}
				return images[new Random().Next(0, images.Count)]["link"].Value<string>();
			}
		}
	}
}
