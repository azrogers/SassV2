using Discord;
using Discord.Commands;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	/// <summary>
	/// Bans users from SASS (can't use SASS commands anymore).
	/// </summary>
	public class Ban : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public Ban(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "ban",
			desc: "Bans a user from SASS. This doesn't ban the user from your guild; it only prevents them from using SASS.",
			usage: "ban <name or mention>",
			example: "ban @Scripted Automated Speech System",
			category: "Administration")]
		[Command("ban")]
		[RequireContext(ContextType.Guild)]
		public async Task BanUser([Remainder] string user)
		{
			if(!(Context.Message.Author as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "ban.notAdmin"));
				return;
			}

			// look for user given by admin
			var foundUsers = await Util.FindWithName(user, Context.Message);
			if(!foundUsers.Any())
			{
				// found 0 people
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "ban.noneFound"));
				return;
			}
			if(foundUsers.Count() > 1)
			{
				// found more than one person
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "ban.moreFound"));
				return;
			}

			var target = foundUsers.First();
			var targetPermissions = (target as IGuildUser).GuildPermissions;
			// can't ban an admin!
			if(targetPermissions.Administrator || _bot.Config.GetRole(target.Id) == "admin")
			{
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "ban.foundAdmin"));
				return;
			}

			// register ban in database
			_bot.Database(Context.Message.GuildId()).InsertObject("ban:" + target.Id, true);
			await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "ban.sure"));
		}

		[SassCommand(
			name: "unban",
			desc: "Unbans a user from SASS.",
			usage: "unban <name or mention>",
			example: "unban @Scripted Automated Speech System",
			category: "Administration")]
		[Command("unban")]
		[RequireContext(ContextType.Guild)]
		public async Task UnbanUser([Remainder] string args)
		{
			if(!(Context.User as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "unban.notAdmin"));
				return;
			}

			var foundUsers = await Util.FindWithName(args, Context.Message);
			if(!foundUsers.Any())
			{
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "unban.noneFound"));
				return;
			}
			if(foundUsers.Count() > 1)
			{
				await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "unban.moreFound"));
				return;
			}
			
			// delete ban
			_bot.Database(Context.Message.GuildId()).InvalidateObject("ban:" + foundUsers.First().Id);
			await ReplyAsync(Locale.GetString(_bot.Language(Context.Guild?.Id), "unban.sure"));
		}
	}
}
