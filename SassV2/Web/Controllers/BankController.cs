using Discord;
using NLog;
using SassV2.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace SassV2.Web.Controllers
{
	public class BankController : BaseController
	{
		private DiscordBot _bot;
		private Logger _logger;

		public BankController(DiscordBot bot, ViewManager viewManager) : base(viewManager)
		{
			_bot = bot;
			_logger = LogManager.GetCurrentClassLogger();
		}

		[WebApiHandler(HttpVerbs.Post, "^/bank/new/{id}")]
		public async Task<bool> BankCreate(WebServer server, HttpListenerContext context, ulong id)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			if(_bot.Client.GetGuild(id) == null)
			{
				return await Error(server, context, "The given guild doesn't exist.");
			}

			var user = AuthManager.GetUser(server, context, _bot);
			var data = Extensions.RequestFormDataDictionary(context);
			var db = _bot.RelDatabase(id);
			await db.BuildAndExecute("BEGIN TRANSACTION;");
			var t = new BankTransaction(db)
			{
				Name = data["name"].ToString(),
				Notes = data["notes"].ToString(),
				Creator = user.Id,
				Timestamp = DateTime.Now
			};

			await t.Save();
			await BankTransactionIndex.RegisterServer(_bot.GlobalDatabase, db.ServerId.Value, user.Id);
			await CreateTransactionRecords(data, db, t);
			await db.BuildAndExecute("END TRANSACTION;");
			return Redirect(server, context, string.Format("/bank/view/{0}/{1}", t.ServerId, t.Id));
		}

		[WebApiHandler(HttpVerbs.Get, "^/bank/new/{id}")]
		public async Task<bool> BankNew(WebServer server, HttpListenerContext context, ulong id)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			if(_bot.Client.GetGuild(id) == null)
			{
				return await Error(server, context, "The given guild doesn't exist.");
			}

			var users = _bot.Client.GetGuild(id).Users.Select(u => (u.Id, u.RealNameOrDefaultSync(_bot))).OrderBy(g => g.Item2);

			return await ViewResponse(server, context, "bank/new", new
			{
				Title = "New Bank Transaction",
				GuildId = id,
				Users = users,
				Edit = false,
				Transaction = new BankTransaction(_bot.RelDatabase(id))
			});
		}

		[WebApiHandler(HttpVerbs.Post, "^/bank/new")]
		public async Task<bool> BankNewPost(WebServer server, HttpListenerContext context)
		{
			var dictionary = Extensions.RequestFormDataDictionary(context);
			if(!dictionary.ContainsKey("guild"))
			{
				return await Error(server, context, "No guild chosen.");
			}
			return Redirect(server, context, "/bank/new/" + dictionary["guild"]);
		}

		[WebApiHandler(HttpVerbs.Get, "^/bank/new")]
		public async Task<bool> BankNewGuild(WebServer server, HttpListenerContext context)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var guilds = _bot.Client.GuildsContainingUser(AuthManager.GetUser(server, context, _bot));
			return await ViewResponse(server, context, "bank/guild", new
			{
				Title = "New Bank Transaction",
				Guilds = guilds
			});
		}

		// Token: 0x0600016C RID: 364 RVA: 0x00007930 File Offset: 0x00005B30
		[WebApiHandler(HttpVerbs.Post, "^/bank/edit/transaction/{serverId}/{id}")]
		public async Task<bool> BankCommitEditTransaction(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			if(!_bot.ServerIds.Contains(serverId))
			{
				return await Error(server, context, "Invalid guild ID.");
			}

			var db = _bot.RelDatabase(serverId);
			var bankTransaction = await DataTable<BankTransaction>.TryLoad(db, id);
			var transaction = bankTransaction;
			if(transaction == null)
			{
				return await Error(server, context, "Invalid transaction ID.");
			}

			if(user.Id != transaction.Creator)
			{
				return await ForbiddenError(server, context);
			}

			await db.BuildAndExecute("BEGIN TRANSACTION;");
			var dictionary = Extensions.RequestFormDataDictionary(context);
			transaction.Name = dictionary["name"].ToString();
			transaction.Notes = dictionary["notes"].ToString();
			await CreateTransactionRecords(dictionary, db, transaction);
			await transaction.Save();
			await db.BuildAndExecute("END TRANSACTION;");
			return Redirect(server, context, string.Format("/bank/view/{0}/{1}", serverId, transaction.Id));
		}

		[WebApiHandler(HttpVerbs.Get, "^/bank/edit/transaction/{serverId}/{id}")]
		public async Task<bool> BankEditTransaction(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			if(!_bot.ServerIds.Contains(serverId))
			{
				return await Error(server, context, "Invalid guild ID.");
			}

			var bankTransaction = await DataTable<BankTransaction>.TryLoad(_bot.RelDatabase(serverId), id);
			if(bankTransaction == null)
			{
				return await Error(server, context, "Invalid transaction ID.");
			}

			if(user.Id != bankTransaction.Creator)
			{
				return await ForbiddenError(server, context);
			}

			var users = _bot.Client.GetGuild(serverId).Users.Select(u => (u.Id, u.RealNameOrDefaultSync(_bot))).OrderBy(g => g.Item2);

			return await ViewResponse(server, context, "bank/new", new
			{
				Title = "Edit Transaction",
				Transaction = bankTransaction,
				GuildId = serverId,
				Edit = true,
				Users = users
			});
		}

		[WebApiHandler(HttpVerbs.Post, "^/bank/settle/{serverId}/{id}")]
		public async Task<bool> BankSettle(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			if(!_bot.ServerIds.Contains(serverId))
			{
				return await Error(server, context, "Invalid guild ID.");
			}

			var db = _bot.RelDatabase(serverId);
			var bankBalance = await DataTable<BankBalance>.TryLoad(db, id);
			var balance = bankBalance;

			if(balance == null)
			{
				return await Error(server, context, "Invalid balance ID.");
			}

			if(new BankTransaction(db, balance.TransactionId).Creator != user.Id)
			{
				return await ForbiddenError(server, context);
			}

			balance.Settled = true;
			await balance.Save();
			return Redirect(server, context, string.Format("/bank/view/balance/{0}/{1}", serverId, balance.Id));
		}

		[WebApiHandler(HttpVerbs.Post, "^/bank/payment/{serverId}/{id}")]
		public async Task<bool> BankAddPaymentCommit(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			if(!_bot.ServerIds.Contains(serverId))
			{
				return await Error(server, context, "Invalid guild ID.");
			}

			var db = _bot.RelDatabase(serverId);
			var bankBalance = await DataTable<BankBalance>.TryLoad(db, id);
			var balance = bankBalance;

			if(balance == null)
			{
				return await Error(server, context, "Invalid balance ID.");
			}

			if(balance.Settled)
			{
				return await Error(server, context, "You can't add a payment to a settled balance!");
			}

			if(new BankTransaction(db, balance.TransactionId).Creator != user.Id)
			{
				return await ForbiddenError(server, context);
			}

			var data = Extensions.RequestFormDataDictionary(context);
			if(!decimal.TryParse(data["amount"].ToString(), out var amount))
			{
				return await Error(server, context, "Amount is invalid.");
			}

			await db.BuildAndExecute("BEGIN TRANSACTION;");
			balance.Amount += amount;
			await balance.Save();

			await new BankBalanceRecord(db, balance.Id.Value, amount)
			{
				Note = data["note"].ToString()
			}.Save();

			await db.BuildAndExecute("END TRANSACTION;");
			return Redirect(server, context, string.Format("/bank/view/balance/{0}/{1}", serverId, balance.Id));
		}

		// Token: 0x06000170 RID: 368 RVA: 0x00007AD0 File Offset: 0x00005CD0
		[WebApiHandler(HttpVerbs.Get, "^/bank/payment/{serverId}/{id}")]
		public async Task<bool> BankAddPayment(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			if(!_bot.ServerIds.Contains(serverId))
			{
				return await Error(server, context, "Invalid guild ID.");
			}

			var db = _bot.RelDatabase(serverId);
			var bankBalance = await DataTable<BankBalance>.TryLoad(db, id);
			if(bankBalance == null)
			{
				return await Error(server, context, "Invalid balance ID.");
			}

			var bankTransaction = new BankTransaction(db, bankBalance.TransactionId);
			if(bankTransaction.Creator != user.Id)
			{
				return await ForbiddenError(server, context);
			}

			return await ViewResponse(server, context, "bank/payment", new
			{
				Title = "Add Payment",
				Balance = bankBalance,
				Transaction = bankTransaction
			});
		}

		// Token: 0x06000171 RID: 369 RVA: 0x00007B38 File Offset: 0x00005D38
		[WebApiHandler(HttpVerbs.Get, "^/bank/view/balance/{serverId}/{id}")]
		public async Task<bool> BankViewBalance(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			if(!_bot.ServerIds.Contains(serverId))
			{
				return await Error(server, context, "Invalid guild ID.");
			}

			var db = _bot.RelDatabase(serverId);
			var bankBalance = await DataTable<BankBalance>.TryLoad(db, id);
			var balance = bankBalance;
			if(balance == null)
			{
				return await Error(server, context, "Invalid balance ID.");
			}

			var transaction = new BankTransaction(db, balance.TransactionId);
			if(!(await transaction.UserCanView(db, user.Id)))
			{
				return await ForbiddenError(server, context);
			}

			var records = await BankBalanceRecord.AllForBalance(db, balance);
			var bioData = await Bio.GetBio(_bot, serverId, transaction.Creator);
			var guildUser = _bot.Client.GetGuild(serverId).GetUser(balance.DiscordUserId);
			var isCreator = user.Id == transaction.Creator;
			var isDebtor = user.Id == balance.DiscordUserId;
			string text = bioData["paypal"]?.Value;

			var payPal = text;
			return await ViewResponse(server, context, "bank/view_balance", new
			{
				Title = "View Balance",
				IsCreator = isCreator,
				IsDebtor = isDebtor,
				Records = records,
				PayPal = payPal,
				Balance = balance,
				Nickname = await guildUser.RealNameOrDefault(_bot),
				Name = guildUser.Username,
				Transaction = transaction
			});
		}

		// Token: 0x06000172 RID: 370 RVA: 0x00007BA0 File Offset: 0x00005DA0
		[WebApiHandler(HttpVerbs.Get, "^/bank/view/{serverId}/{id}")]
		public async Task<bool> BankViewTransaction(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}

			var user = AuthManager.GetUser(server, context, _bot);
			if(!_bot.ServerIds.Contains(serverId))
			{
				return await Error(server, context, "Invalid guild ID.");
			}

			var db = _bot.RelDatabase(serverId);
			var bankTransaction = await DataTable<BankTransaction>.TryLoad(db, id);
			var transaction = bankTransaction;
			if(transaction == null)
			{
				return await Error(server, context, "Invalid transaction ID.");
			}

			var balances = await BankBalance.AllForTransaction(db, transaction);
			if(!balances.Any((BankBalance b) => b.DiscordUserId == user.Id) && user.Id != transaction.Creator)
			{
				return await ForbiddenError(server, context);
			}

			var author = _bot.Client.GetUser(transaction.Creator);
			if(author == null)
			{
				return await Error(server, context, "The creator of this transaction is invalid.");
			}

			var guild = _bot.Client.GetGuild(serverId);
			var authorBio = await Bio.GetBio(_bot, serverId, author.Id);
			var transactionAuthorModel = new TransactionAuthorModel
			{
				Name = author.Username
			};
			var transactionAuthorModel2 = transactionAuthorModel;
			var nickname = transactionAuthorModel2.Nickname;
			transactionAuthorModel2.Nickname = await guild.GetUser(transaction.Creator).RealNameOrDefault(_bot);
			var transactionAuthorModel3 = transactionAuthorModel;
			var bioData = authorBio;
			string payPal = bioData["paypal"]?.Value;

			transactionAuthorModel3.PayPal = payPal;
			var creator = transactionAuthorModel;
			transactionAuthorModel2 = null;
			transactionAuthorModel = null;
			var balances2 = balances.Select(b =>
			{
				var transactionBalanceModel = new TransactionBalanceModel
				{
					Id = b.Id.Value
				};
				var balanceUser = guild.GetUser(b.DiscordUserId);
				transactionBalanceModel.Author = (balanceUser?.RealNameOrDefaultSync(_bot)) ?? ("unknown user id " + b.DiscordUserId);
				transactionBalanceModel.Amount = -b.Amount;
				transactionBalanceModel.IsSettled = b.Settled;
				return transactionBalanceModel;
			});
			return await ViewResponse(server, context, "bank/view", new
			{
				Title = "View Transaction",
				Transaction = transaction,
				Creator = creator,
				Balances = balances2,
				IsCreator = (user.Id == transaction.Creator)
			});
		}

		[WebApiHandler(HttpVerbs.Get, "^/bank/transactions")]
		public async Task<bool> BankTransactions(WebServer server, HttpListenerContext context)
		{
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				IEnumerable<ulong> enumerable = await BankTransactionIndex.ServersForUser(_bot.GlobalDatabase, user.Id);
				List<BankTransaction> transactions = new List<BankTransaction>();
				foreach(ulong serverId in enumerable)
				{
					List<BankTransaction> list = transactions;
					list.AddRange(await BankTransaction.TransactionsByUser(_bot.RelDatabase(serverId), user.Id));
					list = null;
				}
				result = await ViewResponse(server, context, "bank/transactions", new
				{
					Title = "All Transactions",
					Transactions = transactions
				});
			}
			return result;
		}

		// Token: 0x06000174 RID: 372 RVA: 0x00007C60 File Offset: 0x00005E60
		[WebApiHandler(HttpVerbs.Get, "^/bank/balances")]
		public async Task<bool> BankBalances(WebServer server, HttpListenerContext context)
		{
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				IEnumerable<ulong> enumerable = await BankTransactionIndex.ServersForUser(_bot.GlobalDatabase, user.Id);
				List<BankTransaction> balances = new List<BankTransaction>();
				foreach(ulong serverId in enumerable)
				{
					List<BankTransaction> list = balances;
					list.AddRange(await BankTransaction.TransactionsForUser(_bot.RelDatabase(serverId), user.Id));
					list = null;
				}
				result = await ViewResponse(server, context, "bank/balances", new
				{
					Title = "All Balances",
					Balances = balances
				});
			}
			return result;
		}

		// Token: 0x06000175 RID: 373 RVA: 0x00007CB8 File Offset: 0x00005EB8
		[WebApiHandler(HttpVerbs.Get, "^/bank")]
		public async Task<bool> BankIndex(WebServer server, HttpListenerContext context)
		{
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				IEnumerable<ulong> enumerable = await BankTransactionIndex.ServersForUser(_bot.GlobalDatabase, user.Id);
				List<BankTransaction> userOwes = new List<BankTransaction>();
				List<BankTransaction> owedToUser = new List<BankTransaction>();
				foreach(ulong sid in enumerable)
				{
					List<BankTransaction> list = userOwes;
					list.AddRange(await BankTransaction.OpenTransactionsForUser(_bot.RelDatabase(sid), user.Id));
					list = null;
					list = owedToUser;
					list.AddRange(await BankTransaction.OpenTransactionsByUser(_bot.RelDatabase(sid), user.Id));
					list = null;
				}
				result = await ViewResponse(server, context, "bank/index", new
				{
					Title = "First Bank of SASS",
					Owed = owedToUser,
					Owes = userOwes
				});
			}
			return result;
		}

		// Token: 0x06000176 RID: 374 RVA: 0x00007D10 File Offset: 0x00005F10
		private async Task CreateTransactionRecords(Dictionary<string, object> data, RelationalDatabase db, BankTransaction t)
		{
			List<string> users = Util.ReadFormArray(data, "record_user");
			List<string> notes = Util.ReadFormArray(data, "record_note");
			List<string> amounts = Util.ReadFormArray(data, "record_amount");
			for(int i = 0; i < users.Count; i++)
			{
				BankBalance bal = new BankBalance(db)
				{
					DiscordUserId = ulong.Parse(users[i]),
					TransactionId = t.Id.Value,
					Amount = -decimal.Parse(amounts[i])
				};
				await bal.Save();
				await BankTransactionIndex.RegisterServer(_bot.GlobalDatabase, db.ServerId.Value, bal.DiscordUserId);
				await new BankBalanceRecord(db)
				{
					Amount = bal.Amount,
					BalanceId = bal.Id.Value,
					Note = notes[i],
					Timestamp = DateTime.Now
				}.Save();
				bal = null;
			}
		}

		public class TransactionAuthorModel
		{
			public string Nickname;
			public string Name;
			public string PayPal;
		}

		public class TransactionBalanceModel
		{
			public long Id;
			public string Author;
			public decimal Amount;
			public string Notes;
			public bool IsAuthor;
			public bool IsSettled;
		}
	}
}
