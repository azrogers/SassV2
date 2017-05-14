using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.Reactive.Linq;

namespace SassV2.Commands
{
	public class Ban
	{
		[Command(name: "ban", desc: "bans a user from SASS", usage: "ban <name>", category: "Administration")]
		public static string BanUser(DiscordBot bot, Message msg, string args)
		{
			if(!msg.User.ServerPermissions.Administrator && bot.Config.GetRole(msg.User.Id) != "admin")
			{
				throw new CommandException(Util.Locale("ban.notAdmin"));
			}

			var foundUsers = Util.FindWithName(args, msg);
			if(!foundUsers.Any())
			{
				throw new CommandException(Util.Locale("ban.noneFound"));
			}
			if(foundUsers.Count() > 1)
			{
				throw new CommandException(Util.Locale("ban.moreFound"));
			}
			var target = foundUsers.First();
			if(target.ServerPermissions.Administrator || bot.Config.GetRole(target.Id) == "admin")
			{
				throw new CommandException(Util.Locale("ban.foundAdmin"));
			}

			bot.Database(msg.Server.Id).InsertObject<bool>("ban:" + target.Id, true);
			return Util.Locale("ban.sure");
		}

		[Command(name: "unban", desc: "unbans a user from SASS", usage: "unban <name>", category: "Administration")]
		public static string UnbanUser(DiscordBot bot, Message msg, string args)
		{
			if(!msg.User.ServerPermissions.Administrator && bot.Config.GetRole(msg.User.Id) != "admin")
			{
				throw new CommandException(Util.Locale("unban.notAdmin"));
			}

			var foundUsers = Util.FindWithName(args, msg);
			if(!foundUsers.Any())
			{
				throw new CommandException(Util.Locale("unban.noneFound"));
			}
			if(foundUsers.Count() > 1)
			{
				throw new CommandException(Util.Locale("unban.moreFound"));
			}
			
			bot.Database(msg.Server.Id).InvalidateObject<bool>("ban:" + foundUsers.First().Id);
			return Util.Locale("unban.sure");
		}
	}
}
