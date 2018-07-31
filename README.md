# Discord-Bot-DirectLine-CSharp
The sample of Discord app interactive with Microsoft Bot Framework.


This project can receive text message from DiscordApp and redirect message to Microsoft Bot Framework by Direct Line interface. Correspondingly, it also retrieve response message from Microsoft Bot Framework and passthrough to DiscordApp. By deploying binary of this project, you can add a chat bot that created by Microsoft Bot Framework for DiscordApp.


## Add a chat bot in DiscordApp

To achieve this, please follow this article: [Making a Ping-Pong Bot](https://github.com/RogueException/Discord.Net/blob/dev/docs/guides/getting_started/intro.md). Finally, you will get APP BOT TOKEN from below UI:
![](https://github.com/RogueException/Discord.Net/blob/dev/docs/guides/getting_started/images/intro-token.png?raw=true)

## Enable Bot Direct Line for a Bot

The Bot Direct Line is a set of public REST API that can help developers send/receive messages to/from backend Bot Framework service. To enable direct line feature for a bot, you have to follow below article: [Connect a bot to Direct Line](https://docs.microsoft.com/en-us/azure/bot-service/bot-service-channel-connect-directline?view=azure-bot-service-3.0)

When finshed above steps, you will get BotId and Direct Line Secret key from setting UI:

![](https://docs.microsoft.com/en-us/azure/bot-service/media/bot-service-channel-connect-directline/directline-copykey.png?view=azure-bot-service-3.0)

## Configuration

This application has only one configuration file with name appsettings.json which is under application folder. In this file, you need to set values of DiscordBotSecret(APP BOT TOKEN), DirectlineSecret(Direct Line Secret Key) and BotId.

## Running application

This application depends on .NET Core 2.0. Please install [.NET Core 2.0 runtime](https://www.microsoft.com/net/download/) before start application.


Application start command in application folder:

> dotnet DiscordBotDirectline.dll

When application start successfully, the chat bot will display online status.

## Reference:
+ [Connect a bot to Direct Line](https://docs.microsoft.com/en-us/azure/bot-service/bot-service-channel-connect-directline?view=azure-bot-service-3.0)
+ [Making a Ping-Pong bot](https://github.com/RogueException/Discord.Net/blob/dev/docs/guides/getting_started/intro.md)
+ [Direct Line Bot Sample](https://github.com/Microsoft/BotBuilder-Samples/tree/master/CSharp/core-DirectLine)
+ [Discord.NET](https://github.com/RogueException/Discord.Net/blob/dev/docs/guides/getting_started/installing.md)
