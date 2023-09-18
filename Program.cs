using Discord;
using Discord.Addons.EmojiTools;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebateBot
{

    class Program
    {
        static void Main(string[] args)
            => new Program().StartAsync().GetAwaiter().GetResult();
        private CommandHandler _handler;
        private DiscordSocketClient _client;
        public async Task StartAsync()
        {
            await Log("Setting up the bot", ConsoleColor.Green);
            _client = new DiscordSocketClient();
            new CommandHandler(_client);
            new Events(_client);
            //new TurnedBaseDebateMode(_client);
            try
            {
                string json = File.ReadAllText("settings.json");
                settings = JsonConvert.DeserializeObject<Settings>(json);

                if (settings.StopQueueMSGList == null)
                {
                    settings.StopQueueMSGList = new List<ulong>();
                }
                if (settings.AutoAddRoles == null)
                {
                    settings.AutoAddRoles = new List<ulong>();
                }

            }
            catch (Exception)
            {
                settings = new Settings();
                settings.DebateRooms = new List<DebateRoomValues>();
                settings.QueueList = new List<Queue>();
                settings.EmbededLists = new List<EmbedList>();
                settings.StopQueueMSGList = new List<ulong>();

            }
            await Log("Logging in...", ConsoleColor.Green);
            await _client.LoginAsync(TokenType.Bot, "##################################################################");
            await Log("Connecting...", ConsoleColor.Green);
            await _client.StartAsync();
            _client.GuildAvailable += _client_GuildAvailable;
            await Task.Delay(-1);
            _handler = new CommandHandler(_client);

        }
        public class Settings
        {
            public List<Queue> QueueList { get; set; }
            public List<DebateRoomValues> DebateRooms { get; set; }
            public List<EmbedList> EmbededLists { get; set; }
            public List<ulong> StopQueueMSGList { get; set; }
            public List<ulong> AutoAddRoles { get; set; }
        }
        public class Queue
        {
            public ulong GuildID { get; set; }
            public bool Speaker { get; set; }
            public bool NotInQueue { get; set; }
            public ulong UserID { get; set; }
            public ulong VoiceChannelID { get; set; }
            public ulong DebateRoom { get; set; }
            public string UserName { get; set; }
            public DateTime JoinStamp { get; set; }
        }
        public class DebateRoomValues
        {
            public ulong GuildID { get; set; }
            public ulong RoomID { get; set; }
            public int ActiveSpeakers { get; set; }
        }
        public class EmbedList
        {
            public ulong messageID { get; set; }
            public ulong msg_channelID { get; set; }
            public List<string> list { get; set; }
            public int from { get; set; }
            public int to { get; set; }
            public ulong ChannelID_ForUpdate { get; set; }
        }

        public static Settings settings = new Program.Settings();

        public static EmbedBuilder SendEmbededMessage(string title, string description)
        {
            EmbedBuilder eb = new EmbedBuilder();
            eb.WithColor(new Color(1f, 1f, 1f));
            eb.Title = title;
            eb.Description = description;
            return eb;
        }
        public static void Updatelist(EmbedList list, SocketReaction reaction, DiscordSocketClient client)
        {

            var msg = (client.GetChannel(list.msg_channelID) as SocketTextChannel).GetMessageAsync(list.messageID).Result as IUserMessage;
            try
            {
                msg.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
            }
            catch (Exception) { }

            var eb = Rebuild(msg);
            int from = list.from;
            int to = list.to;
            var description = "Empty";
            if (list.list.Count != 0)
            {
                description =
                    "```";
                if ((list.list.Where(x => (list.list.IndexOf(x) > from || list.list.IndexOf(x) == from) && list.list.IndexOf(x) < to)).Count() != 0)
                {
                    foreach (var item in list.list.Where(x => (list.list.IndexOf(x) > from || list.list.IndexOf(x) == from) && list.list.IndexOf(x) < to))
                    {
                        int id = list.list.IndexOf(item) + 1;
                        description = description + id + ". " + item + Environment.NewLine;
                    }
                    description = description + "```";



                }
            }
            eb.Description = description;
            msg.ModifyAsync(x => x.Embed = eb.Build());
        }
        public static void UpdateQueueList(EmbedList list, SocketReaction reaction, DiscordSocketClient client)
        {
            EmbedList NewList = new EmbedList();
            NewList.msg_channelID = list.msg_channelID;
            NewList.from = list.from;
            NewList.messageID = list.messageID;
            NewList.to = list.to;
            var Vchannel = client.GetChannel(list.ChannelID_ForUpdate) as SocketVoiceChannel;


            List<string> que = new List<string>();
            foreach (var item in Program.settings.QueueList.Where(x => x.DebateRoom == Vchannel.Id && x.GuildID == Vchannel.Guild.Id).OrderBy(a => a.JoinStamp))
            {
                try
                {
                    if (Vchannel.Guild.GetUser(item.UserID).VoiceChannel.Id == Vchannel.Id)
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
                }
                catch (Exception) { Program.settings.QueueList.Remove(item); }
            }
            NewList.list = que;

            Updatelist(NewList, reaction, client);
        }
        public static void CreateEmbededList(string titel, List<string> list, SocketCommandContext context, ulong ChannelID)
        {

            int from = 0;
            int to = 10;
            var Titel = titel;
            var description =
                "```";
            if (list.Count == 0)
            {
                context.Channel.SendMessageAsync("", false, SendEmbededMessage("The list is empty!", "").Build());
            }
            else
            {


                foreach (var item in list.Where(x => (list.IndexOf(x) > from || list.IndexOf(x) == from) && list.IndexOf(x) < to))
                {
                    int id = list.IndexOf(item) + 1;
                    description = description + id + ". " + item + Environment.NewLine;
                }
                description = description + "```";

                var msg = context.Channel.SendMessageAsync("", false, SendEmbededMessage(Titel, description).Build()).Result;
                if (list.Count > 10)
                {
                    msg.AddReactionAsync(EmojiExtensions.FromText("arrow_backward"));
                    msg.AddReactionAsync(EmojiExtensions.FromText("arrow_forward"));

                }

                if (settings.EmbededLists == null)
                {
                    settings.EmbededLists = new List<EmbedList>();
                }
                EmbedList Elist = new EmbedList();
                Elist.list = new List<string>();
                Elist.list = list;
                Elist.messageID = msg.Id;
                Elist.from = from;
                Elist.to = to;
                Elist.msg_channelID = msg.Channel.Id;
                if (ChannelID != 0)
                {
                    Elist.ChannelID_ForUpdate = ChannelID;
                    msg.AddReactionAsync(EmojiExtensions.FromText("arrows_counterclockwise"));


                }
                settings.EmbededLists.Add(Elist);
                Save();
            }
        }
        private static void Save()
        {
            File.WriteAllText("settings.json", JsonConvert.SerializeObject(Program.settings, Formatting.Indented));
        }
        public static void EmbededListNextPage(EmbedList list, Discord.Cacheable<Discord.IUserMessage, ulong> omsg, SocketReaction reaction, DiscordSocketClient client, ISocketMessageChannel channel)
        {

            var msg = channel.GetMessageAsync(omsg.Id).Result as IUserMessage;
            msg.RemoveReactionAsync(reaction.Emote, reaction.User.Value);

            var eb = Rebuild(msg);
            int from = list.from + 10;
            int to = list.to + 10;
            var description =
                "```";
            if ((list.list.Where(x => (list.list.IndexOf(x) > from || list.list.IndexOf(x) == from) && list.list.IndexOf(x) < to)).Count() != 0)
            {
                foreach (var item in list.list.Where(x => (list.list.IndexOf(x) > from || list.list.IndexOf(x) == from) && list.list.IndexOf(x) < to))
                {
                    int id = list.list.IndexOf(item) + 1;
                    description = description + id + ". " + item + Environment.NewLine;
                }
                description = description + "```";
                eb.Description = description;
                msg.ModifyAsync(x => x.Embed = eb.Build());

                list.from = from;
                list.to = to;
            }

        }
        public static void EmbededListLastPage(EmbedList list, Discord.Cacheable<Discord.IUserMessage, ulong> omsg, SocketReaction reaction, DiscordSocketClient client, ISocketMessageChannel channel)
        {

            var msg = channel.GetMessageAsync(omsg.Id).Result as IUserMessage;
            msg.RemoveReactionAsync(reaction.Emote, reaction.User.Value);

            var eb = Rebuild(msg);
            int from = list.from - 10;
            int to = list.to - 10;
            var description =
                "```";
            if ((list.list.Where(x => (list.list.IndexOf(x) > from || list.list.IndexOf(x) == from) && list.list.IndexOf(x) < to)).Count() != 0)
            {
                foreach (var item in list.list.Where(x => (list.list.IndexOf(x) > from || list.list.IndexOf(x) == from) && list.list.IndexOf(x) < to))
                {
                    int id = list.list.IndexOf(item) + 1;
                    description = description + id + ". " + item + Environment.NewLine;
                }
                description = description + "```";
                eb.Description = description;
                msg.ModifyAsync(x => x.Embed = eb.Build());

                list.from = from;
                list.to = to;
            }
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
        private async Task _client_GuildAvailable(SocketGuild arg)
        {

            await Log(arg.Name + " Connected!", ConsoleColor.Green);
        }
        public static async Task Log(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(DateTime.Now + " : " + message, color);
            Console.ResetColor();
        }

    }

}
