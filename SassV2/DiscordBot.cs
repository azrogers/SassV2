﻿using Discord;
using Discord.WebSocket;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace SassV2
{
	public class DiscordBot
	{
		private DiscordSocketClient _client;
		private Logger _logger;
		private Config _config;
		private CommandHandler _commandHandler;
		private RelationalDatabase _globalDatabase;
		private Dictionary<ulong, KeyValueDatabase> _serverDatabases;
		private Dictionary<ulong, RelationalDatabase> _serverRelationalDatabases;
		private Dictionary<ulong, Responder> _serverResponders;
		private ServiceProvider _services;
		//private Dictionary<string, LispSandbox.LispAction> _responderFilters;

		public Config Config => _config;
		public CommandHandler CommandHandler => _commandHandler;
		public DiscordSocketClient Client => _client;
		public List<ulong> ServerIds => _serverDatabases.Keys.ToList();
		public RelationalDatabase GlobalDatabase => _globalDatabase;

		public static Logger StaticLogger;

		public DiscordBot(Config config)
		{
			StaticLogger = _logger = LogManager.GetCurrentClassLogger();
			var discordConfig = new DiscordSocketConfig();
			discordConfig.LogLevel = LogSeverity.Verbose;
			
			_client = new DiscordSocketClient(discordConfig);
			_config = config;
			_commandHandler = new CommandHandler();
			_serverDatabases = new Dictionary<ulong, KeyValueDatabase>();
			_serverRelationalDatabases = new Dictionary<ulong, RelationalDatabase>();
			_serverResponders = new Dictionary<ulong, Responder>();
			if(!Directory.Exists("Servers"))
			{
				Directory.CreateDirectory("Servers");
			}

			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton(this);

			_services = serviceCollection.BuildServiceProvider();
		}

		public async Task Start()
		{
			_logger.Info("starting bot");
			_globalDatabase = new RelationalDatabase("global.db");
			await _globalDatabase.Open();

			await _commandHandler.InitCommands();
			
			_client.Log += (m) => Task.Run(() =>
			{
				_logger.Log(Util.SeverityToLevel(m.Severity), m.Message);
			});
			_client.GuildAvailable += OnGuildAvailable;
			_client.MessageReceived += OnMessageReceived;
			_client.UserJoined += UserJoined;
			_client.Disconnected += Disconnected;
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
			if (message.Author.Id == _client.CurrentUser.Id) return;

			if(message.Channel is ISocketPrivateChannel)
			{
				_logger.Debug("message from " + message.Author.Username + ": " + message.Content);
			}
			else if(_config.DebugServers.Contains((message.Channel as IGuildChannel).GuildId.ToString()))
			{
				var channel = (message.Channel as IGuildChannel);
				_logger.Debug($"message on #{channel.Name} ({channel.Guild.Name}) from {message.Author.Username}: {message.Content}");
			}

			// if they mention sass, send a rude message
			if(message.MentionedUsers.Contains(_client.CurrentUser))
			{
				await SendMessage(message.Channel, Util.AssembleRudeMessage());
				return;
			}

			if(message.Content.Trim().Equals(CommandHandler.BOT_NAME, StringComparison.CurrentCultureIgnoreCase))
			{
				SendMessage(message.Channel, "Give me a command. If you don't have one, try `sass help`.").Forget();
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
			
			try
			{
				await _commandHandler.HandleCommand(_services, message);
			}
			// a command exception is one that the user should know about
			catch(CommandException commandEx)
			{
				await SendMessage(message.Channel, commandEx.Message);
				return;
			}
			catch(AggregateException ex)
			{
				if (ex.InnerExceptions.Any(a => a.GetType() == typeof(Discord.Net.RateLimitedException)))
					SendMessage(message.Channel, $"I'm being rate limited!").Forget();
				if(ex.InnerExceptions.Any(a => a.GetType() == typeof(CommandException)))
				{
					foreach (var err in ex.InnerExceptions.Where(a => a.GetType() == typeof(CommandException)))
						SendMessage(message.Channel, err.Message).Forget();
					return;
				}
				await SendMessage(message.Channel, Util.AssembleRudeErrorMessage());
				_logger.Error(ex);
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

		private Task Disconnected(Exception arg)
		{
			return Task.Run(() =>
			{
				if (arg != null)
					_logger.Error(arg);
				_logger.Warn("Rebooting");
				Environment.Exit(1);
			});
		}

		private async Task UserJoined(SocketUser user)
		{
			var guild = (user as IGuildUser).Guild;
			var welcome = Database(guild.Id).GetObject<string>("welcome");
			if (welcome == default(string))
				return;
			var welcomeChannel = _client.GetChannel(Database(guild.Id).GetObject<ulong>("welcome_channel"));

			await SendMessage(welcomeChannel as ISocketMessageChannel, Util.FormatString(welcome, new
			{
				username = (user as IGuildUser).NicknameOrDefault(),
				mention = user.Mention
			}));
		}

		private Task SendMessage(ISocketMessageChannel channel, string message)
        {
			_logger.Info("sent message");
			return channel.SendMessageAsync(message);
        }

        private Task ReplyToPM(SocketMessage pm, string message)
        {
            _logger.Debug("sent pm to " + pm.Author.Username + ": " + message);
			return pm.Channel.SendMessageAsync(message);
        }
	}
}
