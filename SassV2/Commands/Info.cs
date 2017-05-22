using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json.Linq;

namespace SassV2.Commands
{
	public class InfoCommand
	{
		private static Regex _titleRegex = new Regex("<a.+?>(.+?)</a>");
		private static Regex _htmlRegex = new Regex("<.*?>", RegexOptions.Compiled);

		[Command(name: "info", desc: "get some info on something", usage: "info", category: "Useful")]
		public async static Task<string> Info(DiscordBot bot, IMessage msg, string args)
		{
			if(string.IsNullOrWhiteSpace(args))
			{
				throw new CommandException("provide something to get info on, yo");
			}

			var result = await Util.GetURLAsync("http://api.duckduckgo.com/?q=" + Uri.EscapeUriString(args) + "&format=json");
			var data = JObject.Parse(result);

			if(data["Heading"].Value<string>() == "")
			{
				return "Not found.";
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
					infoboxInfo += "**" + item["label"].Value<string>() + "**: " + item["value"].Value<string>() + "\n";
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

					infoboxInfo += $"**{title}**: {rest}\n";
					count++;
					if(count > 10 || infoboxInfo.Length > 1500) break;
				}
				infoboxInfo = infoboxInfo.Trim();
			}

			return $@"
**{data["Heading"].Value<string>()}**

{body}".Trim() + $@"

{infoboxInfo}".TrimEnd() + $@"

*Read more: {data["AbstractURL"].Value<string>()}*";
		}
	}
}
