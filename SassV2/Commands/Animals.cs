using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json.Linq;

namespace SassV2.Commands
{
	public class AnimalCommands
	{
		[Command(names: new string[] { "kitty", "cat", "cats" }, desc: "get a cat", usage: "kitty", category: "Spam")]
		public static string Kitty(DiscordBot bot, Message msg, string args)
		{
			var data = JArray.Parse(Util.GetURL("http://shibe.online/api/cats"));
			var image = data.First.Value<string>();
			return image;
		}

		[Command(names: new string[] { "shibe", "shiba", "shiba inu" }, desc: "get a shiba inu", usage: "shibe", category: "Spam")]
		public static string Shibe(DiscordBot bot, Message msg, string args)
		{
			var data = JArray.Parse(Util.GetURL("http://shibe.online/api/shibes"));
			var image = data.First.Value<string>();
			return image;
		}

		[Command(names: new string[] { "bird", "birb" }, desc: "get a bird", usage: "bird", category: "Spam")]
		public static string Bird(DiscordBot bot, Message msg, string args)
		{
			var data = JArray.Parse(Util.GetURL("http://shibe.online/api/birds"));
			var image = data.First.Value<string>();
			return image;
		}
	}
}
