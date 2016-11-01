using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Levels;
using Discord.Modules;

namespace MagBot
{
    class GeneralModule : IModule
    {
        private DiscordClient client;
        private ModuleManager _manager;
        private ModuleService service;

        void IModule.Install(ModuleManager manager)
        {
            _manager = manager;
            client = manager.Client;

            manager.CreateCommands("", cgb =>
            {
                cgb.MinPermissions((int)PermissionLevel.User);

                // Return info about the bot
                cgb.CreateCommand("info")
                    .Description("Displays info and stats for Mag-Bot.")
                    .Do(async e =>
                    {
                        int servers = client.Servers.Count();
                        int channels = client.Servers.SelectMany(s => s.AllChannels).Count();
                        int users = client.Servers.SelectMany(s => s.Users).Count();
                        TimeSpan uptime = TimeSpan.FromTicks(DateTime.Now.Ticks) - Program.startedat;
                        await e.Channel.SendMessage($"If you'd like to report a bug or request a feature, contact the dev.\n__Info:__\n-**Developer:** Magmatic#2220\n-**Library:** Discord.NET (0.9.4)\n-**Runtime:** .NET Framework 4.5.2\n-**Uptime:** {uptime.Days} days, {uptime.Hours} hours, {uptime.Minutes} minutes, and {uptime.Seconds} seconds\n\n__Stats:__\n-**Servers:** {servers}\n-**Channels:** {channels}\n-**Users:** {users}");
                    });

                // Get invite link
                cgb.CreateCommand("invite")
                    .AddCheck((cmd, u, c) => Program.invitesallowed)
                    .Description("Get an invite link to add Mag-Bot to your own server!")
                    .Do(async e =>
                    {
                        await e.User.SendMessage("Here ya go! <https://discordapp.com/oauth2/authorize?client_id=198296897784381441&scope=bot&permissions=338938929>");
                    });

                // Announcement command group
                cgb.CreateGroup("announce", g =>
                {
                    g.MinPermissions((int)PermissionLevel.ServerAdmin);
                    g.AddCheck((cmd, u, ch) => !ch.IsPrivate);

                    // Toggle announcements for server
                    g.CreateCommand("toggle")
                        .Description("Toggles announcements for the server. User joined/left/banned.")
                        .Do(async e =>
                        {
                            bool on = false;
                            if (Program.annolist.TryGetValue(e.Server.Id, out on))
                            {
                                if (on)
                                {
                                    Program.annolist[e.Server.Id] = false;
                                    await e.Channel.SendMessage("Announcements are now off.");
                                }
                                else if (!on)
                                {
                                    Program.annolist[e.Server.Id] = true;
                                    await e.Channel.SendMessage("Announcements are now on.");
                                }
                            }
                            else
                            {
                                Program.annolist[e.Server.Id] = true;
                                await e.Channel.SendMessage("Announcements are now on.");
                            }
                        });

                    // Set channel for making announcements
                    g.CreateCommand("set")
                        .Description("Sets the channel in which announcments will be made. Run it in the channel you wish to set announcments to.")
                        .Do(async e =>
                        {
                            Program.annochlist[e.Server.Id] = e.Channel.Id;
                            await e.Channel.SendMessage(string.Format("Channel **{0}** set for announcements.", e.Channel.Name));
                        });
                });

                // Roll command
                cgb.CreateCommand("roll")
                    .Description("Roll a die. Default range of 1-6.")
                    .Parameter("range", ParameterType.Optional)
                    .Do(async e =>
                    {
                        int num;
                        if (e.GetArg("range") != "")
                        {
                            int max = Convert.ToInt32(e.GetArg("range"));
                            if (max > 1)
                            {
                                num = RandomNumberGenerator.NumberBetween(1, max);
                                await e.Channel.SendMessage(string.Format("Rolled {0}.", num));
                            }
                            else
                            {
                                await e.Channel.SendMessage("Please enter a number greater than 1.");
                            }
                        }
                        else
                        {
                            num = RandomNumberGenerator.NumberBetween(1, 6);
                            await e.Channel.SendMessage(string.Format("Rolled {0}.", num));
                        }
                    });

                // Karma command
                cgb.CreateCommand("karma")
                    .Description("Returns the karma value for the given phrase. Use ++ or -- at the start or end of a messgae to increment or decrement karma, respectively.")
                    .Parameter("phrase", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        if (e.GetArg("phrase") != "")
                        {
                            string phrase = e.GetArg("phrase").ToLower();
                            long karma = 0;
                            Program.karmalist.TryGetValue(phrase, out karma);
                            await e.Channel.SendMessage(string.Format("Karma for {0} is at {1}.", phrase, karma));
                        }
                        else
                        {
                            await e.Channel.SendMessage("You must enter a phrase.");
                        }
                    });

                // Tag command group
                cgb.CreateGroup("tag", g =>
                {
                    g.AddCheck((cmd, u, ch) => !ch.IsPrivate);

                    // Retrieve tags for keyword
                    g.CreateCommand("query")
                        .Description("Returns tags for given keyword. You may also use ?<keyword>.")
                        .Parameter("keyword", ParameterType.Required)
                        .Do(async e =>
                        {
                            List<string> value = new List<string>();
                            SortedDictionary<string, List<string>> tags = new SortedDictionary<string, List<string>>();
                            Program.taglist.GetOrAdd(e.Server.Id, tags);
                            if (Program.taglist[e.Server.Id].TryGetValue(e.GetArg("keyword"), out value))
                            {
                                string text = string.Join(", ", value);
                                await e.Channel.SendMessage($"Tags for **{e.GetArg("keyword")}**: {text}");
                            }
                            else
                            {
                                await e.Channel.SendMessage($"Keyword **{e.GetArg("keyword")}** has no tags.");
                            }
                        });

                    // Add tag to keyword
                    g.CreateCommand("add")
                        .Description("Adds a tag to a keyword.")
                        .Parameter("keyword", ParameterType.Required)
                        .Parameter("tag", ParameterType.Unparsed)
                        .Do(async e =>
                        {
                            SortedDictionary<string, List<string>> tags = new SortedDictionary<string, List<string>>();
                            Program.taglist.GetOrAdd(e.Server.Id, tags);
                            string keyword = e.GetArg("keyword").ToLower();
                            string tag = e.GetArg("tag");
                            List<string> current = new List<string>();
                            if (!Program.taglist[e.Server.Id].TryGetValue(keyword, out current))
                            {
                                current = new List<string>();
                            }
                            current.Add(tag);
                            Program.taglist[e.Server.Id][keyword] = current;
                            await e.Channel.SendMessage($"Tag *{tag}* added to keyword **{keyword}**.");
                        });

                    g.CreateCommand("remove")
                        .Description("Removes a tag from a keyword.")
                        .Alias("del", "delete")
                        .Parameter("keyword", ParameterType.Required)
                        .Parameter("tag", ParameterType.Unparsed)
                        .Do(async e =>
                        {
                            SortedDictionary<string, List<string>> tags = new SortedDictionary<string, List<string>>();
                            Program.taglist.GetOrAdd(e.Server.Id, tags);
                            string keyword = e.GetArg("keyword").ToLower();
                            string tag = e.GetArg("tag");
                            List<string> current = new List<string>();
                            if (Program.taglist[e.Server.Id].TryGetValue(keyword, out current))
                            {
                                if (current.Remove(e.GetArg("tag")))
                                {
                                    Program.taglist[e.Server.Id][keyword] = current;
                                    await e.Channel.SendMessage($"Tag *{tag}* removed from keyword **{keyword}**.");
                                }
                                else
                                {
                                    await e.Channel.SendMessage($"Keyword **{tag}** does not have tag *{tag}*.");
                                }
                            }
                            else
                            {
                                await e.Channel.SendMessage($"Keyword **{keyword}** has no tags.");
                            }
                        });

                    // Clear tags from keyword
                    g.CreateCommand("clear")
                        .Description("Clears a keyword of its tags.")
                        .Parameter("keyword", ParameterType.Required)
                        .Do(async e =>
                        {
                            string keyword = e.GetArg("keyword").ToLower();
                            SortedDictionary<string, List<string>> tags = new SortedDictionary<string, List<string>>();
                            Program.taglist.GetOrAdd(e.Server.Id, tags);
                            if (Program.taglist[e.Server.Id].Remove(keyword))
                            {
                                await e.Channel.SendMessage($"Tags for keyword **{keyword}** cleared.");
                            }
                            else
                            {
                                await e.Channel.SendMessage($"Keyword **{keyword}** has no tags.");
                            }
                        });

                    // Send a PM with a list of tags
                    g.CreateCommand("list")
                        .Description("Sends a private message with a list of available tags.")
                        .Do(async e =>
                        {
                            SortedDictionary<string, List<string>> tags = new SortedDictionary<string, List<string>>();
                            Program.taglist.GetOrAdd(e.Server.Id, tags);
                            SortedDictionary<string, List<string>>.KeyCollection keys = tags.Keys;
                            string keylist = "The following tags are available: " + string.Join(", ", keys);
                            await e.Message.User.SendMessage(keylist);
                        });
                });

                cgb.CreateCommand("choose")
                    .Alias("decide")
                    .Description("Choose between a set of choices. Seperate words with spaces or phrases with commas.")
                    .Parameter("choices", ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        List<string> choices;
                        string choicesunsplit = e.GetArg("choices");
                        if (choicesunsplit.Contains(","))
                        {
                            choices = new List<string>(choicesunsplit.Split(','));
                        }
                        else
                        {
                            choices = new List<string>(choicesunsplit.Split(' '));
                        }

                        int index = RandomNumberGenerator.NumberBetween(0, choices.Count);
                        string choice = choices[index].Trim(' ');
                        await e.Channel.SendMessage($"{choice}");
                    });

            });

