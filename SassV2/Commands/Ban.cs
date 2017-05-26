using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.Reactive.Linq;
using Discord.Commands;

namespace SassV2.Commands
{
	public class Ban : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public Ban(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(name: "ban", desc: "bans a user from SASS", usage: "ban <name>", category: "Administration")]
		[Command("ban")]
		[RequireContext(ContextType.Guild)]
		public async Task BanUser([Remainder] string user)
		{
			if(!(Context.Message.Author as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync(Util.Locale("ban.notAdmin"));
				return;
			}

			var foundUsers = await Util.FindWithName(user, Context.Message);
			if(!foundUsers.Any())
			{
				await ReplyAsync(Util.Locale("ban.noneFound"));
				return;
			}
			if(foundUsers.Count() > 1)
			{
				await ReplyAsync(Util.Locale("ban.moreFound"));
				return;
			}

			var target = foundUsers.First();
			var targetPermissions = (target as IGuildUser).GuildPermissions;
			if(targetPermissions.Administrator || _bot.Config.GetRole(target.Id) == "admin")
			{
				await ReplyAsync(Util.Locale("ban.foundAdmin"));
				return;
			}

			_bot.Database(Context.Message.ServerId()).InsertObject<bool>("ban:" + target.Id, true);
			await ReplyAsync(Util.Locale("ban.sure"));
		}

		[SassCommand(name: "unban", desc: "unbans a user from SASS", usage: "unban <name>", category: "Administration")]
		[Command("unban")]
		[RequireContext(ContextType.Guild)]
		public async Task UnbanUser([Remainder] string args)
		{
			if(!(Context.User as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync(Util.Locale("unban.notAdmin"));
				return;
			}

			var foundUsers = await Util.FindWithName(args, Context.Message);
			if(!foundUsers.Any())
			{
				await ReplyAsync(Util.Locale("unban.noneFound"));
				return;
			}
			if(foundUsers.Count() > 1)
			{
				await ReplyAsync(Util.Locale("unban.moreFound"));
				return;
			}
			
			_bot.Database(Context.Message.ServerId()).InvalidateObject<bool>("ban:" + foundUsers.First().Id);
			await ReplyAsync(Util.Locale("unban.sure"));
		}
	}
}
