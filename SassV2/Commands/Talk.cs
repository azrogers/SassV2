using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatterBotAPI;
using Discord;

namespace SassV2.Commands
{
	public static class TalkCommand
	{
		private static ChatterBotFactory _botFactory = new ChatterBotFactory();
		private static ChatterBot _bot;
		private static ChatterBotSession _session;
		private static DateTime _sessionLastUsed; 

		static TalkCommand()
		{
			_bot = _botFactory.Create(ChatterBotType.CLEVERBOT);
		}

		[Command(name: "talk", desc: "engage in a turing test.", usage: "talk <things>", category: "Spam")]
		public static async Task<string> Talk(DiscordBot bot, Message msg, string args)
		{
			var session = GetOrCreateSession();
			await msg.Channel.SendIsTyping();
			return session.Think(args);
		}

		private static ChatterBotSession GetOrCreateSession()
		{
			if(_session != null && DateTime.Now.Subtract(_sessionLastUsed).TotalMinutes <= 2)
			{
				_sessionLastUsed = DateTime.Now;
				return _session;
			}
			_sessionLastUsed = DateTime.Now;
			return _session = _bot.CreateSession();
		}
	}
}
