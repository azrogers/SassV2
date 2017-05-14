using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace SassV2.Commands
{
	public class LispCommand
	{
		[Command(name: "lisp", desc: "run a lisp program", usage: "lisp <program>", hidden: true)]
		public static string Lisp(DiscordBot bot, Message msg, string args)
		{
			return SassLisp.Run(msg, args).ToString();
		}
	}
}
