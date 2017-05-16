using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace SassV2.Commands
{
	public class Define
	{
		private static Regex _disambRegex = new Regex(".+? may refer to:");

		[Command(name: "define", desc: "look that shit up.", usage: "define <thing>", category: "Useful")]
		public static async Task<string> WikipediaDefine(DiscordBot bot, IMessage msg, string args)
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
					throw new CommandException(Util.Locale("define.nothing"));
				}
				var parts = page["extract"].Value<string>().Split('\n');
				var str = parts.First();
				if(str.StartsWith("For the", StringComparison.CurrentCulture))
				{
					str = parts.Skip(1).First();
				}
				if(_disambRegex.IsMatch(str))
				{
					return await DefineDisambg(args);
				}
				return str;
			}
			return Util.Locale("define.nothing");
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
				throw new CommandException(Util.Locale("define.nothing"));
			}

			if(names.Count > 20)
			{
				throw new CommandException(Util.Locale("define.ambiguous"));
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
