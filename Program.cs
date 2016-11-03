using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.Commands.Permissions.Levels;
using Discord.Modules;
using System.Collections.Concurrent;

namespace MagBot
{
    class Program
    {
        public static void Main() => new Program().Start();
        
        private static DiscordClient client;
        public static ConcurrentDictionary<ulong, SortedDictionary<string, List<string>>> taglist;
        public static ConcurrentDictionary<ulong, bool> annolist;
        public static ConcurrentDictionary<ulong, ulong> annochlist;
        public static ConcurrentDictionary<string, long> karmalist;
        public static ConcurrentDictionary<ulong, int> karmacooldown;
        public static ConcurrentDictionary<ulong, List<string>> enabledmodules;
        public static ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ulong>> messagecount;
        public static ConcurrentDictionary<ulong, List<ulong>> excludedchannels;
        private static Timer savetimer;
        private static Timer karmacooldowntimer;
        public static bool invitesallowed;
        public static TimeSpan startedat;

        public void Start()
        {
            Console.WriteLine("Configuring client...");
            client = new DiscordClient(x =>
            {
                x.LogLevel = LogSeverity.Info;
                x.ConnectionTimeout = 60000;
                x.ReconnectDelay = 1000;
                x.FailedReconnectDelay = 60000;
                x.MessageCacheSize = 10;
            })
            .UsingCommands(x =>
            {
                x.PrefixChar = '!';
                x.HelpMode = HelpMode.Public;
                x.ErrorHandler = CommandError;
                x.ExecuteHandler += (s, e) => client.Log.Info("Command", $"[{((e.Server != null) ? e.Server.Name : "Private")}{((!e.Channel.IsPrivate) ? $"/#{e.Channel.Name}" : "")}] <@{e.User.Name}> {e.Command.Text} {((e.Args.Length > 0) ? "| " + string.Join(" ", e.Args) : "")}");
            })
            .UsingPermissionLevels((u, c) => (int)GetPermissions(u, c))
            .UsingModules();

            client.Log.Message += (s, e) => WriteLog(e);

            karmacooldown = new ConcurrentDictionary<ulong,int>();
            startedat = TimeSpan.FromTicks(DateTime.Now.Ticks);

            // Load files
            Load();
            
            client.Log.Log(LogSeverity.Info, "Mag-Bot", "Defining events...");

            // Connected to Discord
            client.Ready += (sender, e) =>
            {
                client.Log.Log(LogSeverity.Info, "Mag-Bot", $"User: {client.CurrentUser.Name}");
                client.SetGame("with magic!");
            };

            // Announcement for user joining
            client.UserJoined += (async (sender, e) =>
                {
                    bool on = false;
                    if(annolist.TryGetValue(e.Server.Id, out on))
                    {
                        if (on)
                        {
                            ulong cid = e.Server.DefaultChannel.Id;
                            annochlist.TryGetValue(e.Server.Id, out cid);
                            var ch = e.Server.GetChannel(cid);
                            await ch.SendMessage(string.Format("User **{0}** has joined the server!", e.User.Name));
                        }
                    }

                    ulong id = e.User.Id;
                    if (id == 190321880198703488 || id == 136257664231104448)
                    {
                        await e.Server.Ban(e.Server.GetUser(id));
                    }
                });

            // Announcement for user leaving
            client.UserLeft += (async (sender, e) =>
            {
                bool on = false;
                if (annolist.TryGetValue(e.Server.Id, out on))
                {
                    if (on)
                    {
                        ulong cid = e.Server.DefaultChannel.Id;
                        annochlist.TryGetValue(e.Server.Id, out cid);
                        var ch = e.Server.GetChannel(cid);
                        await ch.SendMessage(string.Format("User **{0}** has left the server!", e.User.Name));
                    }
                }
            });

            // Announcement for user banned
            client.UserBanned += (async (sender, e) =>
            {
                bool on = false;
                if (annolist.TryGetValue(e.Server.Id, out on))
                {
                    if (on)
                    {
                        ulong cid = e.Server.DefaultChannel.Id;
                        annochlist.TryGetValue(e.Server.Id, out cid);
                        var ch = e.Server.GetChannel(cid);
                        await ch.SendMessage(string.Format("User **{0}** was banned the server!", e.User.Name));
                    }
                }
            });

            // Bot added to server
            client.JoinedServer += (async (s, e) =>
                {
                    Server serv = client.GetServer(165284271928377345);
                    User mag = serv.GetUser(156238211067150336);
                    await mag.SendMessage(string.Format("Mag-Bot has been added to server {0} (ID: {1}) at {2}", e.Server.Name, e.Server.Id, DateTime.Now));
                });


            client.Log.Log(LogSeverity.Info, "Mag-Bot", "Loading modules...");
            client.AddModule<OwnerModule>("Owner", ModuleFilter.None);
            client.AddModule<GeneralModule>("General", ModuleFilter.None);
            client.AddModule<PonyModule>("Pony", ModuleFilter.ServerWhitelist);
            client.AddModule<ModuleManagerExtended>("Modules", ModuleFilter.None);
            client.AddModule<Search>("Search", ModuleFilter.ServerWhitelist);
            client.AddModule<FurryModule>("Fur", ModuleFilter.ServerWhitelist);
            client.AddModule<RankModule>("Ranks", ModuleFilter.ServerWhitelist);
            client.AddModule<SilvRankModule>("Silv Ranks", ModuleFilter.ServerWhitelist);
            client.AddModule<RaffleModule>("Raffle", ModuleFilter.ServerWhitelist);

            try
            {

                client.Log.Log(LogSeverity.Info, "Mag-Bot", "Connecting client...");
                //client.ExecuteAndWait(async () =>
                //{
                Task.Run(async () =>
                    {
                        await client.Connect(File.ReadAllText("Token.txt"), TokenType.Bot);
                    });
                    
                //});
            }
            catch (Exception e)
            {
                Console.WriteLine("Error occured while connecting client.\n" + e.Message);
                Console.ReadLine();
            }

            savetimer = new Timer(e =>
            {
                Save();
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            karmacooldowntimer = new Timer(e =>
            {
                List<ulong> keys = new List<ulong>(karmacooldown.Keys);
                foreach (var user in keys)
                {
                    if (karmacooldown[user] > 0)
                    {
                        karmacooldown[user] -= 5;
                    }
                }
            }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            while(true)
            {

                string input = Console.ReadLine();
                if (input == "shutdown")
                {
                    if (RaffleModule.RaffleRunning())
                    {
                        client.Log.Log(LogSeverity.Warning, "Mag-Bot", "Raffle running, cannot shut down.");
                        continue;
                    }
                    Shutdown();
                }
                else if (input == "restart")
                {
                    if (RaffleModule.RaffleRunning())
                    {
                        client.Log.Log(LogSeverity.Warning, "Mag-Bot", "Raffle running, cannot restart.");
                        continue;
                    }
                    Restart();
                }
                else if (input == "save")
                {
                    Save();
                }
                else if (input == "reload")
                {
                    Load();
                }
                else if (input == "forceshutdown")
                {
                    Shutdown();
                }
                else if (input == "forcerestart")
                {
                    Restart();
                }
                else
                {
                    Console.WriteLine("Invalid command.");
                }
            }
        }


        public static void Shutdown()
        {
            client.Log.Log(LogSeverity.Info, "Mag-Bot", "Recieved shutdown command.");
            Save();
            savetimer.Dispose();
            Thread.Sleep(2000);
            client.Log.Log(LogSeverity.Info, "Mag-Bot", "Disconnecting client...");
            client.Disconnect();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(0);
        }

        public static void Restart()
        {
            client.Log.Log(LogSeverity.Info, "Mag-Bot", "Recieved restart command.");
            Save();
            savetimer.Dispose();
            Thread.Sleep(2000);
            client.Log.Log(LogSeverity.Info, "Mag-Bot", "Disconnecting client...");
            client.Disconnect();
            Main();
        }

        public static void Save()
        {
            client.Log.Log(LogSeverity.Info, "Mag-Bot", "Saving files...");
            File.WriteAllText("resources/data/taglist.txt", JsonConvert.SerializeObject(taglist, Formatting.Indented));
            File.WriteAllText("resources/data/annolist.txt", JsonConvert.SerializeObject(annolist, Formatting.Indented));
            File.WriteAllText("resources/data/annochlist.txt", JsonConvert.SerializeObject(annochlist, Formatting.Indented));
            File.WriteAllText("resources/data/karmalist.txt", JsonConvert.SerializeObject(karmalist, Formatting.Indented));
            File.WriteAllText("resources/data/enabledmodules.txt", JsonConvert.SerializeObject(enabledmodules, Formatting.Indented));
            File.WriteAllText("resources/data/messagecount.txt", JsonConvert.SerializeObject(messagecount, Formatting.Indented));
            File.WriteAllText("resources/data/excludedchannels.txt", JsonConvert.SerializeObject(excludedchannels, Formatting.Indented));
            TextWriter tw = new StreamWriter("resources/data/savedvars.txt");
            tw.WriteLine(invitesallowed);
            tw.Close();
        }

        public static void Load()
        {
            client.Log.Log(LogSeverity.Info, "Mag-Bot", "Loading files...");
            if (!Directory.Exists("resources/data")) Directory.CreateDirectory("resources/data");
            if (!File.Exists("resources/data/taglist.txt")) File.Create("resources/data/taglist.txt");
            if (!File.Exists("resources/data/annolist.txt")) File.Create("resources/data/annolist.txt");
            if (!File.Exists("resources/data/annochlist.txt")) File.Create("resources/data/annochlist.txt");
            if (!File.Exists("resources/data/karmalist.txt")) File.Create("resources/data/karmalist.txt");
            if (!File.Exists("resources/data/enabledmodules.txt")) File.Create("resources/data/enabledmodules.txt");
            if (!File.Exists("resources/data/messagecount.txt")) File.Create("resources/data/messagecount.txt");
            if (!File.Exists("resources/data/excludedchannels.txt")) File.Create("resources/data/excludedchannels.txt");
            if (!File.Exists("resources/data/savedvars.txt")) File.Create("resources/data/savedvars.txt");

            taglist = JsonConvert.DeserializeObject<ConcurrentDictionary<ulong, SortedDictionary<string, List<string>>>>(File.ReadAllText("resources/data/taglist.txt"));
            annolist = JsonConvert.DeserializeObject<ConcurrentDictionary<ulong, bool>>(File.ReadAllText("resources/data/annolist.txt"));
            annochlist = JsonConvert.DeserializeObject<ConcurrentDictionary<ulong, ulong>>(File.ReadAllText("resources/data/annochlist.txt"));
            karmalist = JsonConvert.DeserializeObject<ConcurrentDictionary<string, long>>(File.ReadAllText("resources/data/karmalist.txt"));
            enabledmodules = JsonConvert.DeserializeObject<ConcurrentDictionary<ulong, List<string>>>(File.ReadAllText("resources/data/enabledmodules.txt"));
            messagecount = JsonConvert.DeserializeObject<ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ulong>>>(File.ReadAllText("resources/data/messagecount.txt"));
            excludedchannels = JsonConvert.DeserializeObject<ConcurrentDictionary<ulong, List<ulong>>>(File.ReadAllText("resources/data/excludedchannels.txt"));
            TextReader tr = new StreamReader("resources/data/savedvars.txt");
            invitesallowed = Convert.ToBoolean(tr.ReadLine());
            tr.Close();
        }

        private static PermissionLevel GetPermissions(User u, Channel c)
        {
            if (u.Id == 156238211067150336) // Replace this with your own UserId
                return PermissionLevel.BotOwner;

            if (u.IsBot) // Customize this to your liking to ignore other stuff, like a list of known spammers.
            {
                return PermissionLevel.Ignored;
            }

            if (!c.IsPrivate)
            {
                if (u == c.Server.Owner)
                    return PermissionLevel.ServerOwner;

                var serverPerms = u.ServerPermissions;
                if (serverPerms.ManageRoles)
                    return PermissionLevel.ServerAdmin;
                if (serverPerms.ManageMessages && serverPerms.KickMembers && serverPerms.BanMembers)
                    return PermissionLevel.ServerModerator;

                var channelPerms = u.GetPermissions(c);
                if (channelPerms.ManagePermissions)
                    return PermissionLevel.ChannelAdmin;
                if (channelPerms.ManageMessages)
                    return PermissionLevel.ChannelModerator;
            }
            return PermissionLevel.User;
        }

        private async void CommandError(object sender, CommandErrorEventArgs e)
        {
            if (e.ErrorType == CommandErrorType.Exception)
            {
                client.Log.Error("Command", e.Exception);
                await e.Channel.SendMessage($"Error: {e.Exception.GetBaseException().Message}");
            }
            else if (e.ErrorType == CommandErrorType.BadPermissions)
            {
                if (e.Exception?.Message == "This module is currently disabled.")
                {
                    await e.Channel.SendMessage($"The `{e.Command?.Category}` module is currently disabled.");
                    return;
                }
                else if (e.Exception != null)
                {
                    await e.Channel.SendMessage(e.Exception.Message);
                    return;
                }

                if (e.Command?.IsHidden == true)
                    return;

                await e.Channel.SendMessage($"You don't have permission to access that command!");
            }
            else if (e.ErrorType == CommandErrorType.BadArgCount)
            {
                await e.Channel.SendMessage("Error: Invalid parameter count.");
            }
            else if (e.ErrorType == CommandErrorType.InvalidInput)
            {
                await e.Channel.SendMessage("Error: Invalid input! Make sure your quotes match up correctly!");
            }
            else if (e.ErrorType == CommandErrorType.UnknownCommand)
            {
                // Only set up a response in here if you stick with a mention prefix
            }
        }

        private void WriteLog(LogMessageEventArgs e)
        {
            //Color
            ConsoleColor color;
            switch (e.Severity)
            {
                case LogSeverity.Error: color = ConsoleColor.Red; break;
                case LogSeverity.Warning: color = ConsoleColor.Yellow; break;
                case LogSeverity.Info: color = ConsoleColor.White; break;
                case LogSeverity.Verbose: color = ConsoleColor.Gray; break;
                case LogSeverity.Debug: default: color = ConsoleColor.DarkGray; break;
            }

            //Exception
            string exMessage;
            Exception ex = e.Exception;
            if (ex != null)
            {
                while (ex is AggregateException && ex.InnerException != null)
                    ex = ex.InnerException;
                exMessage = $"{ex.Message}";
                if (exMessage != "Reconnect failed: HTTP/1.1 503 Service Unavailable")
                    exMessage += $"\n{ex.StackTrace}";
            }
            else
                exMessage = null;

            //Source
            string sourceName = e.Source?.ToString();

            //Text
            string text;
            if (e.Message == null)
            {
                text = exMessage ?? "";
                exMessage = null;
            }
            else
                text = e.Message;

            if (sourceName == "Command")
                color = ConsoleColor.Cyan;
            else if (sourceName == "<<Message")
                color = ConsoleColor.Green;

            //Build message
            StringBuilder builder = new StringBuilder(text.Length + (sourceName?.Length ?? 0) + (exMessage?.Length ?? 0) + 5);
            if (sourceName != null)
            {
                builder.Append('[');
                builder.Append(sourceName);
                builder.Append("] ");
            }
            builder.Append($"[{DateTime.Now.ToShortDateString()} {DateTime.Now.ToLongTimeString()}] ");
            for (int i = 0; i < text.Length; i++)
            {
                //Strip control chars
                char c = text[i];
                if (c == '\n' || !char.IsControl(c) || c != (char)8226) // (char)8226 beeps like \a, this catches that
                    builder.Append(c);
            }
            if (exMessage != null)
            {
                builder.Append(": ");
                builder.Append(exMessage);
            }

            text = builder.ToString();
            if (e.Severity <= LogSeverity.Info)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
            }
#if DEBUG
            System.Diagnostics.Debug.WriteLine(text);
#endif
        }
    }
}
