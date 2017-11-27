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
	/// <summary>
	/// Image definitions.
	/// </summary>
	public class Images : ModuleBase<SocketCommandContext>
	{
		private Regex _forRegex = new Regex(@"(for\s|)(\s+)?(.+?)$");
		private Regex _urlRegex = new Regex(@"(i\.)?imgur\.com\/(.+?)\.(jpg|png|gif|jpeg)", RegexOptions.IgnoreCase);
		private DiscordBot _bot;

		public Images(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "images",
			desc: "List all images on this server.",
			usage: "images",
			category: "Image")]
		[Command("images")]
		[RequireContext(ContextType.Guild)]
		public async Task ListImages()
		{
			await ReplyAsync(_bot.Config.URL + "images/" + Context.Guild.Id);
		}

		[SassCommand(
			name: "image",
			desc: "Gets the image for the thing you asked for.",
			usage: "image <thing>",
			example: "image pizza",
			category: "Image")]
		[Command("image")]
		[RequireContext(ContextType.Guild)]
		public async Task Image([Remainder] string args)
		{
			var match = _forRegex.Match(args.Trim().ToLower());
			if(!match.Success || match.Groups.Count < 4)
			{
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "images.nonsense"));
				return;
			}

			var thing = match.Groups[3].Value.ToLower();
			string image = _bot.Database(Context.Guild.Id).GetObject<string>("image:" + thing);
			if(image == null)
			{
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "images.noimage", new { thing = thing }));
				return;
			}

			await ReplyAsync($"Image for {thing}: {image}");
		}

		[SassCommand(
			name: "set image",
			desc: "Sets the image for the thing you told it to set it for.",
			example: "set image pizza http://i.imgur.com/b9zDbyb.jpg",
			usage: "set image <thing> <imgur url>",
			category: "Image")]
		[Command("set image")]
		[RequireContext(ContextType.Guild)]
		public async Task SetImage([Remainder] string args)
		{
			var match = _forRegex.Match(args.Trim());
			if(!match.Success || match.Groups.Count < 4)
			{
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "images.nonsense"));
				return;
			}

			var parts = match.Groups[3].Value.Split(' ');
			if(parts.Length < 2 && !Context.Message.Attachments.Any())
			{
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "images.needparts"));
				return;
			}

			var url = (Context.Message.Attachments.Any() ? Context.Message.Attachments.First().Url : parts.Last());
			var urlMatch = _urlRegex.Match(url);
			if(!urlMatch.Success || urlMatch.Length < 4)
			{
				url = await UploadImage(url, _bot.Config.ImgurClientId);
				if(url == null)
				{
					await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "images.cantmove"));
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

			await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "images.set"));
		}

		// upload to imgur if they weren't smart enough to do it themselves
		private async Task<string> UploadImage(string url, string clientID)
		{
			var client = new HttpClient()
			{
				BaseAddress = new Uri("https://api.imgur.com")
			};
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
