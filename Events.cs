using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DebateBot.Program;

namespace DebateBot
{
    class Events
    {
        private DiscordSocketClient _client;
        private CommandService _service;
        bool IsMuteChange(SocketVoiceState OldState, SocketVoiceState CurrentState)
        {

            if (OldState.IsDeafened != CurrentState.IsDeafened)
            {
                return true;
            }
            if (OldState.IsMuted != CurrentState.IsMuted)
            {
                return true;
            }
            if (OldState.IsSelfDeafened != CurrentState.IsSelfDeafened)
            {
                return true;
            }
            if (OldState.IsSelfMuted != CurrentState.IsSelfMuted)
            {
                return true;
            }
            if (OldState.IsSuppressed != CurrentState.IsSuppressed)
            {
                return true;
            }
            return false;
        }
        public Events(DiscordSocketClient client)
        {
            _client = client;
            _service = new CommandService();
            _service.AddModulesAsync(Assembly.GetEntryAssembly());
            _client.UserVoiceStateUpdated += _client_UserVoiceStateUpdated;
            _client.ReactionAdded += _client_ReactionAdded;

        }



        private async Task _client_ReactionAdded(Discord.Cacheable<Discord.IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {

            if (Program.settings.EmbededLists.Exists(x => x.messageID == arg1.Id) && arg3.UserId != _client.CurrentUser.Id)
            {
                var item = Program.settings.EmbededLists.Find(x => x.messageID == arg1.Id);
                if (arg3.Emote.Name == "▶")
                {
                    Program.EmbededListNextPage(item, arg1, arg3, _client, arg2);
                }
                else if (arg3.Emote.Name == "◀")
                {
                    Program.EmbededListLastPage(item, arg1, arg3, _client, arg2);
                }
                else if (arg3.Emote.Name == "🔄")
                {
                    Program.UpdateQueueList(item, arg3, _client);
                }

            }
            
            else if (Program.settings.StopQueueMSGList.Exists(x => x == arg1.Id && arg3.UserId != _client.CurrentUser.Id))
            {
                if (Program.settings.QueueList.Exists(x => x.UserID == arg3.UserId))
                {
                    var msg = arg2.GetMessageAsync(arg1.Id).Result as IUserMessage;
                    await msg.RemoveReactionAsync(arg3.Emote, arg3.User.Value);

                    var user = Program.settings.QueueList.Single(x => x.UserID == arg3.UserId);
                    var DUser = (arg3.Channel as SocketGuildChannel).Users.Single(x=> x.Id == arg3.UserId);
                    var Channel = DUser.VoiceChannel;
                    if (DUser.VoiceChannel != null)
                    {
                        var Room = Program.settings.DebateRooms.Single(x => x.RoomID == DUser.VoiceChannel.Id);
                        if (arg3.Emote.Name == "⏹")
                        {
                            if (user.Speaker)
                            {
                                try
                                {
                                    UnmuteNextUser(Channel, Room);
                                }
                                catch (Exception)
                                {
                                }
                                
                            }
                            user.Speaker = false;
                            user.JoinStamp = DateTime.Now;
                            user.NotInQueue = true;
                            await DUser.ModifyAsync(x => x.Mute = true);
                            Thread.Sleep(150);
                            AutoUpdateMsg( arg3, _client,false, 0);
                        }
                        else if (arg3.Emote.Name == "▶")
                        {
                            user.NotInQueue = false;
                            if (Program.settings.QueueList.Where(x => x.DebateRoom == Room.RoomID).Count() < Room.ActiveSpeakers)
                            {
                                user.Speaker = true;
                                await DUser.ModifyAsync(X => X.Mute = false);
                         
                                await Program.Log(user.UserName + " is now unmuted cause there were free spots when user went into queue", ConsoleColor.Cyan);
                                Thread.Sleep(150);
                                AutoUpdateMsg(arg3, _client,false, 0);
                            }
                            AutoUpdateMsg(arg3, _client,false,0);


                        }
                    }
                }

            }
        }
        public void AutoUpdateMsg( SocketReaction reaction, DiscordSocketClient client, bool IsChannelSpesific, ulong channelID)
        {
            foreach (var lsit in Program.settings.EmbededLists.Where(x=> x.ChannelID_ForUpdate != 0))
            {
                //var item = Program.settings.EmbededLists.LastOrDefault(x => x.ChannelID_ForUpdate != 0);

                try
                {
                    if (IsChannelSpesific)
                    {
                        if (lsit.ChannelID_ForUpdate == channelID)
                        {
                            Program.UpdateQueueList(lsit, reaction, client);
                        }
                    }
                    else if (reaction != null)
                    {
                        var Vchannel = client.GetChannel(lsit.ChannelID_ForUpdate) as SocketVoiceChannel;
                        if (Vchannel.Users.ToList().Exists(x=> x.Id == reaction.UserId))
                        {
                    
                            Program.UpdateQueueList(lsit, reaction, client);
                        }
                    }
                    else
                    {
                        Program.UpdateQueueList(lsit, reaction, client);
                    }
                    
                }
                catch (Exception){}
            }
        }
        public bool equalVoiceState(SocketVoiceState OldState, SocketVoiceState CurrentState)
        {
            if ((OldState.VoiceChannel != null && CurrentState.VoiceChannel != null) && OldState.VoiceChannel.Id != CurrentState.VoiceChannel.Id)
            {
                return false;
            }
            else if (OldState.VoiceChannel == null && CurrentState.VoiceChannel != null)
            {
                return false;
            }
            else if (OldState.VoiceChannel != null && CurrentState.VoiceChannel == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        private async Task _client_UserVoiceStateUpdated(SocketUser User, SocketVoiceState OldState, SocketVoiceState CurrentState)
        {
            if (!IsMuteChange(OldState, CurrentState) && !equalVoiceState(OldState, CurrentState))
            {
                if (OldState.VoiceChannel != null)
                {
                    if (Program.settings.DebateRooms.Exists(x => x.RoomID == OldState.VoiceChannel.Id))
                    {
                        try
                        {
                            UserLeftVoice(OldState.VoiceChannel, OldState.VoiceChannel.Guild.GetUser(User.Id));
                        }
                        catch (Exception)
                        {
                        }

                    }
                }
                Thread.Sleep(100);
                if (CurrentState.VoiceChannel != null)
                {
                    if (Program.settings.DebateRooms.Exists(x => x.RoomID == CurrentState.VoiceChannel.Id))
                    {
                        try
                        {
                            UserJoinedVoice(CurrentState.VoiceChannel, CurrentState.VoiceChannel.Guild.GetUser(User.Id));
                        }
                        catch (Exception) { }

                    }
                }
                Save();
            }
        }

        private void Save()
        {
            File.WriteAllText("settings.json", JsonConvert.SerializeObject(Program.settings, Formatting.Indented));
        }

        public void UserJoinedVoice(SocketVoiceChannel Channel, SocketGuildUser User)
        {
            foreach (var role in Program.settings.AutoAddRoles)
            {
                try
                {
                    var ReRole = Channel.Guild.GetRole(role);
                    User.AddRoleAsync(ReRole);
                }
                catch (Exception){}
            }
            var Room = Program.settings.DebateRooms.Single(x => x.RoomID == Channel.Id);
            Program.Queue item = new Program.Queue();
            item.DebateRoom = Room.RoomID;
            item.VoiceChannelID = Channel.Id;
            item.UserID = User.Id;
            item.UserName = User.Username;
            item.GuildID = Channel.Guild.Id;
            item.JoinStamp = DateTime.Now;
            item.NotInQueue = false;
            foreach (var ExistUsers in Program.settings.QueueList.Where(x => x.UserID == User.Id))
            {
                Program.settings.QueueList.Remove(ExistUsers);
            }
            var i = 0;
            foreach (var user in Channel.Users)
            {
                if (!user.IsMuted)
                {


                    if (user.Id != item.UserID)
                    {
                        i++;
                    }
                }
                

            }
           
            Thread.Sleep(25);
            Program.settings.QueueList.Add(item);
            Program.Log(User.Username + " added to queuelist", ConsoleColor.Cyan);

            var PplInSameRoom = Program.settings.QueueList.Where(x => x.DebateRoom == Room.RoomID);
            if (i > Room.ActiveSpeakers ||  i == Room.ActiveSpeakers)
            {
                var mutedppl = PplInSameRoom.Where(x => x.NotInQueue = true);
                item.Speaker = false;
                User.ModifyAsync(X => X.Mute = true);
                Program.Log(User.Username + " is now Servermuted", ConsoleColor.Cyan);
            }
            else
            {
                item.Speaker = true;
            }
            foreach (var queue in Program.settings.QueueList.Where(x=> x.NotInQueue))
            {
                queue.JoinStamp = DateTime.Now;
            }
            foreach (var lala in Program.settings.QueueList.Where(x => x.DebateRoom == Channel.Id && x.GuildID == Channel.Guild.Id).OrderBy(a => a.JoinStamp))
            {
                try
                {
                    if (Channel.Guild.GetUser(item.UserID).VoiceChannel.Id == Channel.Id)
                    {
                        
                    }
                }
                catch (Exception) { Program.settings.QueueList.Remove(item); }
            }
            AutoUpdateMsg(null,_client,true, Channel.Id);
        }
        public void UserLeftVoice(SocketVoiceChannel Channel, SocketGuildUser User)
        {

            foreach (var role in Program.settings.AutoAddRoles)
            {
                try
                {
                    var ReRole = Channel.Guild.GetRole(role);
                    User.RemoveRoleAsync(ReRole);
                }
                catch (Exception) { }
            }

            var Room = Program.settings.DebateRooms.SingleOrDefault(x => x.RoomID == Channel.Id);
            var queueuser = Program.settings.QueueList.SingleOrDefault(x => x.UserID == User.Id);

            Program.settings.QueueList.Remove(queueuser);
            Program.Log(User.Username + " removed from queuelist", ConsoleColor.Cyan);


            if (queueuser.Speaker)
            {
                try
                {
                    UnmuteNextUser(Channel, Room);
                }
                catch (Exception)
                {
                }
            }
            if (User.IsMuted)
            {
                User.ModifyAsync(X => X.Mute = false);
                Program.Log(User.Username + " is now Un-Servermuted", ConsoleColor.Cyan);
            }
            AutoUpdateMsg(null, _client,true,Channel.Id);
        }
        public void UnmuteNextUser(SocketVoiceChannel Channel, DebateRoomValues Room)
        {
            var NextUnmuteUser = Channel.Guild.GetUser(Program.settings.QueueList.Where(x => x.DebateRoom == Room.RoomID && !x.Speaker && !x.NotInQueue).OrderBy(a => a.JoinStamp).FirstOrDefault().UserID);
            var QueueNextUnmuteUSer = Program.settings.QueueList.SingleOrDefault(x => x.UserID == NextUnmuteUser.Id);

            QueueNextUnmuteUSer.Speaker = true;
            NextUnmuteUser.ModifyAsync(x => x.Mute = false);
            Program.Log(NextUnmuteUser.Username + " is now Un-Servermuted cause of queue spot", ConsoleColor.Cyan);
        }
    }
}
