using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace SassV2.Commands
{
	public static class Dumb
	{
		[Command(name: "42nd digit of pi", hidden: true)]
		public static string ConroPi(DiscordBot bot, Message msg, string args)
		{
			return "According to Yahoo Answers user Vampz, the 42nd digit of Pi is a 9.";
		}

		[Command(
			names: new string[] { "the best joke from town with no name", "what is the best joke from town with no name" },
			hidden: true
		)]
		public static string ConroTownWithNoName(DiscordBot bot, Message msg, string args)
		{
			return "https://youtu.be/WeV18bZGMqc?t=1341";
		}

		[Command(name: "thanks", hidden: true)]
		public static string Thanks(DiscordBot bot, Message msg, string args)
		{
			return "you're welcome bby ;)";
		}

		[Command(name: "seinfeld", desc: "seinfeld theme", usage: "seinfeld", category: "Dumb")]
		public static string Seinfeld(DiscordBot bot, Message msg, string args)
		{
			return "https://www.youtube.com/watch?v=_V2sBURgUBI";
		}

		[Command(names: new string[] { "love me", "i love you" }, hidden: true)]
		public static string LoveYou(DiscordBot bot, Message msg, string args)
		{
			if(msg.User.Id == 101100871227543552)
			{
				return "love u too jenelle";
			}
			return Util.Locale("dumb.ily");
		}
	}
}
