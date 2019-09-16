using Discord;
using Discord.Commands;
using SassV2.Web;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Role : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public Role(DiscordBot bot) => _bot = bot;

		[Command("roles manage")]
		[SassCommand(
			name: "roles manage",
			desc: "links you to the admin page for SASS roles",
			usage: "roles manage",
			category: "Administration"
		)]
		[RequireContext(ContextType.Guild)]
		public async Task ManageRoles()
		{
			if(!(Context.Message.Author as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync("You're not an admin of this server.");
				return;
			}

			var dm = await Context.Message.Author.GetOrCreateDMChannelAsync();
			await dm.SendMessageAsync(await AuthCodeManager.GetURL("/admin/roles/" + Context.Guild.Id, Context.User, _bot));

			await ReplyAsync("Check your DMs for a link.");
		}

		[Command("roles")]
		[SassCommand(
			name: "roles",
			desc: "links you to the page to manage your roles",
			usage: "roles",
			category: "Useful")]
		public async Task Roles()
		{
			var url = "/roles/manage";
			if(!Context.IsPrivate)
			{
				url = $"/roles/manage/{Context.Guild.Id}";
			}

			var dm = await Context.Message.Author.GetOrCreateDMChannelAsync();
			await dm.SendMessageAsync(await AuthCodeManager.GetURL(url, Context.User, _bot));

			if(!Context.IsPrivate)
			{
				await ReplyAsync("Check your DMs for a link.");
			}
		}
	}
}
