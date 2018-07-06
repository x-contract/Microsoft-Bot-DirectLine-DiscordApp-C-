/**************************************************************************************************************************
 * The X-Contract foundation is a organzation of dedicating on smart contract evolution.
 * This application can convert discord meessage to bot framework message activity. 
 * It can help Microsoft Bot interactive with Discord App.
 * 
 * X-Contract website: http://www.jarvisplus.com
 * Author： Michael Li
 * Date: 23/JUNE/2018
 * ========================================================================================================================
 */

using Discord;
using Discord.WebSocket;
using Microsoft.Bot.Connector.DirectLine;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBotDirectline
{
    class Program
    {
        #region --- Private members ---

        private static DiscordSocketClient _discordClient = null;
        private static DirectLineClient _directlineClinet = null;
        private static readonly string _theChannelName = "DiscordApp";
        private static string _botId = string.Empty;
        private static AutoResetEvent _autoEvent = new AutoResetEvent(false);
        private static Dictionary<ulong, ChannelContext> _conversationManager = new Dictionary<ulong, ChannelContext>();
        #endregion

        #region --- Methods ---

        public static IConfigurationRoot Config { get; private set; }
        public static void Main(string[] args)
        {
            // Retrieve configuration from Json file.
            Config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
            _botId = Config["BotId"];

            // Initialize Discord.NET client & DirectLine client.
            DirectlineInit();
            DiscordInit().GetAwaiter().GetResult();
            //while (true)
            //{
            //    string input = Console.ReadLine();
            //    if ("exit" == input.ToLower().Trim())
            //    {
            //        Console.WriteLine("Exiting....");
            //        ShutdownAllBotThread();
            //        break;
            //    }
            //}
            //DiscordUnInit().GetAwaiter();
            // The reason of use AutoEvent replace Console.ReadLine() is 
            // to make the program run in the background without UI.
            // Application will be killed by OS.
            _autoEvent.WaitOne();
        }
        private static async Task DiscordInit()
        {
            _discordClient = new DiscordSocketClient();
            _discordClient.Log += DiscordClient_Log;
            _discordClient.MessageReceived += DiscordClient_MessageReceived;
       
            await _discordClient.LoginAsync(TokenType.Bot, Config["DiscordBotSecret"]);
            await _discordClient.StartAsync();
        }

        private static async Task DiscordUnInit()
        {
            _discordClient.Log -= DiscordClient_Log;
            _discordClient.MessageReceived -= DiscordClient_MessageReceived;

            await _discordClient.StopAsync();
            await _discordClient.LogoutAsync();
            _discordClient = null;
        }
        private static void DirectlineInit()
        {
            _directlineClinet = new DirectLineClient(Config["DirectlineSecret"]);
        }
        private static async Task SendMessageToBotAsync(string conversationId, SocketMessage msg)
        {
            if (null == msg)
                return;
            // Set channel object.
            _conversationManager[msg.Channel.Id].Channel = msg.Channel;
            Activity act = new Activity();
            act.ChannelId = _theChannelName;
            act.From = new ChannelAccount(msg.Author.Id.ToString(), msg.Author.Username);
            act.Text = msg.Content;
            if (MessageSource.System == msg.Source)
                act.Type = ActivityTypes.ConversationUpdate;
            else if (MessageSource.User == msg.Source)
                act.Type = ActivityTypes.Message;
            act.Conversation = new ConversationAccount();
            // Set group flag.
            if ('@' == msg.Channel.Name[0])
                act.Conversation.IsGroup = false;
            else
                act.Conversation.IsGroup = true;
            act.Conversation.Id = conversationId;
            act.Conversation.Name = msg.Channel.Name;

            foreach (Discord.Attachment datt in msg.Attachments)
            {
                string url = datt.Url;            
                Microsoft.Bot.Connector.DirectLine.Attachment att = new Microsoft.Bot.Connector.DirectLine.Attachment();
                // Actually, When user send a file, DiscordApp always upload it to DiscordApp media server
                // and then raise MessageReceived event. If you want to get binary please un-comments below code.
                // But usually, we only need to send media Uri. Because it will improve message transfer speed.
                //
                //HttpClient client = new HttpClient();
                //using (Stream s = await client.GetStreamAsync(url))
                //{
                //    byte[] buff = new byte[s.Length];
                //    s.Read(buff, 0, (int)s.Length);

                //    att.Content = buff;
                //}
                att.ContentType = GetMediaTypeFromUri(url);
                att.ContentUrl = url;
                act.Attachments.Add(att);
            }
            Log.V("Direct to bot...");
            await _directlineClinet.Conversations.PostActivityAsync(conversationId, act);
        }
        private static void ShutdownAllBotThread()
        {
            foreach (ChannelContext context in _conversationManager.Values)
            {
                context.ReceiveMessageClass.ThreadRunning = false;
            }
        }
        private static async Task GenerateConversationAndSendMessage(SocketMessage msg)
        {
            // Create a new conversation.
            var conversation = await _directlineClinet.Conversations.StartConversationAsync();
            if (null != conversation)
            {
                ulong discordChannelId = msg.Channel.Id;
                ChannelContext context = new ChannelContext();
                context.ConversationId = conversation.ConversationId;
                context.Conversation = conversation;
                context.Channel = msg.Channel;
                // Create a thread object reveive message from Bot Framework.
                context.ReceiveMessageClass = new ReceiveMessageFromBotClass(
                    _directlineClinet, _botId, msg.Channel.Id);
                context.ReceiveMessageClass.StartThread();
                // Add context to conversation manager.
                _conversationManager.Add(discordChannelId, context);
                await SendMessageToBotAsync(conversation.ConversationId, msg);
            }
        }
        private static async Task GenerateConversation(SocketChannel channel)
        {
            // Create a new conversation.
            var conversation = await _directlineClinet.Conversations.StartConversationAsync();
            if (null != conversation)
            {
                ulong discordChannelId = channel.Id;
                ChannelContext context = new ChannelContext();
                context.ConversationId = conversation.ConversationId;
                context.Conversation = conversation;
                context.Channel = null;
                // Create a thread object reveive message from Bot Framework.
                context.ReceiveMessageClass = new ReceiveMessageFromBotClass(
                    _directlineClinet, _botId, channel.Id);
                context.ReceiveMessageClass.StartThread();
                // Add context to conversation manager.
                _conversationManager.Add(discordChannelId, context);
            }
        }
        public static ISocketMessageChannel GetDiscordSocketChannel(ulong discordChannelId)
        {
            if (_conversationManager.ContainsKey(discordChannelId))
                return _conversationManager[discordChannelId].Channel;
            else
                return null;
        }
        public static string GetBotConversationId(ulong discordChannelId)
        {
            if (_conversationManager.ContainsKey(discordChannelId))
                return _conversationManager[discordChannelId].ConversationId;
            else
                return string.Empty;
        }
        public static Conversation GetBotConversation(ulong discordChannelId)
        {
            if (_conversationManager.ContainsKey(discordChannelId))
                return _conversationManager[discordChannelId].Conversation;
            else
                return null;
        }
        private static string GetMediaTypeFromUri(string uri)
        {
            if (uri.Contains('.'))
                return uri.Substring(uri.IndexOf(".") + 1, 
                    uri.Length - uri.IndexOf(".") - 1);
            else
                return "binary";
        }
        #endregion

        #region --- Discord Event Handlers ---

        private static async Task DiscordClient_MessageReceived(SocketMessage msg)
        {
            try
            {                
                // exclude Bot.
                if (msg.Author.IsBot || msg.Author.IsWebhook)
                    return;
                Log.V(string.Format("user {0}[{1}] say in group {2}[{3}]  {4}", msg.Author.Username, msg.Author.Id, msg.Channel.Name, msg.Channel.Id, msg.Content));

                string conversationId = GetBotConversationId(msg.Channel.Id);
                if (string.Empty == conversationId)
                {
                    GenerateConversationAndSendMessage(msg);
                }
                else
                    // Send message to Bot Framework.
                    SendMessageToBotAsync(conversationId, msg);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        private static Task DiscordClient_Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
        #endregion

    }

    public static class Log
    {
        public static void V(string text)
        {
            Console.WriteLine(DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss]") + " " + text);
        }
    }

    public class ChannelContext
    {
        public string ConversationId { get; set; }
        public Conversation Conversation { get; set; }
        public ISocketMessageChannel Channel { get; set; }
        public ReceiveMessageFromBotClass ReceiveMessageClass { get; set; }
    }

    public class ReceiveMessageFromBotClass
    {
        public bool ThreadRunning { get; set; }
        public string BotId { get; private set; }
        public DirectLineClient BotClient { get; private set; }
        public ulong ChannelId { get; private set; }
        public Thread ReceiveThread { get; private set; }
        public ReceiveMessageFromBotClass(DirectLineClient botClient, string botId, ulong channelId)
        {
            ThreadRunning = true;
            BotId = botId;
            BotClient = botClient;
            ChannelId = channelId;
        }
        public void StartThread()
        {
            ReceiveThread = new Thread(new ThreadStart(RetrieveMessageFromBot));
            ThreadRunning = true;
            ReceiveThread.Start();
        }
        private void RetrieveMessageFromBot()
        {
            string conversationId = Program.GetBotConversationId(ChannelId);
            string watermark = null;
            while (ThreadRunning)
            {
                try
                {
                    ActivitySet activitySet = BotClient.Conversations
                        .GetActivitiesAsync(conversationId, watermark).GetAwaiter().GetResult();
                    watermark = activitySet?.Watermark;
                    ISocketMessageChannel channel = Program.GetDiscordSocketChannel(ChannelId);
                    var activities = from x in activitySet.Activities
                                     where x.From.Id == BotId
                                     select x;
                    foreach (Activity activity in activities)
                    {
                        Log.V("Bot response: " + GetMessageText(activity));
                        if (null != channel)
                        {
                            channel.SendMessageAsync(GetMessageText(activity));
                            Thread.Sleep(20);
                        }
                        if (activity.Attachments.Count > 0)
                        {
                            foreach (Microsoft.Bot.Connector.DirectLine.Attachment att in activity.Attachments)
                            {
                                if ("application/vnd.microsoft.card.hero" == att.ContentType)
                                    RenderHeroCard(att);
                                else if (IsImage(att.ContentType))
                                {
                                    // Send image to DiscordApp.
                                    channel.SendFileAsync(att.ContentUrl);
                                }
                            }
                        }
                    }
                }
                catch //(Exception e)
                {
                    //Log.V(e.Message);
                    continue;
                }
            }
        }
        private void RenderHeroCard(Microsoft.Bot.Connector.DirectLine.Attachment attachment)
        {
            // It seems that the Discord cannot display any other message type except text and voice.
            Console.WriteLine(attachment.Content.ToString());
            ISocketMessageChannel channel = Program.GetDiscordSocketChannel(ChannelId);
            if (null != channel)
            {
                channel.SendMessageAsync(attachment.Content.ToString());
                Thread.Sleep(20);
            }
        }
        private bool IsImage(string contentType)
        {
            if (contentType.ToLower().IndexOf("image/") >= 0)
                return true;
            else
                return false;
        }
        private string GetMessageText(Activity activity)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(activity.Text);
            if (activity.SuggestedActions.Actions.Count > 0)
            {
                sb.Append("\r\n");
                for (int i = 1; i <= activity.SuggestedActions.Actions.Count; i++)
                {
                    sb.Append(i.ToString() + ". ");
                    sb.Append(activity.SuggestedActions.Actions[i - 1].Value + "\r\n");
                }
            }
            return sb.ToString();
        }
    }
}
