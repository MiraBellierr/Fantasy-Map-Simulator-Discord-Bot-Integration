using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

class DiscordController
{
    private DiscordSocketClient? _client;
    private const string QueueFilePath = @"C:/data/commands_queue.txt";
    private const string FeedbackFilePath = @"C:/data/feedback.txt";
    private const ulong FeedbackChannelId = 1282597999658143776;
    private bool _start = false;

    static void Main(string[] args) => new DiscordController().RunBotAsync().GetAwaiter().GetResult();

    public async Task RunBotAsync()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig { GatewayIntents = GatewayIntents.All });
        _client.Log += Log;

        string token = new ConfigurationBuilder().AddUserSecrets<DiscordController>().Build()["TOKEN"];
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _client.MessageReceived += HandleMessageReceived;

        _ = Task.Run(async () =>
        {
            while (true)
            {
                await ProcessFeedback();
                await Task.Delay(1000);
            }
        });

        await Task.Delay(-1);
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg);
        return Task.CompletedTask;
    }

    private async Task HandleMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        string authorName = message.Author.GlobalName;

        if (message.Author.Id == 548050617889980426)
        {
            if (message.Content.StartsWith("!startgame"))
            {
                ClearFeedbackFile();
                _start = true;
                QueueCommand("start");
            }

            if (message.Content.StartsWith("!stopgame"))
                await message.Channel.SendMessageAsync("The game is stopped.");
        }

        if (message.Content == "!hi")
            await message.Channel.SendMessageAsync("!register, !startwar <country>, !country");

        if (_start)
        {
            if (message.Content.StartsWith("!register"))
                QueueCommand($"register {authorName}");

            if (message.Content == "!country")
                QueueCommand($"info {authorName}");

            if (message.Content.StartsWith("!startwar"))
            {
                string[] parts = message.Content.Split(' ');
                if (parts.Length == 2)
                {
                    QueueCommand($"{authorName} ForceStartWar {parts[1]}");
                    await message.Channel.SendMessageAsync($"Added to queue: {authorName} will start a war with {parts[1]}");
                }
                else
                    await message.Channel.SendMessageAsync("Invalid command format. Use !startwar <user>");
            }
        }
    }

    private async Task ProcessFeedback()
    {
        if (!File.Exists(FeedbackFilePath)) return;

        string[] lines = File.ReadAllLines(FeedbackFilePath);
        if (lines.Length == 0) return;

        if (_client?.GetChannel(FeedbackChannelId) is not IMessageChannel channel)
        {
            Console.WriteLine($"Channel with ID {FeedbackChannelId} not found.");
            return;
        }

        foreach (string line in lines)
        {
            if (_start && !string.IsNullOrWhiteSpace(line))
            {
                if (line.StartsWith("Name:"))
                {
                    string formattedDescription = string.Join("\n", line.Split('-'));
                    await channel.SendMessageAsync($"```\n{formattedDescription}\n```");
                }
                else
                    await channel.SendMessageAsync(line);
            }
        }

        ClearFeedbackFile();
    }

    private static void QueueCommand(string command) => File.AppendAllText(QueueFilePath, command + Environment.NewLine);

    private static void ClearFeedbackFile() => File.WriteAllText(FeedbackFilePath, string.Empty);
}
