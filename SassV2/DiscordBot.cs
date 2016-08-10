using Discord;
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

		private DiscordClient _client;
		private Logger _logger;
		private Config _config;
		private CommandHandler _commandHandler;
		private Dictionary<ulong, KeyValueDatabase> _serverDatabases;
		private Dictionary<ulong, RelationalDatabase> _serverRelationalDatabases;

		public Config Config => _config;
		public CommandHandler CommandHandler => _commandHandler;
		public DiscordClient Client => _client;
		public List<ulong> ServerIds => _serverDatabases.Keys.ToList();

		public DiscordBot(Config config)
		{
			_logger = LogManager.GetCurrentClassLogger();
			_client = new DiscordClient();
			_config = config;
			_commandHandler = new CommandHandler();
			_serverDatabases = new Dictionary<ulong, KeyValueDatabase>();
			_serverRelationalDatabases = new Dictionary<ulong, RelationalDatabase>();
			if(!Directory.Exists("Servers"))
			{
				Directory.CreateDirectory("Servers");
			}
		}

		public void Start()
		{
			_logger.Info("starting bot");

			_client.ServerAvailable += OnServerAvailable;
			_client.MessageReceived += OnMessageReceived;
			_client.Ready += (s, e) => {
				_logger.Info("client ready");
			};

			_client.ExecuteAndWait(async () =>
			{
				await _client.Connect(_config.Token);
			});
		}

		private void OnServerAvailable(object sender, ServerEventArgs e)
		{
			var dbPath = Path.Combine("Servers", e.Server.Id.ToString());
			if(!Directory.Exists(dbPath))
			{
				Directory.CreateDirectory(dbPath);
			}
			_serverDatabases[e.Server.Id] = new KeyValueDatabase(Path.Combine(dbPath, "server2.db"));
			_serverDatabases[e.Server.Id].Open();

			_serverRelationalDatabases[e.Server.Id] = new RelationalDatabase(Path.Combine(dbPath, "relational.db"));
			_serverRelationalDatabases[e.Server.Id].Open();

			_logger.Info("joined " + e.Server.Name + " (" + e.Server.Id + ")");
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

		private async void OnMessageReceived(object sender, MessageEventArgs e)
		{
			// ignore messages we send
			if(e.Message.IsAuthor) return;

			_logger.Debug("message from " + e.Message.User.Name + ": " + e.Message.Text);

			// if they mention sass, send a rude message
			if(e.Message.IsMentioningMe())
			{
				await SendMessage(e.Channel, Util.AssembleRudeMessage());
				return;
			}
			
			// check if the command starts with the bot name
			if(!e.Message.Text.StartsWith(BotName, StringComparison.CurrentCultureIgnoreCase))
			{
				return;
			}

			// check if the user is banned
			bool banned = (e.Channel.IsPrivate ? false : Database(e.Server.Id).GetObject<bool>("ban:" + e.User.Id));

			if(banned && !e.User.ServerPermissions.Administrator && Config.GetRole(e.User.Id) != "admin")
			{
				await SendMessage(e.Channel, Util.Locale("error.banned"));
				return;
			}

			// find the command
			var commandMaybe = _commandHandler.FindCommand(e.Message.Text.Substring(BotName.Length).Trim(), e.Message.Channel.IsPrivate);
			if(commandMaybe.HasValue)
			{
				var command = commandMaybe.Value;
				try
				{
					string result;
					if(command.IsAsync)
					{
						result = await command.AsyncDelegate(this, e.Message, command.Arguments);
					}
					else
					{
						result = command.Delegate(this, e.Message, command.Arguments);
					}

					if(string.IsNullOrWhiteSpace(result))
					{
						return;
					}

					await SendMessage(e.Channel, result);
				}
				// a command exception is one that the user should know about
				catch(CommandException commandEx)
				{
					await SendMessage(e.Channel, commandEx.Message);
					return;
				}
#if !DEBUG
				// this is an exception the user shouldn't know about
				catch(Exception ex)
				{
					await SendMessage(e.Channel, Util.AssembleRudeErrorMessage());
					_logger.Error(ex);
					return;
				}
#endif
			}
			else
			{
				await SendMessage(e.Channel, Util.Locale("error.noCommand"));
			}
		}

		private Task<Message> SendMessage(Channel channel, string message)
        {
            _logger.Debug("sent message on " + channel.Name + ": " + message);
            return channel.SendMessage(message);
        }

        private Task<Message> ReplyToPM(Message pm, string message)
        {
            _logger.Debug("sent pm to " + pm.User.Name + ": " + message);
            return pm.User.SendMessage(message);
        }
	}
}
