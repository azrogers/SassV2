using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using NCalc;

namespace SassV2.Commands
{
	public class MathCommand
	{
		[Command(name: "math", desc: "calc.exe, but in the cloud!", usage: "math <some math stuff>", category: "Useful")]
		public static string Math(DiscordBot bot, Message msg, string args)
		{
			try
			{
				return args.Trim() + " = " + new Expression(args).Evaluate().ToString();
			}
			catch(ArgumentException ex)
			{
				throw new CommandException(ex.Message);
			}
		}
	}
}
