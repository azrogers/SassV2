using Discord.Commands;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class InviteCommand : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public InviteCommand(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(name: "invite", desc: "invite sass to your server.", usage: "invite")]
		[Command("invite")]
		public async Task Invite()
		{
			await ReplyAsync(
				"Click this: https://discordapp.com/oauth2/authorize?client_id=" + _bot.Config.ClientID + "&scope=bot&permissions=104061952");
		}
	}
}
