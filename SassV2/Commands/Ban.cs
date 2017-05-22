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
		public static async Task<string> BanUser(DiscordBot bot, IMessage msg, string args)
		{
			if(!(msg.Author as IGuildUser).IsAdmin(bot))
			{
				throw new CommandException(Util.Locale("ban.notAdmin"));
			}

			var foundUsers = await Util.FindWithName(args, msg);
			if(!foundUsers.Any())
			{
				throw new CommandException(Util.Locale("ban.noneFound"));
			}
			if(foundUsers.Count() > 1)
			{
				throw new CommandException(Util.Locale("ban.moreFound"));
			}

			var target = foundUsers.First();
			var targetPermissions = (target as IGuildUser).GuildPermissions;
			if(targetPermissions.Administrator || bot.Config.GetRole(target.Id) == "admin")
			{
				throw new CommandException(Util.Locale("ban.foundAdmin"));
			}

			bot.Database(msg.ServerId()).InsertObject<bool>("ban:" + target.Id, true);
			return Util.Locale("ban.sure");
		}

		[Command(name: "unban", desc: "unbans a user from SASS", usage: "unban <name>", category: "Administration")]
		public static async Task<string> UnbanUser(DiscordBot bot, IMessage msg, string args)
		{
			if(!(msg.Author as IGuildUser).IsAdmin(bot))
			{
				throw new CommandException(Util.Locale("unban.notAdmin"));
			}

			var foundUsers = await Util.FindWithName(args, msg);
			if(!foundUsers.Any())
			{
				throw new CommandException(Util.Locale("unban.noneFound"));
			}
			if(foundUsers.Count() > 1)
			{
				throw new CommandException(Util.Locale("unban.moreFound"));
			}
			
			bot.Database(msg.ServerId()).InvalidateObject<bool>("ban:" + foundUsers.First().Id);
			return Util.Locale("unban.sure");
		}
	}
}
