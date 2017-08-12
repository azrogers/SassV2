using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace SassV2.Commands
{
	public class Base64 : ModuleBase<SocketCommandContext>
	{
		[SassCommand(
			name: "base64 encode", 
			desc: "Encode something in <a href='https://en.wikipedia.org/wiki/Base64'>base64</a>.", 
			usage: "base64 encode <thing>", 
			example: "base64 encode Example.",
			category: "Spam")]
		[Command("base64 encode")]
		public async Task Base64Encode([Remainder] string args)
		{
			await ReplyAsync("Encoded result: " + Convert.ToBase64String(Encoding.UTF8.GetBytes(args)));
		}

		[SassCommand(
			name: "base64 decode", 
			desc: "Decode something from <a href='https://en.wikipedia.org/wiki/Base64'>base64</a>.", 
			usage: "base64 decode <thing>", 
			example: "base64 decode RXhhbXBsZS4=",
			category: "Spam")]
		[Command("base64 decode")]
		public async Task Base64Decode([Remainder] string args)
		{
			await ReplyAsync("Decoded result: " + Encoding.UTF8.GetString(Convert.FromBase64String(args)));
		}
	}
}
