using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Discord;

namespace SassV2.Commands
{
	public class StackCommand
	{
		[Command(name: "stack", desc: "add something to the stack of requests or print it out.", usage: "stack\nstack <thing>")]
		public static string Stack(DiscordBot bot, Message msg, string args)
		{
			if(args.Trim().Length == 0)
			{
				if(!File.Exists("requests.txt"))
				{
					throw new CommandException("There is nothing on the stack.");
				}

				return "**Requests Stack**\n" + string.Join("\n", File.ReadAllLines("requests.txt"));
			}
			else
			{
				File.AppendAllText("requests.txt", args + "\n");
				return "It's on the stack now.";
			}
		}
	}
}
