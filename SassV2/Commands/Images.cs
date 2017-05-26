using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Images : ModuleBase<SocketCommandContext>
	{
		private Regex _forRegex = new Regex(@"(for\s|)(\s+)?(.+?)$");
		private Regex _urlRegex = new Regex(@"(i\.)?imgur\.com\/(.+?)\.(jpg|png|gif|jpeg)", RegexOptions.IgnoreCase);
		private DiscordBot _bot;

		public Images(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(name: "images", desc: "list images.", usage: "images")]
		[Command("images")]
		[RequireContext(ContextType.Guild)]
		public async Task ListImages()
		{
			await ReplyAsync(_bot.Config.URL + "images/" + Context.Guild.Id);
		}

		[SassCommand(name: "image", desc: "gets the image for the thing you asked for.", usage: "image <thing>")]
		[Command("image")]
		[RequireContext(ContextType.Guild)]
		public async Task Image([Remainder] string args)
		{
			var match = _forRegex.Match(args.Trim().ToLower());
			if(!match.Success || match.Groups.Count < 4)
			{
				await ReplyAsync(Util.Locale("images.nonsense"));
				return;
			}

			var thing = match.Groups[3].Value.ToLower();
			string image = _bot.Database(Context.Guild.Id).GetObject<string>("image:" + thing);
			if(image == null)
			{
				await ReplyAsync(Util.Locale("images.noimage", new { thing = thing }));
				return;
			}

			await ReplyAsync($"Image for {thing}: {image}");
		}

		[SassCommand(name: "set image", desc: "sets the image for the thing you told it to set it for.", usage: "set image <thing> <imgur url>")]
		[Command("set image")]
		[RequireContext(ContextType.Guild)]
		public async Task SetImage([Remainder] string args)
		{
			var match = _forRegex.Match(args.Trim());
			if(!match.Success || match.Groups.Count < 4)
			{
				await ReplyAsync(Util.Locale("images.nonsense"));
				return;
			}
			
			var parts = match.Groups[3].Value.Split(' ');
			if(parts.Length < 2 && !Context.Message.Attachments.Any())
			{
				await ReplyAsync(Util.Locale("images.needparts"));
				return;
			}

			var url = (Context.Message.Attachments.Any() ? Context.Message.Attachments.First().Url : parts.Last());
			var urlMatch = _urlRegex.Match(url);
			if(!urlMatch.Success || urlMatch.Length < 4)
			{
				url = await UploadImage(url, _bot.Config.ImgurClientId);
				if(url == null)
				{
					await ReplyAsync(Util.Locale("images.cantmove"));
					return;
				}
				urlMatch = _urlRegex.Match(url);
			}

			var imageId = urlMatch.Groups[2].Value;
			var extension = urlMatch.Groups[3].Value;
			var thing = (Context.Message.Attachments.Any() ? 
				string.Join(" ", parts) :
				string.Join(" ", parts.Take(parts.Count() - 1))).ToLower();
			var finalUrl = "http://i.imgur.com/" + imageId + "." + extension;

			_bot.Database(Context.Guild.Id).InsertObject<string>("image:" + thing, finalUrl);

			await ReplyAsync(Util.Locale("images.set"));
		}

		private async Task<string> UploadImage(string url, string clientID)
		{
			var client = new HttpClient();
			client.BaseAddress = new Uri("https://api.imgur.com");
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Client-ID", clientID);
			var content = new FormUrlEncodedContent(new[]
			{
				new KeyValuePair<string, string>("image", url),
				new KeyValuePair<string, string>("name", "sass_is_very_good.jpg"),
				new KeyValuePair<string, string>("type", "URL")
			});
			var result = client.PostAsync("/3/image", content).Result;
			var data = JObject.Parse(await result.Content.ReadAsStringAsync());
			if(!data["success"].Value<bool>())
			{
				return null;
			}
			return data["data"]["link"].Value<string>();
		}
	}
}
