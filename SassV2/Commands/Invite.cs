using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace SassV2.Commands
{
	public class InviteCommand
	{
		[Command(name: "invite", desc: "invite sass to your server.", usage: "invite")]
		public static string Invite(DiscordBot bot, IMessage msg, string args)
		{
			return "Click this: https://discordapp.com/oauth2/authorize?client_id=" + bot.Config.ClientID + "&scope=bot&permissions=104061952";
		}
	}
}
