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
		[SassCommand(name: "base64 encode", desc: "encode something in base64", usage: "base64 encode <thing>", category: "Spam")]
		[Command("base64 encode")]
		public async Task Base64Encode([Remainder] string args)
		{
			await ReplyAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(args)));
		}

		[SassCommand(name: "base64 decode", desc: "decode something in base64", usage: "base64 decode <thing>", category: "Spam")]
		[Command("base64 decode")]
		public async Task Base64Decode([Remainder] string args)
		{
			await ReplyAsync(Encoding.UTF8.GetString(Convert.FromBase64String(args)));
		}
	}
}
