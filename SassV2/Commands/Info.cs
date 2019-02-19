using Discord.Commands;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class InfoCommand : ModuleBase<SocketCommandContext>
	{
		private const int MAX_CHARS = 2000;
		private Regex _titleRegex = new Regex("<a.+?>(.+?)</a>");
		private Regex _htmlRegex = new Regex("<.*?>", RegexOptions.Compiled);
		private ILogger _logger = LogManager.GetCurrentClassLogger();

		[SassCommand(
			name: "info",
			desc: "Get some info on something.",
			usage: "info <something>",
			example: "info National Treasure Film",
			category: "Useful")]
		[Command("info")]
		public async Task Info([Remainder] string args)
		{
			var result = await Util.GetURLAsync("http://api.duckduckgo.com/?q=" + Uri.EscapeUriString(args) + "&format=json");
			var data = JObject.Parse(result);

			if(data["Heading"].Value<string>() == "")
			{
				await ReplyAsync("Not found.");
				return;
			}

			var body = data["AbstractText"].Value<string>();
			body = body.Replace("<pre><code>", "```").Replace("</code></pre>", "```\n");
			var type = data["Type"].Value<string>();
			if(type == "D")
			{
				body = "Did you mean:";
			}

			var infoboxInfo = "";
			if(data["Infobox"].HasValues)
			{
				var items = data["Infobox"]["content"] as JArray;
				foreach(var item in items)
				{
					var dataType = item["data_type"].Value<string>();
					switch(dataType)
					{
						case "string":
							infoboxInfo += "**" + item["label"].Value<string>() + ":** " + item["value"].Value<string>() + "\n";
							break;
						case "imdb_id":
							infoboxInfo += $"**IMDb:** <https://www.imdb.com/title/{item["value"].Value<string>()}/>\n";
							break;
						case "rotten_tomatoes":
							infoboxInfo += $"**Rotten Tomatoes:** <https://www.rottentomatoes.com/{item["value"].Value<string>()}/>\n";
							break;
						case "netflix_id":
							infoboxInfo += $"**Netflix:** <https://www.netflix.com/title/{item["value"].Value<string>()}>\n";
							break;
						case "facebook_profile":
							infoboxInfo += $"**Facebook:** <https://facebook.com/{item["value"].Value<string>()}>\n";
							break;
						case "twitter_profile":
							infoboxInfo += $"**Twitter:** <https://twitter.com/{item["value"].Value<string>()}>\n";
							break;
						case "instance":
							// no idea how we're supposed to handle this one
							break;
						default:
							// unknown data type
							_logger.Info("Unknown DuckDuckGo data type: " + dataType + ", query: " + args);
							break;
					}
				}
				infoboxInfo = infoboxInfo.Trim();
			}
			else if(data["RelatedTopics"].HasValues)
			{
				var topics = data["RelatedTopics"];
				var count = 0;
				foreach(var topic in topics)
				{
					if(topic["Topics"] != null)
					{
						continue;
					}

					var topicResult = topic["Result"].Value<string>();
					var titleMatch = _titleRegex.Match(topicResult);
					var title = titleMatch.Groups[1].Value;
					var rest = _htmlRegex.Replace(topicResult.Substring(titleMatch.Length).Trim(), string.Empty);

					infoboxInfo += $"**{title}:** {rest}\n";
					count++;
					if(count > 10 || infoboxInfo.Length > 1500)
					{
						break;
					}
				}
				infoboxInfo = infoboxInfo.Trim();
			}

			var wikipedia = $"\n\n*Read more: <{data["AbstractURL"].Value<string>()}>*";

			var response = $@"
**{data["Heading"].Value<string>()}**

{body}".Trim() + $@"

{infoboxInfo}".TrimEnd();

			// limit body to max number of lines that fit + read more
			response = Util.SmartMaxLength(response, MAX_CHARS - wikipedia.Length) + wikipedia;

			await ReplyAsync(response);
		}
	}
}
