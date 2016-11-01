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
using System.Timers;

namespace MagBot
{
    class RaffleModule : IModule
    {
        private DiscordClient client;
        private ModuleManager manager;
        private static ConcurrentDictionary<ulong, List<ulong>> raffleentries;
        private static ConcurrentDictionary<ulong, bool> rafflerunning;
        private static ConcurrentDictionary<ulong, ulong> raffleowners;
        private static ConcurrentDictionary<ulong, Role> roleneeded;
        private static ConcurrentDictionary<ulong, Timer> raffletimers;
        private static ConcurrentDictionary<ulong, DateTime> timestarted;

        void IModule.Install(ModuleManager _manager)
        {
            manager = _manager;
            client = manager.Client;

            raffleentries = new ConcurrentDictionary<ulong, List<ulong>>();
            rafflerunning = new ConcurrentDictionary<ulong, bool>();
            raffleowners = new ConcurrentDictionary<ulong, ulong>();
            roleneeded = new ConcurrentDictionary<ulong, Role>();
            raffletimers = new ConcurrentDictionary<ulong, Timer>();
            timestarted = new ConcurrentDictionary<ulong, DateTime>();

            manager.CreateCommands("", cgb =>
            {
                cgb.MinPermissions((int)PermissionLevel.User);

                cgb.CreateGroup("raffle", g =>
                {
                    g.CreateCommand("auto")
                    .Description("Automatically select raffle winners. You can specify the number of winners and a role to restrict the raffle to.")
                    .Parameter("winners", ParameterType.Required)
                    .Parameter("role", ParameterType.Optional)
                    .Do(async e =>
                   {
                       int winners;
                       if (!Int32.TryParse(e.Args[0], out winners) || winners < 1)
                       {
                           await e.Channel.SendMessage("Error: Invalid winner count.");
                           return;
                       }
                       Role role = e.Server.FindRoles(e.Args[1]).FirstOrDefault();
                       if (e.Args[1] != "" && role == null)
                       {
                           await e.Channel.SendMessage("Error: Invalid role.");
                           return;
                       }
                       if (e.Args[1] == "") role = e.Server.EveryoneRole;

                       List<User> users = new List<User>();
                       foreach (User user in e.Server.Users)
                       {
                           if (user.HasRole(role)) users.Add(user);
                       }

                       if(users.Count < winners)
                       {
                           winners = users.Count;
                       }
                       Random rand = new Random();
                       List<User> wins = new List<User>();
                       for (int i = 0; i < winners; i++)
                       {
                           User win = users[RandomNumberGenerator.NumberBetween(0, users.Count - 1)];
                           if (!wins.Contains(win))
                           {
                               wins.Add(win);
                           }
                           else
                           {
                               i--;
                           }
                       }

                       List<string> mentions = new List<string>();
                       foreach (User u in wins)
                       {
                           mentions.Add(u.Mention);
                       }

                       string message;
                       if (winners == 1) message = "The winner is: ";
                       else message = "The winners are: ";

                       message += string.Join(", ", mentions);
                       await e.Channel.SendMessage(message);
                   });

                    g.CreateCommand("")
                    .Description("Create a raffle that people have to enter. You can specify the length of the raffle (s, m, h for seconds, minutes, hours), number of winners, and a role to restrict the raffle to.")
                    .Parameter("time", ParameterType.Required)
                    .Parameter("winners", ParameterType.Required)
                    .Parameter("role", ParameterType.Optional)
                    .Do(async e =>
                    {
                        bool running = false;
                        running = rafflerunning.GetOrAdd(e.Server.Id, running);
                        if (running)
                        {
                            await e.Channel.SendMessage("A raffle is already running in this server.");
                            return;
                        }

                        int winners;
                        if (!Int32.TryParse(e.Args[1], out winners) || winners < 1)
                        {
                            await e.Channel.SendMessage("Error: Invalid winner count.");
                            return;
                        }
                        Role role = e.Server.FindRoles(e.Args[2]).FirstOrDefault();
                        if (e.Args[2] != "" && role == null)
                        {
                            await e.Channel.SendMessage("Error Invalid role.");
                            return;
                        }
                        if (e.Args[2] == "") role = e.Server.EveryoneRole;
                        roleneeded[e.Server.Id] = role;

                        string rawtime = e.Args[0];
                        TimeSpan raffletime = new TimeSpan();
                        char rawlast = rawtime.Last();
                        rawtime = rawtime.Remove(rawtime.Count()-1, 1);

                        int time;
                        if (!Int32.TryParse(rawtime, out time))
                        {
                            await e.Channel.SendMessage("Error: Invalid time.");
                            return;
                        }

                        string timeend;
                        switch (rawlast)
                        {
                            case 's':
                                raffletime = TimeSpan.FromSeconds(time);
                                timeend = "second(s)";
                                break;
                            case 'm':
                                raffletime = TimeSpan.FromMinutes(time);
                                timeend = "minute(s)";
                                break;
                            case 'h':
                                raffletime = TimeSpan.FromHours(time);
                                timeend = "hour(s)";
                                break;
                            default:
                                await e.Channel.SendMessage("Error: Invalid time.");
                                return;
                        }

                        if (raffletime > TimeSpan.FromHours(24))
                        {
                            await e.Channel.SendMessage("Error: Time cannot be longer than 24 hours.");
                            return;
                        }

                        raffleowners[e.Server.Id] = e.User.Id;
                        rafflerunning[e.Server.Id] = true;
                        raffleentries[e.Server.Id] = new List<ulong>();

                        await e.Channel.SendMessage($"Attention @here! {e.User.Name} has started a raffle! Use !enter to enter and !leave to leave. The raffle ends in {time} {timeend}. Role {role.Name} required. Cancel the raffle with !cancel.");

                        Timer raffletimer = new Timer(raffletime.TotalMilliseconds);
                        raffletimers[e.Server.Id] = raffletimer;
                        raffletimer.AutoReset = false;
                        raffletimer.Elapsed += (async (s, ev) =>
                        {
                            await raffleover(e, winners);
                            raffletimer.Close();
                        });
                        raffletimer.Start();
                        timestarted[e.Server.Id] = DateTime.Now;
                    });
                });

                cgb.CreateCommand("enter")
                .Description("Enter the currently running raffle.")
                .Do(async e =>
                {
                    bool running = false;
                    running = rafflerunning.GetOrAdd(e.Server.Id, running);
                    if (!running)
                    {
                        await e.Channel.SendMessage("There is no raffle currently running.");
                        return;
                    }

                    if (e.User.Id == raffleowners[e.Server.Id])
                    {
                        await e.Channel.SendMessage("You are the raffle owner.");
                        return;
                    }

                    if (e.User.HasRole(roleneeded[e.Server.Id]))
                    {
                        if (!raffleentries[e.Server.Id].Contains(e.User.Id))
                        {
                            raffleentries[e.Server.Id].Add(e.User.Id);
                            await e.Channel.SendMessage("You are now entered into the raffle.");
                        }
                        else
                        {
                            await e.Channel.SendMessage("You are already in the raffle.");
                            return;
                        }
                    }
                    else
                    {
                        await e.Channel.SendMessage("You do not have the required role.");
                        return;
                    }
                });

                cgb.CreateCommand("leave")
                .Description("Leave the currently running raffle.")
                .Do(async e =>
                {
                    bool running = false;
                    running = rafflerunning.GetOrAdd(e.Server.Id, running);
                    if (!running)
                    {
                        await e.Channel.SendMessage("There is no raffle currently running.");
                        return;
                    }

                    if (e.User.Id == raffleowners[e.Server.Id])
                    {
                        await e.Channel.SendMessage("You are the raffle owner.");
                        return;
                    }

                    if (raffleentries[e.Server.Id].Contains(e.User.Id))
                    {
                        raffleentries[e.Server.Id].Remove(e.User.Id);
                        await e.Channel.SendMessage("You are no longer entered into the raffle.");
                    }
                    else
                    {
                        await e.Channel.SendMessage("You are not in the raffle.");
                        return;
                    }
                });

                cgb.CreateCommand("cancel")
                .Description("Cancel the current raffle.")
                .Do(async e =>
                {
                    bool running = false;
                    running = rafflerunning.GetOrAdd(e.Server.Id, running);
                    if (!running)
                    {
                        await e.Channel.SendMessage("There is no raffle currently running.");
                        return;
                    }

                    if (e.User.Id == raffleowners[e.Server.Id] || e.User.ServerPermissions.ManageRoles)
                    {
                        raffletimers[e.Server.Id].Close();
                        raffleentries[e.Server.Id].Clear();
                        rafflerunning[e.Server.Id] = false;
                        raffleowners[e.Server.Id] = 0;
                        await e.Channel.SendMessage("Raffle cancelled.");
                    }
                    else
                    {
                        await e.Channel.SendMessage("You are not the raffle owner.");
                        return;
                    }
                });

                cgb.CreateCommand("timeleft")
                    .Description("Get the time left in the current raffle.")
                    .Do(async e =>
                    {
                        bool running = false;
                        running = rafflerunning.GetOrAdd(e.Server.Id, running);
                        if (!running)
                        {
                            await e.Channel.SendMessage("There is no raffle currently running.");
                            return;
                        }

                        TimeSpan timeleft = TimeSpan.FromTicks(DateTime.Now.Ticks) - TimeSpan.FromTicks(timestarted[e.Server.Id].Ticks);
                        timeleft = TimeSpan.FromMilliseconds(raffletimers[e.Server.Id].Interval) - timeleft;
                        string time;
                        if (timeleft.TotalSeconds < 60)
                        {
                            time = timeleft.Seconds + " second(s)";
                        }
                        else if (timeleft.TotalMinutes < 60)
                        {
                            time = timeleft.Minutes + " minute(s)";
                        }
                        else
                        {
                            time = timeleft.Hours + " hour(s)";
                        }

                        await e.Channel.SendMessage($"There are {time} left in the raffle.");
                    });

            });
        }