            // Message recieved
            client.MessageReceived += (async (s, e) =>
            {
                // Alternate tag command
                if (e.Message.Text.StartsWith("?") && e.Message.Text.Length != 1 && !e.Channel.IsPrivate)
                {
                    string keyword = e.Message.Text.Substring(1).ToLower();
                    if (keyword != null || keyword != "" || keyword != " ")
                    {
                        List<string> value = new List<string>();
                        SortedDictionary<string, List<string>> tags = new SortedDictionary<string, List<string>>();
                        Program.taglist.GetOrAdd(e.Server.Id, tags);
                        if (Program.taglist[e.Server.Id].TryGetValue(keyword, out value))
                        {
                            string text = $"Tags for **{keyword}**: " + string.Join(", ", value);
                            await e.Channel.SendMessage(text);
                        }
                        else
                        {
                            await e.Channel.SendMessage($"Keyword **{keyword}** has no tags.");
                        }
                    }
                }

                // Karma increase
                if (e.Message.Text.EndsWith("++") || e.Message.Text.StartsWith("++"))
                {
                    string phrase = "";
                    if (e.Message.Text.EndsWith("++"))
                    {
                        phrase = e.Message.Text.Remove(e.Message.Text.LastIndexOf("++")).ToLower();
                    }
                    else
                    {
                        phrase = e.Message.Text.Remove(e.Message.Text.IndexOf("++"), 2).ToLower();
                    }
                    long karma = 0;
                    if (phrase != "")
                    {
                        int cooldown = 0;
                        Program.karmacooldown.TryGetValue(e.User.Id, out cooldown);
                        if (cooldown == 0)
                        {
                            if (Program.karmalist.TryGetValue(phrase, out karma))
                            {
                                Program.karmalist[phrase]++;
                            }
                            else
                            {
                                Program.karmalist[phrase] = 1;
                            }
                            Program.karmacooldown[e.User.Id] = 60;
                            await e.Channel.SendMessage(string.Format("Karma for {0} is now at {1}. [60s]", phrase, Program.karmalist[phrase]));
                        }
                        else
                        {
                            await e.User.SendMessage(string.Format("You have recently used karma and cannot use it again for {0} seconds.", Program.karmacooldown[e.User.Id]));
                        }
                    }
                }

                // Karma decrease
                else if (e.Message.Text.EndsWith("--") || e.Message.Text.StartsWith("--"))
                {
                    string phrase = "";
                    if (e.Message.Text.EndsWith("--"))
                    {
                        phrase = e.Message.Text.Remove(e.Message.Text.LastIndexOf("--")).ToLower();
                    }
                    else
                    {
                        phrase = e.Message.Text.Remove(e.Message.Text.IndexOf("--"), 2).ToLower();
                    }
                    long karma = 0;
                    if (phrase != "")
                    {
                        int cooldown = 0;
                        Program.karmacooldown.TryGetValue(e.User.Id, out cooldown);
                        if (cooldown == 0)
                        {
                            if (Program.karmalist.TryGetValue(phrase, out karma))
                            {
                                Program.karmalist[phrase]--;
                            }
                            else
                            {
                                Program.karmalist[phrase] = -1;
                            }
                            Program.karmacooldown[e.User.Id] = 60;
                            await e.Channel.SendMessage(string.Format("Karma for {0} is now at {1}. [60s]", phrase, Program.karmalist[phrase]));
                        }
                        else
                        {
                            await e.User.SendMessage(string.Format("You have recently used karma and cannot use it again for {0} seconds.", Program.karmacooldown[e.User.Id]));
                        }
                    }
                }
            });
        }
    }
}
