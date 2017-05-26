using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class InfoCommand : ModuleBase<SocketCommandContext>
	{
		private Regex _titleRegex = new Regex("<a.+?>(.+?)</a>");
		private Regex _htmlRegex = new Regex("<.*?>", RegexOptions.Compiled);

		[SassCommand(name: "info", desc: "get some info on something", usage: "info", category: "Useful")]
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

			var response = $@"
**{data["Heading"].Value<string>()}**

{body}".Trim() + $@"

{infoboxInfo}".TrimEnd() + $@"

*Read more: {data["AbstractURL"].Value<string>()}*";

			await ReplyAsync(response);
		}
	}
}
