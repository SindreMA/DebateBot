using Discord;
using Discord.Addons.EmojiTools;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace DebateBot
{

    class TurnedBaseDebateMode
    {
        public static EmbedBuilder SendEmbededMessage(string title, string description)
        {
            EmbedBuilder eb = new EmbedBuilder();
            eb.WithColor(new Color(1f, 1f, 1f));
            eb.Title = title;
            eb.Description = description;
            return eb;
        }
        public static EmbedBuilder Rebuild(IUserMessage bmsg)
        {
            var msg = bmsg;
            EmbedBuilder eb = new EmbedBuilder();
            var oldEmbed = msg.Embeds.ToList()[0];

            eb.WithColor(msg.Embeds.ToList()[0].Color.Value);
            eb.Title = msg.Embeds.ToList()[0].Title;
            foreach (var fields in msg.Embeds.ToList()[0].Fields)
            {

                if (fields.Inline)
                {
                    eb.AddField(fields.Name, fields.Value, true);
                }
                else
                {
                    eb.AddField(fields.Name, fields.Value);
                }

            }
            if (oldEmbed.Description != null)
            {
                eb.Description = msg.Embeds.ToList()[0].Description;
            }
            if (oldEmbed.Image != null)
            {
                eb.ImageUrl = oldEmbed.Image.Value.Url;
            }
            if (oldEmbed.Thumbnail != null)
            {
                eb.ThumbnailUrl = oldEmbed.Thumbnail.Value.Url;
            }
            return eb;
        }
        private static DiscordSocketClient _client;
        private CommandService _service;
        public class TBMode
        {
            public List<ulong> SpeakQueue { get; set; }
            public List<ulong> MutedUsers { get; set; }
            public ulong MessageID { get; set; }
            public ulong Creator { get; set; }
            public int SpeakTime { get; set; }
            public ulong CurrentSpeaker { get; set; }
            public DateTime CurrentSpeaker_Start { get; set; }
            public ulong VoiceChannel { get; set; }
            public ulong GuildID { get; set; }
            public ulong TextChannel { get; set; }

        }
        public Timer timer = new Timer();
        public static List<TBMode> TBModeMsg = new List<TBMode>();
        public TurnedBaseDebateMode(DiscordSocketClient client)
        {
            timer.Interval = 1000;
            timer.Enabled = true;
            timer.Start();
            timer.Elapsed += Timer_Elapsed;
            _client = client;
            _service = new CommandService();
            _service.AddModulesAsync(Assembly.GetEntryAssembly());
            _client.ReactionAdded += _client_ReactionAdded;
        }

        public static void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var item in TBModeMsg)
            {
                DateTime when = item.CurrentSpeaker_Start;
                TimeSpan ts = DateTime.Now.Subtract(when);
                var Time = (int)ts.TotalSeconds;
                if (item.CurrentSpeaker != 0 &&  Time > item.SpeakTime)
                {
                    var msg = _client.GetGuild(item.GuildID).GetTextChannel(item.TextChannel).GetMessageAsync(item.MessageID).Result as IUserMessage;

                    NextOrUnmute( item,msg,item.CurrentSpeaker);
                }
            }
        }

        private async Task _client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (TBModeMsg.Exists(x => x.MessageID == arg1.Id) && arg3.UserId != _client.CurrentUser.Id && !arg3.User.Value.IsBot)
            {
                var item = TBModeMsg.Single(x => x.MessageID == arg1.Id);
                var msg = arg2.GetMessageAsync(arg1.Id).Result as IUserMessage;
                await msg.RemoveReactionAsync(arg3.Emote, arg3.User.Value);

                if (arg3.Emote.Name == "➕")
                {
                    if (item.CurrentSpeaker == 0)
                    {
                        NewSpeaker(item,msg,arg3.UserId);
                    }
                    else
                    {
                        item.SpeakQueue.Add(arg3.UserId);
                    }
                }
                else if (arg3.Emote.Name == "➖")
                {
                    if (item.SpeakQueue.Exists(x=> x == arg3.UserId))
                    {
                        item.SpeakQueue.Remove(arg3.UserId);
                        UpdateMsg(msg, item);
                    }
                    else if (item.CurrentSpeaker == arg3.UserId)
                    {
                        NextOrUnmute(item, msg,arg3.UserId);
                        UpdateMsg(msg, item);
                    }
                    else
                    {

                    }
                }
                else if (arg3.Emote.Name == "✖")
                {
                    if (arg3.UserId == item.Creator)
                    {
                        foreach (var user in item.MutedUsers)
                        {
                            var guser = _client.GetGuild(item.GuildID).GetUser(user);
                            await guser.ModifyAsync(x => x.Mute = false);
                        }
                        TBModeMsg.Remove(item);
                        var eb = Rebuild(msg);
                        eb.Description = "Stopped! \nUse .tbmode (x) to create a new one!";
                        await msg.ModifyAsync(x => x.Embed = eb.Build());
                    }
                }

            }
        }

        private static void NextOrUnmute(TBMode item, IUserMessage msg, ulong UserID)
        {
            var olduser = item.CurrentSpeaker;
            item.CurrentSpeaker = 0;
            var voicechannel = _client.GetChannel(item.VoiceChannel) as SocketVoiceChannel;

            if (item.SpeakQueue.Count != 0)
            {
                var guser = _client.GetGuild(item.GuildID).GetUser(olduser);
                guser.ModifyAsync(x => x.Mute = true);

                NewSpeaker(item, msg, UserID);

            }
            else
            {
                foreach (var user in item.MutedUsers)
                {
                    var guser = _client.GetGuild(item.GuildID).GetUser(user);
                    guser.ModifyAsync(x => x.Mute = false);
                }
            }
        }

        private static void NewSpeaker(TBMode item, IUserMessage msg, ulong UserId)
        {
            var guser = _client.GetGuild(item.GuildID).GetUser(UserId);
            var voicechannel = _client.GetChannel(item.VoiceChannel) as SocketVoiceChannel;
            if (item.MutedUsers.Count == 0 && item.CurrentSpeaker == 0)
            {
                PlayMessage($@"{guser.Username} is up, you have {item.SpeakTime} seconds!");
                foreach (var VoiceUser in voicechannel.Users)
                {
                    if (VoiceUser.Id != UserId)
                    {
                        VoiceUser.ModifyAsync(x => x.Mute = true);
                        item.MutedUsers.Add(VoiceUser.Id);
                    }
                }
                item.CurrentSpeaker = UserId;
                item.CurrentSpeaker_Start = DateTime.Now;
                item.SpeakQueue.Remove(UserId);
            }
            else if (item.MutedUsers.Count != 0 && item.CurrentSpeaker == UserId)
            {
                PlayMessage($@"{guser.Username} is up, you have {item.SpeakTime} seconds!");
                guser.ModifyAsync(x => x.Mute = true);
                item.MutedUsers.Add(guser.Id);
                item.CurrentSpeaker = 0;


                var user = item.SpeakQueue.First();
                var Firstguser = _client.GetGuild(item.GuildID).GetUser(UserId);
                Firstguser.ModifyAsync(x => x.Mute = false);
                item.MutedUsers.Remove(user);
                item.CurrentSpeaker = user;
                item.CurrentSpeaker_Start = DateTime.Now;
                
            }
            else
            {
                item.SpeakQueue.Add(UserId);
                UpdateMsg(msg, item);
            }
            
        }

        private static void UpdateMsg(IUserMessage msg, TBMode item)
        {
            var eb = Rebuild(msg);
            string usersinqueue = "";
            foreach (var stringuser in item.MutedUsers)
            {
                var user = _client.GetUser(stringuser);
                usersinqueue = usersinqueue + user.Username + Environment.NewLine;
            }
            eb.Description = "Click + to join queue\nClick - to leave queue\nClick X to stop\n\n Speak Queue```" + usersinqueue + "```";
            msg.ModifyAsync(x => x.Embed = eb.Build());
        }

        private static void PlayMessage(string v)
        {
        }
        private void Save()
        {
            File.WriteAllText("settings.json", JsonConvert.SerializeObject(Program.settings, Formatting.Indented));
        }
       
        
    }
    public class TurnedBaseDebateModeCommands : ModuleBase<SocketCommandContext>
    {
        [Command("TBMode")]
        [Alias("tbm")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task TBMode(int SpeakTime)
        {
            var user = (Context.User as SocketGuildUser);
            if (user.VoiceChannel != null)
            {
                var eb = TurnedBaseDebateMode.SendEmbededMessage($@"Turned based Debate mode for {(Context.User as SocketGuildUser).VoiceChannel.Name}", "Click + to join queue\nClick - to leave queue\nClick X to stop\n\n Speak Queue```Nobody```");
                var msg = Context.Channel.SendMessageAsync("", false, eb.Build()).Result;
             
                await msg.AddReactionAsync(EmojiExtensions.FromText(":heavy_plus_sign:"));
                await msg.AddReactionAsync(EmojiExtensions.FromText(":heavy_minus_sign:"));
                await msg.AddReactionAsync(EmojiExtensions.FromText(":heavy_multiplication_x:"));
                TurnedBaseDebateMode.TBMode item = new TurnedBaseDebateMode.TBMode();
                item.Creator = Context.User.Id;
                item.MessageID = msg.Id;
                item.SpeakTime = SpeakTime;
                item.MutedUsers = new List<ulong>();
                item.SpeakQueue = new List<ulong>();
                item.VoiceChannel = user.VoiceChannel.Id;
                item.TextChannel = Context.Channel.Id;
                item.GuildID = Context.Guild.Id;
                TurnedBaseDebateMode.TBModeMsg.Add(item);
            }
        }
        [Command("t")]
        public async Task test()
        {
            TurnedBaseDebateMode.Timer_Elapsed(null, null);
        }
    }

}
