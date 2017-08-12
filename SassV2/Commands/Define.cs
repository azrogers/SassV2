using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using Discord.Commands;

namespace SassV2.Commands
{
	public class Define : ModuleBase<SocketCommandContext>
	{
		private Regex _disambRegex = new Regex(".+? may refer to:");

		[SassCommand(
			name: "define", 
			desc: "Look something up on Wikipedia.", 
			usage: "define <thing>", 
			example: "define computers",
			category: "Useful")]
		[Command("define")]
		public async Task WikipediaDefine([Remainder] string args)
		{
			var url = "https://en.wikipedia.org/w/api.php?format=json&redirects&action=query&prop=extracts&exintro=&explaintext=&titles=";
			url += Uri.EscapeUriString(args.Trim());
			var data = await Util.GetURL(url);
			var info = JObject.Parse(data);
			foreach(var pageKey in (info["query"]["pages"] as JObject).Properties().Select(p => p.Name))
			{
				var page = info["query"]["pages"][pageKey];
				if(page["missing"] != null)
				{
					await ReplyAsync(Util.Locale("define.nothing"));
					return;
				}
				var parts = page["extract"].Value<string>().Split('\n');
				var str = parts.First();
				if(str.StartsWith("For the", StringComparison.CurrentCulture))
				{
					str = parts.Skip(1).First();
				}

				if(_disambRegex.IsMatch(str))
				{
					await ReplyAsync(await DefineDisambg(args));
				}
				else
				{
					await ReplyAsync(str);
				}

				return;
			}

			await ReplyAsync(Util.Locale("define.nothing"));
		}

		private static async Task<string> DefineDisambg(string args)
		{
			var url = "https://en.wikipedia.org/w/api.php?format=json&action=query&prop=revisions&rvprop=content&titles=";
			url += Uri.EscapeUriString(args.Trim());
			var data = await Util.GetURL(url);
			var info = JObject.Parse(data);
			var page = info["query"]["pages"].First().First;
			var content = page["revisions"].First()["*"].Value<string>();
			var regex = new Regex(@"\* \[\[(.+?)\]\]");
			var names = new List<string>();
			var matchCollection = regex.Matches(content);
			for(var i = 0; i < matchCollection.Count; i++)
			{
				var match = matchCollection[i];
				names.Add(match.Groups[1].Value);
			}

			if(!names.Any())
			{
				return Util.Locale("define.nothing");
			}

			if(names.Count > 20)
			{
				return Util.Locale("define.ambiguous");
			}

			string str = "";
			for(var i = 0; i < names.Count - 1; i++)
			{
				str += names[i] + ", ";
			}
			str += "or " + names.Last();
			return Util.Locale("define.dym", new { options = str });
		}
	}
}
