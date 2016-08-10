using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.IO;

namespace SassV2.Commands
{
	public class PiCommand
	{
		[Command(name: "pi", desc: "gets the nth digit of pi, up to the millionth.", usage: "pi <number>", category: "Dumb")]
		public static string Pi(DiscordBot bot, Message msg, string args)
		{
			int numPosition;
			if(!int.TryParse(args, out numPosition))
			{
				throw new CommandException("I don't know what that is.");
			}
			if(numPosition < 1)
			{
				throw new CommandException("That's not how it works.");
			}
			if(numPosition > 1000000)
			{
				throw new CommandException("You and I both know you don't really need to know what that digit is.");
			}
			if(!File.Exists("pi.dat"))
			{
				throw new CommandException("I don't know how, sorry.");
			}

			using(var file = File.OpenRead("pi.dat"))
			{
				byte[] output = new byte[1];
				file.Position = numPosition - 1;
				file.Read(output, 0, 1);
				return "The " + Util.CardinalToOrdinal(numPosition) + " digit of Pi is " + Encoding.ASCII.GetString(output) + ".";
			}
		}
	}
}
