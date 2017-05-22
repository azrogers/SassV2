using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Discord;
using System.Reactive.Linq;

namespace SassV2.Commands
{
	public class Images
	{
		private static Regex _forRegex = new Regex(@"(for\s|)(\s+)?(.+?)$");
		private static Regex _urlRegex = new Regex(@"(i\.)?imgur\.com\/(.+?)\.(jpg|png|gif|jpeg)", RegexOptions.IgnoreCase);

		[Command(name: "images", desc: "list images.", usage: "images")]
		public static string ListImages(DiscordBot bot, IMessage msg, string args)
		{
			return bot.Config.URL + "images/" + msg.ServerId();
		}

		[Command(name: "image", desc: "gets the image for the thing you asked for.", usage: "image <thing>")]
		public static string Image(DiscordBot bot, IMessage msg, string args)
		{
			var match = _forRegex.Match(args.Trim().ToLower());
			if(!match.Success || match.Groups.Count < 4)
			{
				throw new CommandException(Util.Locale("images.nonsense"));
			}

			var thing = match.Groups[3].Value.ToLower();
			string image = bot.Database(msg.ServerId()).GetObject<string>("image:" + thing);
			if(image == null)
			{
				throw new CommandException(Util.Locale("images.noimage", new { thing = thing }));
			}

			return $"Image for {thing}: {image}";
		}

		[Command(name: "set image", desc: "sets the image for the thing you told it to set it for.", usage: "set image <thing> <imgur url>")]
		public async static Task<string> SetImage(DiscordBot bot, IMessage msg, string args)
		{
			var match = _forRegex.Match(args.Trim());
			if(!match.Success || match.Groups.Count < 4)
			{
				throw new CommandException(Util.Locale("images.nonsense"));
			}
			
			var parts = match.Groups[3].Value.Split(' ');
			if(parts.Length < 2 && !msg.Attachments.Any())
			{
				throw new CommandException(Util.Locale("images.needparts"));
			}

			var url = (msg.Attachments.Any() ? msg.Attachments.First().Url : parts.Last());
			var urlMatch = _urlRegex.Match(url);
			if(!urlMatch.Success || urlMatch.Length < 4)
			{
				//throw new CommandException("I suggest using Imgur.");
				url = await UploadImage(url, bot.Config.ImgurClientId);
				urlMatch = _urlRegex.Match(url);
			}

			var imageId = urlMatch.Groups[2].Value;
			var extension = urlMatch.Groups[3].Value;
			var thing = (msg.Attachments.Any() ? 
				string.Join(" ", parts) :
				string.Join(" ", parts.Take(parts.Count() - 1))).ToLower();
			var finalUrl = "http://i.imgur.com/" + imageId + "." + extension;

			bot.Database(msg.ServerId()).InsertObject<string>("image:" + thing, finalUrl);

			return Util.Locale("images.set");
		}

		private async static Task<string> UploadImage(string url, string clientID)
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
				throw new CommandException(Util.Locale("images.cantmove"));
			}
			return data["data"]["link"].Value<string>();
		}
	}
}
