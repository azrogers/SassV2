using Discord.Commands;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Bank : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public Bank(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "bank",
			desc: "open up the bank page",
			category: "Bank")]
		[Command("bank")]
		public async Task OpenBank()
		{
			var dm = await Context.User.GetOrCreateDMChannelAsync();
			await dm.SendMessageAsync(await Web.AuthCodeManager.GetURL("/bank", Context.User, _bot));
		}

		[SassCommand(
			name: "who owes me",
			desc: "who owes me money",
			category: "Bank")]
		[Command("who owes me")]
		[RequireContext(ContextType.Guild)]
		public async Task WhoOwesMe([Remainder] string args = null)
		{
			var db = _bot.RelDatabase(Context.Guild.Id);
			var transactions = await BankTransaction.OpenTransactionsByUser(db, Context.User.Id);
			var forUser = new Dictionary<ulong, List<BankTransaction>>();
			foreach(var t in transactions)
			{
				var balances = await BankBalance.AllForTransaction(db, t);
				foreach(var bal in balances)
				{
					if(bal.Settled) continue;
					if(forUser.ContainsKey(bal.DiscordUserId))
						forUser[bal.DiscordUserId].Add(t);
					else
						forUser[bal.DiscordUserId] = new List<BankTransaction>() { t };
				}
			}

			if(forUser.Keys.Count == 0)
			{
				await ReplyAsync("No users owe you money.");
				return;
			}
			
			var msg = "These users owe you money:";
			foreach(var user in forUser.Keys)
			{
				var tnames = Util.NaturalArrayJoin(forUser[user].Select(t => t.Name));
				msg += $"\n\t{Context.Guild.GetUser(user).Mention} owes you for {tnames}.";
			}

			await ReplyAsync(msg);
		}
	}
}