        async Task raffleover(CommandEventArgs e, int winners)
        {
            if(raffleentries[e.Server.Id].Count() == 0)
            {
                await e.Channel.SendMessage("The raffle ended with no entries.");
                raffleentries[e.Server.Id].Clear();
                raffleowners[e.Server.Id] = 0;
                rafflerunning[e.Server.Id] = false;
                return;
            }
            List<User> users = new List<User>();
            foreach (ulong user in raffleentries[e.Server.Id])
            {
                users.Add(e.Server.GetUser(user));
            }

            if (users.Count < winners)
            {
                winners = users.Count;
            }
            List<User> wins = new List<User>();
            for (int i = 0; i < winners; i++)
            {
                User win = users[RandomNumberGenerator.NumberBetween(0, users.Count - 1)];
                if (!wins.Contains(win))
                {
                    wins.Add(win);
                }
                else
                {
                    i--;
                }
            }

            List<string> mentions = new List<string>();
            foreach (User u in wins)
            {
                mentions.Add(u.Mention);
            }

            string message;
            if (winners == 1) message = "Time's up! The winner is: ";
            else message = "Time's up! The winners are: ";

            message += string.Join(", ", mentions);
            await e.Channel.SendMessage(message);

            raffleentries[e.Server.Id].Clear();
            raffleowners[e.Server.Id] = 0;
            rafflerunning[e.Server.Id] = false;
        }

        public static bool RaffleRunning()
        {
            foreach(bool b in rafflerunning.Values)
            {
                if(b)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
