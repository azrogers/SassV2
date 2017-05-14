using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace SassV2.Commands
{
	public class Choice
	{
		[Command(name: "choice", desc: "gives you a random thing out of the options you give it.", usage: "choice <a whole bunch of things>", category: "Useful")]
		public static string Choices(DiscordBot bot, IMessage msg, string args)
		{
			if(args.Trim() == "meme")
			{
				throw new CommandException(Util.Locale("choice.memes"));
			}

			var parts = args.Trim().Split(' ');
			var inString = false;
			var currentString = "";
			var finalParts = new List<string>();

			// this is bad. todo: make it good
			foreach(var part in parts)
			{
				if(part[0] == '"')
				{
					inString = true;
					currentString = part.Substring(1);
					if(part.EndsWith("\"", StringComparison.CurrentCulture))
					{
						finalParts.Add(part.Trim('"'));
						inString = false;
						currentString = "";
					}
				}
				else if(inString && part.EndsWith("\"", StringComparison.CurrentCulture))
				{
					inString = false;
					currentString += " " + part.Substring(0, part.Length - 1);
					finalParts.Add(currentString);
					currentString = "";
				}
				else if(inString)
				{
					currentString += " " + part;
				}
				else
				{
					finalParts.Add(part);
				}
			}

			if(inString)
			{
				finalParts.Add(currentString);
			}

			var random = new Random();
			return finalParts[random.Next(finalParts.Count)];
		}
	}
}
