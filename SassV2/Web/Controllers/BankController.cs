using Discord;
using Discord.WebSocket;
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
	// Token: 0x02000039 RID: 57
	public class BankController : BaseController
	{
		// Token: 0x06000167 RID: 359 RVA: 0x00007756 File Offset: 0x00005956
		public BankController(DiscordBot bot, ViewManager viewManager) : base(viewManager)
		{
			_bot = bot;
			_logger = LogManager.GetCurrentClassLogger();
		}

		// Token: 0x06000168 RID: 360 RVA: 0x00007774 File Offset: 0x00005974
		[WebApiHandler(HttpVerbs.Post, "^/bank/new/{id}")]
		public async Task<bool> BankCreate(WebServer server, HttpListenerContext context, ulong id)
		{
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else if(_bot.Client.GetGuild(id) == null)
			{
				result = await Error(server, context, "The given guild doesn't exist.");
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				Dictionary<string, object> data = Extensions.RequestFormDataDictionary(context);
				RelationalDatabase db = _bot.RelDatabase(id);
				await db.BuildAndExecute("BEGIN TRANSACTION;");
				BankTransaction t = new BankTransaction(db)
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
				result = base.Redirect(server, context, string.Format("/bank/view/{0}/{1}", t.ServerId, t.Id));
			}
			return result;
		}

		// Token: 0x06000169 RID: 361 RVA: 0x000077D4 File Offset: 0x000059D4
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
			var users = from u in _bot.Client.GetGuild(id).Users
						select new ValueTuple<ulong, string>(u.Id, u.RealNameOrDefaultSync(_bot)) into g
						orderby g.Item2
						select g;
			return await ViewResponse(server, context, "bank/new", new
			{
				Title = "New Bank Transaction",
				GuildId = id,
				Users = users,
				Edit = false,
				Transaction = new BankTransaction(_bot.RelDatabase(id))
			});
		}

		// Token: 0x0600016A RID: 362 RVA: 0x0000788C File Offset: 0x00005A8C
		[WebApiHandler(HttpVerbs.Post, "^/bank/new")]
		public async Task<bool> BankNewPost(WebServer server, HttpListenerContext context)
		{
			Dictionary<string, object> dictionary = Extensions.RequestFormDataDictionary(context);
			if(!dictionary.ContainsKey("guild"))
			{
				return await Error(server, context, "No guild chosen.");
			}
			return Redirect(server, context, "/bank/new/" + dictionary["guild"]);
		}

		// Token: 0x0600016B RID: 363 RVA: 0x000078D8 File Offset: 0x00005AD8
		[WebApiHandler(HttpVerbs.Get, "^/bank/new")]
		public async Task<bool> BankNewGuild(WebServer server, HttpListenerContext context)
		{
			if(!AuthManager.IsAuthenticated(server, context))
			{
				return await ForbiddenError(server, context);
			}
			IEnumerable<IGuild> guilds = _bot.Client.GuildsContainingUser(AuthManager.GetUser(server, context, _bot));
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
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				if(!_bot.ServerIds.Contains(serverId))
				{
					result = await Error(server, context, "Invalid guild ID.");
				}
				else
				{
					RelationalDatabase db = _bot.RelDatabase(serverId);
					BankTransaction bankTransaction = await DataTable<BankTransaction>.TryLoad(db, id);
					BankTransaction transaction = bankTransaction;
					if(transaction == null)
					{
						result = await Error(server, context, "Invalid transaction ID.");
					}
					else if(user.Id != transaction.Creator)
					{
						result = await ForbiddenError(server, context);
					}
					else
					{
						await db.BuildAndExecute("BEGIN TRANSACTION;");
						Dictionary<string, object> dictionary = Extensions.RequestFormDataDictionary(context);
						transaction.Name = dictionary["name"].ToString();
						transaction.Notes = dictionary["notes"].ToString();
						await CreateTransactionRecords(dictionary, db, transaction);
						await transaction.Save();
						await db.BuildAndExecute("END TRANSACTION;");
						result = base.Redirect(server, context, string.Format("/bank/view/{0}/{1}", serverId, transaction.Id));
					}
				}
			}
			return result;
		}

		// Token: 0x0600016D RID: 365 RVA: 0x00007998 File Offset: 0x00005B98
		[WebApiHandler(HttpVerbs.Get, "^/bank/edit/transaction/{serverId}/{id}")]
		public async Task<bool> BankEditTransaction(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				if(!_bot.ServerIds.Contains(serverId))
				{
					result = await Error(server, context, "Invalid guild ID.");
				}
				else
				{
					BankTransaction bankTransaction = await DataTable<BankTransaction>.TryLoad(_bot.RelDatabase(serverId), id);
					if(bankTransaction == null)
					{
						result = await Error(server, context, "Invalid transaction ID.");
					}
					else if(user.Id != bankTransaction.Creator)
					{
						result = await ForbiddenError(server, context);
					}
					else
					{
						IOrderedEnumerable<ValueTuple<ulong, string>> users = from u in _bot.Client.GetGuild(serverId).Users
																			  select new ValueTuple<ulong, string>(u.Id, u.RealNameOrDefaultSync(_bot)) into g
																			  orderby g.Item2
																			  select g;
						result = await ViewResponse(server, context, "bank/new", new
						{
							Title = "Edit Transaction",
							Transaction = bankTransaction,
							GuildId = serverId,
							Edit = true,
							Users = users
						});
					}
				}
			}
			return result;
		}

		// Token: 0x0600016E RID: 366 RVA: 0x00007A00 File Offset: 0x00005C00
		[WebApiHandler(HttpVerbs.Post, "^/bank/settle/{serverId}/{id}")]
		public async Task<bool> BankSettle(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				if(!_bot.ServerIds.Contains(serverId))
				{
					result = await Error(server, context, "Invalid guild ID.");
				}
				else
				{
					RelationalDatabase db = _bot.RelDatabase(serverId);
					BankBalance bankBalance = await DataTable<BankBalance>.TryLoad(db, id);
					BankBalance balance = bankBalance;
					if(balance == null)
					{
						result = await Error(server, context, "Invalid balance ID.");
					}
					else if(new BankTransaction(db, balance.TransactionId).Creator != user.Id)
					{
						result = await ForbiddenError(server, context);
					}
					else
					{
						balance.Settled = true;
						await balance.Save();
						result = base.Redirect(server, context, string.Format("/bank/view/balance/{0}/{1}", serverId, balance.Id));
					}
				}
			}
			return result;
		}

		// Token: 0x0600016F RID: 367 RVA: 0x00007A68 File Offset: 0x00005C68
		[WebApiHandler(HttpVerbs.Post, "^/bank/payment/{serverId}/{id}")]
		public async Task<bool> BankAddPaymentCommit(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				if(!_bot.ServerIds.Contains(serverId))
				{
					result = await Error(server, context, "Invalid guild ID.");
				}
				else
				{
					RelationalDatabase db = _bot.RelDatabase(serverId);
					BankBalance bankBalance = await DataTable<BankBalance>.TryLoad(db, id);
					BankBalance balance = bankBalance;
					if(balance == null)
					{
						result = await Error(server, context, "Invalid balance ID.");
					}
					else if(balance.Settled)
					{
						result = await Error(server, context, "You can't add a payment to a settled balance!");
					}
					else if(new BankTransaction(db, balance.TransactionId).Creator != user.Id)
					{
						result = await ForbiddenError(server, context);
					}
					else
					{
						Dictionary<string, object> data = Extensions.RequestFormDataDictionary(context);
						if(!decimal.TryParse(data["amount"].ToString(), out var amount))
						{
							result = await Error(server, context, "Amount is invalid.");
						}
						else
						{
							await db.BuildAndExecute("BEGIN TRANSACTION;");
							balance.Amount += amount;
							await balance.Save();
							await new BankBalanceRecord(db, balance.Id.Value, amount)
							{
								Note = data["note"].ToString()
							}.Save();
							await db.BuildAndExecute("END TRANSACTION;");
							result = base.Redirect(server, context, string.Format("/bank/view/balance/{0}/{1}", serverId, balance.Id));
						}
					}
				}
			}
			return result;
		}

		// Token: 0x06000170 RID: 368 RVA: 0x00007AD0 File Offset: 0x00005CD0
		[WebApiHandler(HttpVerbs.Get, "^/bank/payment/{serverId}/{id}")]
		public async Task<bool> BankAddPayment(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				if(!_bot.ServerIds.Contains(serverId))
				{
					result = await Error(server, context, "Invalid guild ID.");
				}
				else
				{
					RelationalDatabase db = _bot.RelDatabase(serverId);
					BankBalance bankBalance = await DataTable<BankBalance>.TryLoad(db, id);
					if(bankBalance == null)
					{
						result = await Error(server, context, "Invalid balance ID.");
					}
					else
					{
						BankTransaction bankTransaction = new BankTransaction(db, bankBalance.TransactionId);
						if(bankTransaction.Creator != user.Id)
						{
							result = await ForbiddenError(server, context);
						}
						else
						{
							result = await ViewResponse(server, context, "bank/payment", new
							{
								Title = "Add Payment",
								Balance = bankBalance,
								Transaction = bankTransaction
							});
						}
					}
				}
			}
			return result;
		}

		// Token: 0x06000171 RID: 369 RVA: 0x00007B38 File Offset: 0x00005D38
		[WebApiHandler(HttpVerbs.Get, "^/bank/view/balance/{serverId}/{id}")]
		public async Task<bool> BankViewBalance(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				if(!_bot.ServerIds.Contains(serverId))
				{
					result = await Error(server, context, "Invalid guild ID.");
				}
				else
				{
					RelationalDatabase db = _bot.RelDatabase(serverId);
					BankBalance bankBalance = await DataTable<BankBalance>.TryLoad(db, id);
					BankBalance balance = bankBalance;
					if(balance == null)
					{
						result = await Error(server, context, "Invalid balance ID.");
					}
					else
					{
						BankTransaction transaction = new BankTransaction(db, balance.TransactionId);
						if(!(await transaction.UserCanView(db, user.Id)))
						{
							result = await ForbiddenError(server, context);
						}
						else
						{
							IEnumerable<BankBalanceRecord> records = await BankBalanceRecord.AllForBalance(db, balance);
							BioData bioData = await Bio.GetBio(_bot, serverId, transaction.Creator);
							SocketGuildUser guildUser = _bot.Client.GetGuild(serverId).GetUser(balance.DiscordUserId);
							bool isCreator = user.Id == transaction.Creator;
							bool isDebtor = user.Id == balance.DiscordUserId;
							IEnumerable<BankBalanceRecord> records2 = records;
							string text;
							if(bioData == null)
							{
								text = null;
							}
							else
							{
								BioField bioField = bioData["paypal"];
								text = (bioField?.Value);
							}
							string payPal = text;
							BankBalance balance2 = balance;
							result = await ViewResponse(server, context, "bank/view_balance", new
							{
								Title = "View Balance",
								IsCreator = isCreator,
								IsDebtor = isDebtor,
								Records = records2,
								PayPal = payPal,
								Balance = balance2,
								Nickname = await guildUser.RealNameOrDefault(_bot),
								Name = guildUser.Username,
								Transaction = transaction
							});
						}
					}
				}
			}
			return result;
		}

		// Token: 0x06000172 RID: 370 RVA: 0x00007BA0 File Offset: 0x00005DA0
		[WebApiHandler(HttpVerbs.Get, "^/bank/view/{serverId}/{id}")]
		public async Task<bool> BankViewTransaction(WebServer server, HttpListenerContext context, ulong serverId, long id)
		{
			bool result;
			if(!AuthManager.IsAuthenticated(server, context))
			{
				result = await ForbiddenError(server, context);
			}
			else
			{
				IUser user = AuthManager.GetUser(server, context, _bot);
				if(!_bot.ServerIds.Contains(serverId))
				{
					result = await Error(server, context, "Invalid guild ID.");
				}
				else
				{
					RelationalDatabase db = _bot.RelDatabase(serverId);
					BankTransaction bankTransaction = await DataTable<BankTransaction>.TryLoad(db, id);
					BankTransaction transaction = bankTransaction;
					if(transaction == null)
					{
						result = await Error(server, context, "Invalid transaction ID.");
					}
					else
					{
						IEnumerable<BankBalance> balances = await BankBalance.AllForTransaction(db, transaction);
						if(!balances.Any((BankBalance b) => b.DiscordUserId == user.Id) && user.Id != transaction.Creator)
						{
							result = await ForbiddenError(server, context);
						}
						else
						{
							SocketUser author = _bot.Client.GetUser(transaction.Creator);
							if(author == null)
							{
								result = await Error(server, context, "The creator of this transaction is invalid.");
							}
							else
							{
								SocketGuild guild = _bot.Client.GetGuild(serverId);
								BioData authorBio = await Bio.GetBio(_bot, serverId, author.Id);
								TransactionAuthorModel transactionAuthorModel = new TransactionAuthorModel
								{
									Name = author.Username
								};
								TransactionAuthorModel transactionAuthorModel2 = transactionAuthorModel;
								string nickname = transactionAuthorModel2.Nickname;
								transactionAuthorModel2.Nickname = await guild.GetUser(transaction.Creator).RealNameOrDefault(_bot);
								TransactionAuthorModel transactionAuthorModel3 = transactionAuthorModel;
								BioData bioData = authorBio;
								string payPal;
								if(bioData == null)
								{
									payPal = null;
								}
								else
								{
									BioField bioField = bioData["paypal"];
									payPal = (bioField?.Value);
								}
								transactionAuthorModel3.PayPal = payPal;
								TransactionAuthorModel creator = transactionAuthorModel;
								transactionAuthorModel2 = null;
								transactionAuthorModel = null;
								IEnumerable<TransactionBalanceModel> balances2 = balances.Select(delegate (BankBalance b)
								{
									TransactionBalanceModel transactionBalanceModel = new TransactionBalanceModel
									{
										Id = b.Id.Value
									};
									SocketGuildUser balanceUser = guild.GetUser(b.DiscordUserId);
									transactionBalanceModel.Author = (balanceUser?.RealNameOrDefaultSync(_bot)) ?? ("unknown user id " + b.DiscordUserId);
									transactionBalanceModel.Amount = -b.Amount;
									transactionBalanceModel.IsSettled = b.Settled;
									return transactionBalanceModel;
								});
								result = await ViewResponse(server, context, "bank/view", new
								{
									Title = "View Transaction",
									Transaction = transaction,
									Creator = creator,
									Balances = balances2,
									IsCreator = (user.Id == transaction.Creator)
								});
							}
						}
					}
				}
			}
			return result;
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

		private DiscordBot _bot;
		private Logger _logger;

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
