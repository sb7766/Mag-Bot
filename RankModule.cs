using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Levels;
using Discord.Modules;
using System.Collections.Concurrent;
using System.Threading;

namespace MagBot
{
    class RankModule : IModule
    {
        private DiscordClient client;
        private ModuleManager manager;
        private TimeSpan rank1time;
        private TimeSpan rank2time;
        private TimeSpan rank3time;

        void IModule.Install(ModuleManager _manager)
        {
            manager = _manager;
            client = manager.Client;

            rank1time = TimeSpan.FromDays(7);
            rank2time = TimeSpan.FromDays(14);
            rank3time = TimeSpan.FromDays(30);

            manager.CreateCommands ("", cgb =>
            {
                cgb.MinPermissions((int)PermissionLevel.User);

                cgb.CreateGroup ("rank", g =>
                {
                    g.CreateCommand("")
                    .Description("Shows your current rank and how far away you are from the next rank.")
                    .Do (async e =>
                    {
                        Role rank1 = e.Server.FindRoles("Regular", true).First();
                        Role rank2 = e.Server.FindRoles("Veteran", true).First();
                        Role rank3 = e.Server.FindRoles("Founder", true).First();
                        if (e.User.HasRole(rank3))
                        {
                            await e.Channel.SendMessage("You have the highest rank, Founder.");
                        }
                        else if (e.User.HasRole(rank2))
                        {
                            await NextRank(e, "rank Veteran", "Founder");
                        }
                        else if (e.User.HasRole(rank1))
                        {
                            await NextRank(e, "rank Regular", "Veteran");
                        }
                        else
                        {
                            await NextRank(e, "no rank", "Regular");
                        }
                    });

                    g.CreateCommand("exclude")
                    .Description("Exclude a channel from the message counting system. Use a channel mention.")
                    .MinPermissions((int)PermissionLevel.ServerAdmin)
                    .Parameter("channel", ParameterType.Required)
                    .Do(async e =>
                   {
                       Channel ch = e.Message.MentionedChannels.First();
                       List<ulong> ex = new List<ulong>();
                       ex = Program.excludedchannels.GetOrAdd(e.Server.Id, ex);
                       if (ex.Contains(ch.Id))
                       {
                           await e.Channel.SendMessage($"Channel {ch.Mention} already in exclusion list.");
                       }
                       else
                       {
                           ex.Add(ch.Id);
                           Program.excludedchannels[e.Server.Id] = ex;
                           await e.Channel.SendMessage($"Channel {ch.Mention} added to exclusion list.");
                       }
                   });

                    g.CreateCommand("include")
                    .Description("Include a channel in the message counting system. Use a channel mention.")
                    .MinPermissions((int)PermissionLevel.ServerAdmin)
                    .Parameter("channel", ParameterType.Required)
                    .Do(async e =>
                    {
                        Channel ch = e.Message.MentionedChannels.First();
                        List<ulong> ex = new List<ulong>();
                        ex = Program.excludedchannels.GetOrAdd(e.Server.Id, ex);
                        if(ex.Contains(ch.Id))
                        {
                            ex.Remove(ch.Id);
                            Program.excludedchannels[e.Server.Id] = ex;
                            await e.Channel.SendMessage($"Channel {ch.Mention} removed from exclusion list.");
                        }
                        else
                        {
                            await e.Channel.SendMessage($"Channel {ch.Mention} not in exclusion list.");
                        }
                    });

                    g.CreateCommand("excluded")
                    .Description("List the currently excluded channnels.")
                    .MinPermissions((int)PermissionLevel.ServerAdmin)
                    .Do(async e =>
                    {
                        List<ulong> ex = new List<ulong>();
                        if (!Program.excludedchannels.TryGetValue(e.Server.Id, out ex) || ex.Count <= 0)
                        {
                            await e.Channel.SendMessage("No channels excluded.");
                        }
                        else
                        {
                            List<string> channels = new List<string>();
                            foreach (var chid in ex)
                            {
                                Channel ch = e.Server.GetChannel(chid);
                                channels.Add(ch.Mention);
                            }
                            string message = "The following channels are excluded: " + string.Join(", ", channels);
                            await e.Channel.SendMessage(message);
                        }
                        
                    });
                });
            });

            client.ServerAvailable += (async (s, e) =>
            {
                List<string> modules = new List<string>();
                modules = Program.enabledmodules.GetOrAdd(e.Server.Id, modules);
                if (modules.Contains("ranks"))
                {
                    await ServerInit(e.Server);
                }
            });

            client.MessageReceived += (async (s, e) =>
            {
                if (!e.Channel.IsPrivate && Program.enabledmodules[e.Server.Id].Contains("ranks"))
                {
                    List<ulong> exch = new List<ulong>();
                    exch = Program.excludedchannels.GetOrAdd(e.Server.Id, exch);
                    if (!exch.Contains(e.Channel.Id))
                    {
                        ConcurrentDictionary<ulong, ulong> serv = new ConcurrentDictionary<ulong, ulong>();
                        serv = Program.messagecount.GetOrAdd(e.Server.Id, serv);
                        ulong value = 0;
                        value = serv.GetOrAdd(e.User.Id, value);
                        value++;
                        Program.messagecount[e.Server.Id][e.User.Id] = value;
                        Role rank1 = e.Server.FindRoles("Regular", true).First();
                        Role rank2 = e.Server.FindRoles("Veteran", true).First();
                        Role rank3 = e.Server.FindRoles("Founder", true).First();
                        if (value >= 100 && !e.User.HasRole(rank1) && TimeReqMet(e.User, rank1time) && !e.User.HasRole(rank2) && !e.User.HasRole(rank3))
                        {
                            await e.User.AddRoles(rank1);
                            await e.User.SendMessage($"You have been promoted to Regular in server: {e.Server.Name}");
                        }
                        if (value >= 150 && !e.User.HasRole(rank2) && TimeReqMet(e.User, rank2time) && !e.User.HasRole(rank3))
                        {
                            await e.User.RemoveRoles(rank1);
                            await e.User.AddRoles(rank2);
                            await e.User.SendMessage($"You have been promoted to Veteran in server: {e.Server.Name}");
                        }
                        if (value >= 200 && !e.User.HasRole(rank3) && TimeReqMet(e.User, rank3time))
                        {
                            await e.User.RemoveRoles(rank1);
                            await e.User.RemoveRoles(rank2);
                            await e.User.AddRoles(rank3);
                            await e.User.SendMessage($"You have been promoted to Founder in server: {e.Server.Name}");
                        }
                    }
                }
            });
        }

