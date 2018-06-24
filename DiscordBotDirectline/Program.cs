/**************************************************************************************************************************
 * The X-Contract foundation is a organzation of dedicating on smart contract evolution.
 * This application can convert discord meessage to bot framework message activity. 
 * It can help Microsoft Bot interactive with Discord App.
 * 
 * X-Contract website: http://www.x-contract.org
 * Author： Michael Li
 * Date: 23/JUNE/2018
 * ========================================================================================================================
 */

using System;
using System.IO;
using Microsoft.Bot.Connector.DirectLine;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace DiscordBotDirectline
{
    class Program
    {
        #region --- Private members ---

        private static DiscordSocketClient _discordClient = null;
        private static DirectLineClient _directlineClinet = null;
        private static string _conversationID = string.Empty;
        private delegate Task RetrieveMessageFromBotHandler();
        private static ISocketMessageChannel _channel = null;
        private static bool _threadRunning = false;
        private static Thread _botThread = null;

        private static Dictionary<string, ChannelContext> _channelMap = null;
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

            _threadRunning = true;

            // Initialize Discord.NET client & DirectLine client.
            DirectlineInit().GetAwaiter().GetResult();
            DiscordInit().GetAwaiter().GetResult();
            while (true)
            {
                string input = Console.ReadLine();
                if ("exit" == input.ToLower().Trim())
                {
                    Console.WriteLine("Exiting....");
                    _threadRunning = false;
                    break;
                }
            }
            DiscordUnInit().GetAwaiter();
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
            _discordClient = null;
        }
        private static async Task DirectlineInit()
        {
            _directlineClinet = new DirectLineClient(Config["DirectlineSecret"]);
            var conversation = await _directlineClinet.Conversations.StartConversationAsync();
            _conversationID = conversation.ConversationId;
            // start a thread to retrieve messages from Bot Framework.
            _botThread = new Thread(async () => await RetrieveMessageFromBot());
            _botThread.Start();
        }
        private static async Task SendMessageToBotAsync(SocketMessage msg)
        {
            if (null == msg)
                return;
            Activity act = new Activity();
            act.ChannelId = msg.Channel.Name;
            act.From = new ChannelAccount(msg.Author.Id.ToString(), msg.Author.Username);
            act.Text = msg.Content;
            act.Type = ActivityTypes.Message;

            _channel = msg.Channel;
            await _directlineClinet.Conversations.PostActivityAsync(_conversationID, act);
        }
        private static Task RetrieveMessageFromBot()
        {
            string watermark = null;
            while (_threadRunning)
            {
                try
                {
                    var activitySet = _directlineClinet.Conversations
                        .GetActivitiesAsync(_conversationID, watermark).GetAwaiter().GetResult();
                    watermark = activitySet?.Watermark;
                    var activities = from x in activitySet.Activities
                                     where x.From.Id == Config["BotId"]
                                     select x;
                    foreach (Activity activity in activities)
                    {
                        Console.WriteLine("Bot response: " + activity.Text);
                        if (null != _channel)
                        {
                            _channel.SendMessageAsync(activity.Text);
                            Thread.Sleep(20);
                        }
                        if (activity.Attachments.Count > 0)
                        {
                            foreach (Microsoft.Bot.Connector.DirectLine.Attachment att in activity.Attachments)
                            {
                                if ("application/vnd.microsoft.card.hero" == att.ContentType)
                                    RenderHeroCard(att);
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
            return Task.CompletedTask;
        }
        private static void RenderHeroCard(
            Microsoft.Bot.Connector.DirectLine.Attachment attachment)
        {
            // It seems that the Discord cannot display any other message type except text and voice.
            Console.WriteLine(attachment.Content.ToString());
            if(null != _channel)
            {
                _channel.SendMessageAsync(attachment.Content.ToString());
                Thread.Sleep(20);
            }
        }
        #endregion

        #region --- Discord Event Handlers ---

        private static async Task DiscordClient_MessageReceived(SocketMessage msg)
        {
            // exclude Bot.
            if (msg.Author.IsBot)   
                return;
            // Send message to Bot Framework.
            SendMessageToBotAsync(msg);
        }
        private static Task DiscordClient_Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
        #endregion

    }

    public class ChannelContext
    {
        public string ChannelName { get; set; }
        public Conversation Conversation { get; set; }
        public ISocketMessageChannel Channel { get; set; }
    }
}
