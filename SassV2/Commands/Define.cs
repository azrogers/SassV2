using Discord.Commands;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	/// <summary>
	/// Get Wikipedia definition for term.
	/// </summary>
	public class Define : ModuleBase<SocketCommandContext>
	{
		private Regex _disambRegex = new Regex(".+? may refer to:");
		private DiscordBot _bot;

		public Define(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "define",
			desc: "Look something up on Wikipedia.",
			usage: "define <thing>",
			example: "define computers",
			category: "Useful")]
		[Command("define")]
		public async Task WikipediaDefine([Remainder] string args)
		{
			// just look it up, straight
			var url = "https://en.wikipedia.org/w/api.php?format=json&redirects&action=query&prop=extracts&exintro=&explaintext=&titles=";
			url += Uri.EscapeUriString(args.Trim());
			var data = await Util.GetURLAsync(url);
			var info = JObject.Parse(data);

			foreach(var pageKey in (info["query"]["pages"] as JObject).Properties().Select(p => p.Name))
			{
				var page = info["query"]["pages"][pageKey];
				if(page["missing"] != null)
				{
					await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "define.nothing"));
					return;
				}

				var parts = page["extract"].Value<string>().Split('\n');
				var str = parts.First();
				if(str.StartsWith("For the", StringComparison.CurrentCulture))
				{
					str = parts.Skip(1).First();
				}

				// does this look like a disambiguation? prompt the user
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

			await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "define.nothing"));
		}

		private async Task<string> DefineDisambg(string args)
		{
			// look up the disambiguation page
			var url = "https://en.wikipedia.org/w/api.php?format=json&action=query&prop=revisions&rvprop=content&titles=";
			url += Uri.EscapeUriString(args.Trim());
			var data = await Util.GetURLAsync(url);
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
				return Locale.GetString(_bot.Language(Context.Guild?.Id), "define.nothing");
			}

			// too many to disambiguate
			if(names.Count > 20)
			{
				return Locale.GetString(_bot.Language(Context.Guild?.Id), "define.ambiguous");
			}

			// string concat
			string str = "";
			for(var i = 0; i < names.Count - 1; i++)
			{
				str += names[i] + ", ";
			}
			str += "or " + names.Last();
			return Locale.GetString(_bot.Language(Context.Guild?.Id), "define.dym", new { options = str });
		}
	}
}
