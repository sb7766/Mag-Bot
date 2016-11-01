using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Levels;
using Discord.Modules;

namespace MagBot
{
    class OwnerModule : IModule
    {
        private DiscordClient client;
        private ModuleManager _manager;

        void IModule.Install(ModuleManager manager)
        {
            _manager = manager;
            client = manager.Client;

            manager.CreateCommands("", cgb =>
            {
                cgb.MinPermissions((int)PermissionLevel.BotOwner);

                // Shut down the bot
                cgb.CreateCommand("shutdown")
                    .Do(async e =>
                    {
                        await e.Channel.SendMessage("Shutting down...");
                        Program.Shutdown();
                    });

                cgb.CreateCommand("restart")
                    .Do(async e =>
                    {
                        await e.Channel.SendMessage("Restarting...");
                        Program.Restart();
                    });
                cgb.CreateCommand("save")
                    .Do(async e =>
                    {
                        await e.Channel.SendMessage("Saving...");
                        Program.Save();
                        await e.Channel.SendMessage("Saved.");
                    });

                // List servers
                cgb.CreateCommand("serverlist")
                    .AddCheck((c, u, ch) => ch.IsPrivate)
                    .Do(async e =>
                    {
                        string message = "";
                        foreach (var s in client.Servers)
                        {
                            string name = s.Name;
                            ulong id = s.Id;
                            message = message + string.Format("**Name:** {0} - **ID:** {1}\n", name, id);
                        }
                        await e.User.SendMessage(message);
                    });

                // List channels in server
                cgb.CreateCommand("channellist")
                    .AddCheck((c, u, ch) => ch.IsPrivate)
                    .Parameter("serverid", ParameterType.Required)
                    .Do(async e =>
                    {
                        ulong id = 0;
                        UInt64.TryParse(e.GetArg("serverid"), out id);
                        var server = client.GetServer(id);
                        string name = server.Name;
                        string message = string.Format("Channels in server {0} (ID: {1}):\n", name, id);
                        foreach (var ch in server.AllChannels)
                        {
                            string cname = ch.Name;
                            ulong cid = ch.Id;
                            message = message + string.Format("**Name:** {0} - **ID:** {1}\n", cname, cid);
                        }
                        await e.User.SendMessage(message);
                    });

                // Leave server
                cgb.CreateCommand("leaveserver")
                    .AddCheck((c, u, ch) => ch.IsPrivate)
                    .Parameter("serverid", ParameterType.Required)
                    .Do(async e =>
                    {
                        ulong id = 0;
                        UInt64.TryParse(e.GetArg("serverid"), out id);
                        var s = client.GetServer(id);
                        string name = s.Name;
                        await s.Leave();
                        await e.User.SendMessage(string.Format("Left server {0} (ID: {1})", name, id));
                    });

                // Create invite
                cgb.CreateCommand("makeinvite")
                    .AddCheck((c, u, ch) => ch.IsPrivate)
                    .Parameter("serverid", ParameterType.Required)
                    .Do(async e =>
                    {
                        ulong id = 0;
                        UInt64.TryParse(e.GetArg("serverid"), out id);
                        Server serv = client.GetServer(id);
                        Invite inv = await serv.CreateInvite();
                        string url = inv.Url.Remove(inv.Url.LastIndexOf("/"), 1);
                        await e.User.SendMessage(string.Format("Invite link to {0}: {1}", serv.Name, url));
                    });

                cgb.CreateCommand("checkperms")
                    .AddCheck((c, u, ch) => ch.IsPrivate)
                    .Parameter("serverid", ParameterType.Required)
                    .Do(async e =>
                    {
                        ulong id = 0;
                        UInt64.TryParse(e.GetArg("serverid"), out id);
                        Server serv = client.GetServer(id);
                        List<string> permlist = new List<string>();
                        ServerPermissions perms = serv.CurrentUser.ServerPermissions;
                        if (perms.Administrator == true) permlist.Add("Administrator");
                        if (perms.AttachFiles == true) permlist.Add("Attach Files");
                        if (perms.BanMembers == true) permlist.Add("Ban Members");
                        if (perms.ChangeNickname == true) permlist.Add("Change Nickname");
                        if (perms.Connect == true) permlist.Add("Connect");
                        if (perms.CreateInstantInvite == true) permlist.Add("Create Instant Invite");
                        if (perms.DeafenMembers == true) permlist.Add("Deafen Members");
                        if (perms.EmbedLinks == true) permlist.Add("Embed Links");
                        if (perms.KickMembers == true) permlist.Add("Kick Members");
                        if (perms.ManageChannels == true) permlist.Add("Manage Channels");
                        if (perms.ManageMessages == true) permlist.Add("Manage Messages");
                        if (perms.ManageNicknames == true) permlist.Add("Manage Nicknames");
                        if (perms.ManageRoles == true) permlist.Add("Manage Roles");
                        if (perms.ManageServer == true) permlist.Add("Manage Server");
                        if (perms.MentionEveryone == true) permlist.Add("Mention Everyone");
                        if (perms.MoveMembers == true) permlist.Add("Move Members");
                        if (perms.MuteMembers == true) permlist.Add("Mute Members");
                        if (perms.ReadMessageHistory == true) permlist.Add("Read Message History");
                        if (perms.ReadMessages == true) permlist.Add("Read Messages");
                        if (perms.SendMessages == true) permlist.Add("Send Messages");
                        if (perms.SendTTSMessages == true) permlist.Add("Send TTS Messages");
                        if (perms.Speak == true) permlist.Add("Speak");
                        if (perms.UseVoiceActivation == true) permlist.Add("Use Voice Activation");
                        string perm = string.Join(", ", permlist);
                        await e.User.SendMessage($"Server permissions for {serv.Name}: {perm}");
                    });

                // Toggle invites allowed
                cgb.CreateCommand("invitestoggle")
                    .AddCheck((c, u, ch) => ch.IsPrivate)
                    .Do(async e =>
                    {
                        if (Program.invitesallowed)
                        {
                            Program.invitesallowed = false;
                            await e.User.SendMessage("Invites no longer allowed.");
                        }
                        else
                        {
                            Program.invitesallowed = true;
                            await e.User.SendMessage("Invites now allowed.");
                        }
                    });
            });
        }
    }
}
