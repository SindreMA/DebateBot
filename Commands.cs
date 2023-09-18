using Discord;
using Discord.Addons.EmojiTools;
using Discord.Commands;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DebateBot.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        private void Save()
        {
            File.WriteAllText("settings.json", JsonConvert.SerializeObject(Program.settings, Formatting.Indented));
        }
        [Command("Help")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Help()
        {
            var eb = Program.SendEmbededMessage("Commands:", "All in () is Optional" +Environment.NewLine + "All in [] is a nummber");
            eb.AddField(".ShowRoles","Show all roles and their ID. Used to get ID to set autoaddrole.");
            eb.AddField(".AutoAddRole [RoleID]","Add a role to the list of all roles that get auto added when joining a debateroom");
            eb.AddField(".RemoveAutoAddRole [RoleID]", "Removes the role from autoaddrole list");
            eb.AddField(".ShowAutoRoles", "Returns you a list of all the autoadd roles");
            eb.AddField(".StopQueueMessage", "Post the DebateControl message in the channel");
            eb.AddField(".AddDebateRoom [VoiceChannelID] [SpeakerAmount]", "Puts the voice channel in debate mode with x amount of speakers");
            eb.AddField(".ShowDebateRooms", "Returns a list of all debate rooms");
            eb.AddField(".ShowSpeakQueue ([VoiceChannelID])" , "Returns a list of the Speak queue for the spesified or current channel");
            eb.AddField(".RemoveDebateRoom [VoiceChannelID]", "Removed debate mode for that voice channel");
            eb.AddField(".AddNextSpeaker ([VoiceChannelID])", "Lets you add the add the next person in queue for your current room or the spesified room");
            eb.AddField(".RotateSpeakers ([AmountOfRotates])", "Swap out the oldest speaker with the first in queue");
            eb.AddField(".UnMuteAllPeople", "Unmutes everyone on the server, nice to have incase bot messes up");
            await Context.Channel.SendMessageAsync("", false, eb.Build());

        }
        [Command("ShowRoles")]
        [Alias("sr")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task ShowRoles()
        {
            List<string> list = new List<string>();
            foreach (var role in Context.Guild.Roles)
            {
                list.Add(role.Name + "(" +role.Id.ToString()+ ")");
            }
            Program.CreateEmbededList("Roles",list,Context,0);

        }
        [Command("UnMuteAllPeople")]
        [Alias("umap")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Unmute()
        {
            foreach (var item in Context.Guild.Users.Where(x=> x.IsMuted))
            {
                await item.ModifyAsync(x => x.Mute = false);
            }
            await Context.Channel.SendMessageAsync("", false, Program.SendEmbededMessage("All users have been unmuted!", "").Build());

        }
        [Command("AutoAddRole")]
        [Alias("aar")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task AutoAddRole(ulong id)
        {
            Program.settings.AutoAddRoles.Add(id);
            Save();
            await Context.Channel.SendMessageAsync("", false, Program.SendEmbededMessage("Role added to autoadd!", "").Build());
        }
        [Command("RemoveAutoAddRole")]
        [Alias("raar")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task RemoveAutoAddRole(ulong id)
        {
            Program.settings.AutoAddRoles.Remove(id);
            Save();
            await Context.Channel.SendMessageAsync("", false, Program.SendEmbededMessage("Role removed from autoadd!", "").Build());
        }
        [Command("ShowAutoRoles")]
        [Alias("sar")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task ShowautoRoles()
        {
            List<string> list = new List<string>();
            foreach (var role in Program.settings.AutoAddRoles)
            {
                var rrole = Context.Guild.GetRole(role);
                list.Add(rrole.Name + "(" + role.ToString() + ")");
            }
            Program.CreateEmbededList("Roles", list, Context, 0);

        }
        [Command("StopQueueMessage")]
        [Alias("sqm")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task StopQueueMessage()
        {
            Context.Message.DeleteAsync();

            var eb = Program.SendEmbededMessage("Debate Room Controls","");
            eb.AddField(""+new Emoji(":stop_button:") + "", "Click this reaction to jump out of queue.");
            eb.AddField("" + new Emoji(":arrow_forward:") + "", "Click this reaction to jump back into the queue.");

            var msg = Context.Channel.SendMessageAsync("", false, eb.Build()).Result;
            await msg.AddReactionAsync(EmojiExtensions.FromText("stop_button"));
            await msg.AddReactionAsync(EmojiExtensions.FromText("arrow_forward"));
            Program.settings.StopQueueMSGList.Add(msg.Id);

        }
        [Command("AddDebateRoom")]
        [Alias("adr")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task AddDebateRoom(ulong ID, int maxtalkUSers)
        {
            var exist = Program.settings.DebateRooms.Exists(x => x.GuildID == Context.Guild.Id && x.RoomID == ID);
            if (!exist)
            {


                Program.DebateRoomValues room = new Program.DebateRoomValues();
                room.ActiveSpeakers = maxtalkUSers;
                room.RoomID = ID;
                room.GuildID = Context.Guild.Id;
                Program.settings.DebateRooms.Add(room);
                await Context.Channel.SendMessageAsync("", false, Program.SendEmbededMessage("Debate mode activated for that room!", "").Build());
            }
            else
            {
                await Context.Channel.SendMessageAsync("", false, Program.SendEmbededMessage("Room is already in debate mode!", "").Build());
            }
        }
        [Command("ShowDebateRooms")]
        [Alias("sdr")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task ShowDebateRooms()
        {
            List<string> rooms = new List<string>();
            foreach (var item in Program.settings.DebateRooms.FindAll(x => x.GuildID == Context.Guild.Id))
            {
                string add = item.RoomID + " - " + item.ActiveSpeakers;
                try
                {
                    var channel = Context.Guild.GetVoiceChannel(item.RoomID);
                    add = "'" + channel.Name + "'(" + item.RoomID + ") - " + item.ActiveSpeakers;
                }
                catch (Exception)
                {
                }
                rooms.Add(add);
            }
            Program.CreateEmbededList("Debate Rooms - (name/channelID - number of speakers)", rooms, Context, 0);
        }
        [Command("ShowSpeakQueue")]
        [Alias("ssq")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task ShowSpeakQueue([Optional]ulong id)
        {
            Context.Message.DeleteAsync();
            if (id == 0)
            {
                if (Context.Guild.VoiceChannels.ToList().Exists(x => x.Users.ToList().Exists(z => z.Id == Context.User.Id)))
                {
                    var channel = Context.Guild.VoiceChannels.ToList().Single(x => x.Users.ToList().Exists(z => z.Id == Context.User.Id));
                    id = channel.Id;
                }
                else
                {
                    await Context.Channel.SendMessageAsync("", false, Program.SendEmbededMessage("You need to send the ChannelID with the Command!", "").Build());
                }
            }
            var channelll = Context.Guild.GetVoiceChannel(id);

            List<string> que = new List<string>();
            foreach (var item in Program.settings.QueueList.Where(x => x.DebateRoom == id && x.GuildID == Context.Guild.Id))
            {
                string s = "In Queue";
                if (item.NotInQueue)
                {
                    s = "Out of Queue";
                }
                if (!item.NotInQueue && item.Speaker)
                {
                    s = "4543";
                }
                string state = "Speaking";
                if (!item.Speaker)
                {
                    state = "Muted";
                }

                que.Add(($"{item.UserName} - {state} - {s}").Replace("- 4543", ""));

            }
            Program.CreateEmbededList("Speaker Queue for - " + channelll.Name, que, Context, id);
        }
        [Command("RemoveDebateRoom")]
        [Alias("rdr")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(Discord.GuildPermission.MuteMembers)]
        public async Task RemoveDebateRoom(ulong id)
        {
            if (id == 0)
            {
                if (Context.Guild.VoiceChannels.ToList().Exists(x => x.Users.ToList().Exists(z => z.Id == Context.User.Id)))
                {
                    var channel = Context.Guild.VoiceChannels.ToList().Single(x => x.Users.ToList().Exists(z => z.Id == Context.User.Id));
                    id = channel.Id;
                }
                else
                {
                    await Context.Channel.SendMessageAsync("", false, Program.SendEmbededMessage("You need to send the ChannelID with the Command!", "").Build());
                }
            }
            var channelll = Context.Guild.GetVoiceChannel(id);


            var room = Program.settings.DebateRooms.Single(x => x.GuildID == Context.Guild.Id && x.RoomID == id);
            Program.settings.DebateRooms.Remove(room);

            await Context.Channel.SendMessageAsync("", false, Program.SendEmbededMessage("Debate mode for that room have been removed!", "").Build());
        }
        [Command("AddNextSpeaker")]
        [Alias("ans")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task AddNextSpeaker([Optional]ulong id)
        {
            if (id == 0)
            {
                if (Context.Guild.VoiceChannels.ToList().Exists(x => x.Users.ToList().Exists(z => z.Id == Context.User.Id)))
                {
                    var channel = Context.Guild.VoiceChannels.ToList().Single(x => x.Users.ToList().Exists(z => z.Id == Context.User.Id));
                    id = channel.Id;
                }
                else
                {
                    await Context.Channel.SendMessageAsync("", false, Program.SendEmbededMessage("You need to send the ChannelID with the Command!", "").Build());
                }
            }
            var channelll = Context.Guild.GetVoiceChannel(id);




            var Room = Program.settings.DebateRooms.Single(x => x.RoomID == id);

            var NextUnmuteUser = Context.Guild.GetUser(Program.settings.QueueList.Where(x => x.DebateRoom == Room.RoomID).OrderByDescending(a => a.JoinStamp).FirstOrDefault().UserID);
            await NextUnmuteUser.ModifyAsync(x => x.Mute = false);
            await Program.Log(NextUnmuteUser.Username + " is now Un-Servermuted cause of next speak command", ConsoleColor.Cyan);
        }
        [Command("RotateSpeakers")]
        [Alias("rs")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task RotateSpeakers([Optional]int nr)
        {
            ulong id = 0;
                if (Context.Guild.VoiceChannels.ToList().Exists(x => x.Users.ToList().Exists(z => z.Id == Context.User.Id)))
                {
                    var channel = Context.Guild.VoiceChannels.ToList().Single(x => x.Users.ToList().Exists(z => z.Id == Context.User.Id));
                    id = channel.Id;
                }
                else
                {
                    await Context.Channel.SendMessageAsync("", false, Program.SendEmbededMessage("You need to be in a debate room to use this command!", "").Build());
                }
            if (nr == 0)
            {
                nr = 1;
            }
            



            for (int i = 0; i < nr; i++)
            {
                await rotate(id);
            }
        }
        public async Task rotate(ulong RoomID)
        {
            var Room = Program.settings.DebateRooms.SingleOrDefault(x => x.RoomID == RoomID);

            var NextUnmuteUser = Context.Guild.GetUser(Program.settings.QueueList.Where(x => x.DebateRoom == Room.RoomID && !x.Speaker).OrderBy(a => a.JoinStamp).FirstOrDefault().UserID);
            if (Program.settings.QueueList.Exists(x => x.GuildID == Context.Guild.Id && x.UserID == NextUnmuteUser.Id))
            {
                var Nuser = Program.settings.QueueList.Single(x => x.GuildID == Context.Guild.Id && x.UserID == NextUnmuteUser.Id);
                Nuser.Speaker = true;
            }
            await NextUnmuteUser.ModifyAsync(x => x.Mute = false);
            await Program.Log(NextUnmuteUser.Username + " is now Un-Servermuted cause of rotation command", ConsoleColor.Cyan);


            var OldestunmutedUser = Context.Guild.GetUser(Program.settings.QueueList.Where(x => x.DebateRoom == Room.RoomID && x.Speaker).OrderBy(a => a.JoinStamp).FirstOrDefault().UserID);
            if (Program.settings.QueueList.Exists(x => x.GuildID == Context.Guild.Id && x.UserID == NextUnmuteUser.Id))
            {
                var Ouser = Program.settings.QueueList.Single(x => x.GuildID == Context.Guild.Id && x.UserID == OldestunmutedUser.Id);
                Ouser.Speaker = false;
                Ouser.JoinStamp = DateTime.Now;
            }
            //var OldestunmutedUser = Context.Guild.GetUser(Program.settings.QueueList.Where(x => x.DebateRoom == Room.RoomID && !Context.Guild.GetUser(x.UserID).IsMuted).OrderByDescending(a => a.JoinStamp).FirstOrDefault().UserID);
            await OldestunmutedUser.ModifyAsync(x => x.Mute = true);
            await Program.Log(NextUnmuteUser.Username + " is nowServermuted cause of rotation command", ConsoleColor.Cyan);

        }

    }
}