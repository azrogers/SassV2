using Discord;
using Discord.WebSocket;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SassV2
{
	public class DiscordBot
	{
#if DEBUG
		public const string BotName = "!sass";
#else
		public const string BotName = "sass";
#endif

		private DiscordSocketClient _client;
		private Logger _logger;
		private Config _config;
		private CommandHandler _commandHandler;
		private RelationalDatabase _globalDatabase;
		private Dictionary<ulong, KeyValueDatabase> _serverDatabases;
		private Dictionary<ulong, RelationalDatabase> _serverRelationalDatabases;
		private Dictionary<ulong, Responder> _serverResponders;
		//private Dictionary<string, LispSandbox.LispAction> _responderFilters;

		public Config Config => _config;
		public CommandHandler CommandHandler => _commandHandler;
		public IDiscordClient Client => _client;
		public List<ulong> ServerIds => _serverDatabases.Keys.ToList();
		public RelationalDatabase GlobalDatabase => _globalDatabase;

		public static Logger StaticLogger;

		public DiscordBot(Config config)
		{
			StaticLogger = _logger = LogManager.GetCurrentClassLogger();
			_client = new DiscordSocketClient();
			_config = config;
			_commandHandler = new CommandHandler();
			_serverDatabases = new Dictionary<ulong, KeyValueDatabase>();
			_serverRelationalDatabases = new Dictionary<ulong, RelationalDatabase>();
			_serverResponders = new Dictionary<ulong, Responder>();
			if(!Directory.Exists("Servers"))
			{
				Directory.CreateDirectory("Servers");
			}
		}

		public async Task Start()
		{
			_logger.Info("starting bot");
			_globalDatabase = new RelationalDatabase("global.db");
			await _globalDatabase.Open();

			_client.Log += (m) => Task.Run(() =>
			{
				_logger.Log(Util.SeverityToLevel(m.Severity), m.Message);
			});
			_client.GuildAvailable += OnGuildAvailable;
			_client.MessageReceived += OnMessageReceived;
			_client.Ready += () => Task.Run(() => _logger.Info("client ready"));

			await _client.LoginAsync(TokenType.Bot, _config.Token);
			await _client.StartAsync();
			await Task.Delay(-1);
		}

		private async Task OnGuildAvailable(SocketGuild guild)
		{
			var dbPath = Path.Combine("Servers", guild.Id.ToString());
			if(!Directory.Exists(dbPath))
			{
				Directory.CreateDirectory(dbPath);
			}

			_serverDatabases[guild.Id] = new KeyValueDatabase(Path.Combine(dbPath, "server2.db"));
			await _serverDatabases[guild.Id].Open();

			_serverRelationalDatabases[guild.Id] = new RelationalDatabase(Path.Combine(dbPath, "relational.db"));
			await _serverRelationalDatabases[guild.Id].Open();

			_serverResponders[guild.Id] = new Responder();

			foreach(var kv in _serverDatabases[guild.Id].GetKeysOfNamespace<string>("filter"))
			{
				//_responderFilters[kv.Key.Substring(0, "filter:".Length)] = SassLisp.Compile(kv.Value);
			}

			_logger.Info("joined " + guild.Name + " (" + guild.Id + ")");
		}

		/// <summary>
		/// Returns a database for the given server ID.
		/// </summary>
		/// <param name="serverId">The server ID.</param>
		/// <returns>The database for the given server.</returns>
		public KeyValueDatabase Database(ulong serverId)
		{
			return _serverDatabases[serverId];
		}

		public RelationalDatabase RelDatabase(ulong serverId)
		{
			return _serverRelationalDatabases[serverId];
		}

		public Responder Responder(ulong serverId)
		{
			return _serverResponders[serverId];
		}

		public void AddResponderFilter(string name, string filter)
		{
			//_responderFilters[name] = SassLisp.Compile(filter);
		}

		public IEnumerable<IGuild> GuildsContainingUser(IUser user)
		{
			return _client.Guilds.Where(g => g.Users.Any(s => s.Id == user.Id));
		}

		private async Task OnMessageReceived(SocketMessage message)
		{
			// ignore messages we send
			if(message.Author == _client.CurrentUser) return;

			_logger.Debug("message from " + message.Author.Username + ": " + message.Content);

			// if they mention sass, send a rude message
			if(message.MentionedUsers.Contains(_client.CurrentUser))
			{
				await SendMessage(message.Channel, Util.AssembleRudeMessage());
				return;
			}
			
			// check if the command starts with the bot name
			if(!message.Content.StartsWith(BotName, StringComparison.CurrentCultureIgnoreCase))
			{
				return;
			}

			var guild = (message.Channel as SocketGuildChannel)?.Guild;

			// check if the user is banned
			bool banned = (message.Channel is ISocketPrivateChannel ? false : Database(guild.Id).GetObject<bool>("ban:" + message.Author.Id));

			var permissions = (message.Author as SocketGuildUser)?.GuildPermissions;
			if(banned && (!permissions.HasValue || !permissions.Value.Administrator) && Config.GetRole(message.Author.Id) != "admin")
			{
				await SendMessage(message.Channel as ISocketMessageChannel, Util.Locale("error.banned"));
				return;
			}

			// find the command
			var command = _commandHandler.FindCommand(message.Content.Substring(BotName.Length).Trim(), message.Channel is ISocketPrivateChannel);
			if(command != null)
			{
				try
				{
					string result;
					if(command.IsAsync)
					{
						result = await command.AsyncDelegate(this, message, command.Arguments);
					}
					else
					{
						result = command.Delegate(this, message, command.Arguments);
					}

					if(string.IsNullOrWhiteSpace(result))
					{
						return;
					}

					await SendMessage(message.Channel, result);
				}
				// a command exception is one that the user should know about
				catch(CommandException commandEx)
				{
					await SendMessage(message.Channel, commandEx.Message);
					return;
				}
				// this is an exception the user shouldn't know about
				catch(Exception ex)
				{
					await SendMessage(message.Channel, Util.AssembleRudeErrorMessage());
					_logger.Error(ex);
					return;
				}
			}
			else
			{
				await SendMessage(message.Channel, Util.Locale("error.noCommand"));
			}
		}

		private Task SendMessage(ISocketMessageChannel channel, string message)
        {
            _logger.Debug("sent message on " + channel.Name + ": " + message);
			return channel.SendMessageAsync(message);
        }

        private Task ReplyToPM(SocketMessage pm, string message)
        {
            _logger.Debug("sent pm to " + pm.Author.Username + ": " + message);
			return pm.Channel.SendMessageAsync(message);
        }
	}
}
