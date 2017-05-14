﻿using System;
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
		public static string ConroPi(DiscordBot bot, IMessage msg, string args)
		{
			return Util.Locale("dumb.pi");
		}

		[Command(
			names: new string[] { "the best joke from town with no name", "what is the best joke from town with no name" },
			hidden: true
		)]
		public static string ConroTownWithNoName(DiscordBot bot, IMessage msg, string args)
		{
			return "https://youtu.be/WeV18bZGMqc?t=1341";
		}

		[Command(name: "thanks", hidden: true)]
		public static string Thanks(DiscordBot bot, IMessage msg, string args)
		{
			return Util.Locale("dumb.thanks");
		}

		[Command(name: "seinfeld", desc: "seinfeld theme", usage: "seinfeld", category: "Dumb")]
		public static string Seinfeld(DiscordBot bot, IMessage msg, string args)
		{
			return "https://www.youtube.com/watch?v=_V2sBURgUBI";
		}

		[Command(names: new string[] { "love me", "i love you" }, hidden: true)]
		public static string LoveYou(DiscordBot bot, IMessage msg, string args)
		{
			if(msg.Author.Id == 101100871227543552)
			{
				return Util.Locale("dumb.ilyjenelle");
			}
			return Util.Locale("dumb.ily");
		}

		[Command(name: "crippling debt", hidden: true)]
		public static string CripplingDebt(DiscordBot bot, IMessage msg, string args)
		{
			return "http://i.imgur.com/JxRGHO0.jpg";
		}

		[Command(name: "harambe", hidden: true)]
		public static string Harambe(DiscordBot bot, IMessage msg, string args)
		{
			return "shut the fuck up chloe\nhttps://66.media.tumblr.com/b4befe07adbd872f7c7a734ada8acd9a/tumblr_oc3a77ZplG1tgdhtio1_500.gif";
		}
	}
}