        public static async Task ServerInit(Server serv)
        {
            Role rank1 = serv.FindRoles("Regular", true).FirstOrDefault();
            Role rank2 = serv.FindRoles("Veteran", true).FirstOrDefault();
            Role rank3 = serv.FindRoles("Founder", true).FirstOrDefault();
            if (rank1 == null) await serv.CreateRole("Regular");
            if (rank2 == null) await serv.CreateRole("Veteran");
            if (rank3 == null) await serv.CreateRole("Founder");
        }

        bool TimeReqMet(User user, TimeSpan basetime)
        {
            TimeSpan joined = TimeSpan.FromTicks(user.JoinedAt.Ticks);
            TimeSpan sincejoin = TimeSpan.FromTicks(DateTime.Now.Ticks);
            sincejoin -= joined;
            if (sincejoin >= basetime)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        async Task NextRank(CommandEventArgs e, string rank, string nextrank)
        {
            ulong rankmesreq = 100;
            switch (nextrank)
            {
                case "Regular":
                    rankmesreq = 100;
                    break;
                case "Veteran":
                    rankmesreq = 150;
                    break;
                case "Founder":
                    rankmesreq = 200;
                    break;
            }
            string message = $"You have {rank} and are ";
            if (Program.messagecount[e.Server.Id][e.User.Id] < rankmesreq)
            {
                ulong messagesuntil = rankmesreq - Program.messagecount[e.Server.Id][e.User.Id];
                message += $"{messagesuntil} message(s) ";
            }
            if (!TimeReqMet(e.User, rank1time))
            {
                if (message.Contains("message(s)")) message += "and ";
                TimeSpan untilnext = rank1time;
                TimeSpan joined = TimeSpan.FromTicks(e.User.JoinedAt.Ticks);
                TimeSpan sincejoin = TimeSpan.FromTicks(DateTime.Now.Ticks);
                sincejoin -= joined;
                untilnext -= sincejoin;
                string timeuntil = $"{untilnext.Days} day(s), {untilnext.Hours} hour(s), and {untilnext.Minutes} minute(s) ";
                message += timeuntil;

            }
            await e.Channel.SendMessage($"{message}away from rank {nextrank}.");
        }
    }
}
