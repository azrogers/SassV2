using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace SassV2.Commands
{
	public static class Base64
	{
		[Command(name: "base64 encode", desc: "encode something in base64", usage: "base64 encode <thing>", category: "Spam")]
		public static string Base64Encode(DiscordBot bot, IMessage msg, string args)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(args));
		}

		[Command(name: "base64 decode", desc: "decode something in base64", usage: "base64 decode <thing>", category: "Spam")]
		public static string Base64Decode(DiscordBot bot, IMessage msg, string args)
		{
			return Encoding.UTF8.GetString(Convert.FromBase64String(args));
		}
	}
}
