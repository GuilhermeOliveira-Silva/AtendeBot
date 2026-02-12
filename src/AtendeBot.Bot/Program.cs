using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using DotNetEnv;

// Depois vamos mover isso pra variável de ambiente (mais seguro)
DotNetEnv.Env.Load();

var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");

if (string.IsNullOrEmpty(botToken))
{
    Console.WriteLine("❌ Token não encontrado! Crie um arquivo .env com TELEGRAM_BOT_TOKEN=seu_token");
    return;
}
var botClient = new TelegramBotClient(botToken);

Console.WriteLine("🤖 AtendeBot está rodando! Pressione Ctrl+C para parar.");

// Configuração do bot
using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    errorHandler: HandleErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

// Mostra o nome do bot no terminal
var me = await botClient.GetMe();
Console.WriteLine($"✅ Bot conectado como: @{me.Username}");

// Mantém o programa rodando
Console.ReadLine();
cts.Cancel();

// === FUNÇÃO QUE RECEBE AS MENSAGENS ===
async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token)
{
    // Só processa mensagens de texto
    if (update.Message is not { } message)
        return;
    if (message.Text is not { } texto)
        return;

    var nomeCliente = message.From?.FirstName ?? "Cliente";
    var chatId = message.Chat.Id;

    Console.WriteLine($"📩 Mensagem de {nomeCliente}: {texto}");

    // Responde de acordo com o que o cliente mandou
    if (texto == "/start")
    {
        await client.SendMessage(
            chatId: chatId,
            text: $"Olá, {nomeCliente}! 👋\n\n"
                + "Bem-vindo ao AtendeBot! 🤖\n\n"
                + "Comandos disponíveis:\n"
                + "/cardapio - Ver nosso cardápio\n"
                + "/pedir - Fazer um pedido\n"
                + "/status - Ver status do seu pedido",
            cancellationToken: token
        );
    }
    else if (texto == "/cardapio")
    {
        await client.SendMessage(
            chatId: chatId,
            text: "🍽️ *CARDÁPIO*\n\n"
                + "1️⃣ X-Burger - R$ 18,00\n"
                + "2️⃣ X-Salada - R$ 20,00\n"
                + "3️⃣ X-Bacon - R$ 22,00\n"
                + "4️⃣ Coca-Cola Lata - R$ 6,00\n"
                + "5️⃣ Suco Natural - R$ 8,00\n\n"
                + "Para pedir, envie /pedir",
            parseMode: ParseMode.Markdown,
            cancellationToken: token
        );
    }
    else if (texto == "/pedir")
    {
        await client.SendMessage(
            chatId: chatId,
            text: "📝 Ótimo! Vamos montar seu pedido.\n\n"
                + "Por enquanto, me diga o que deseja "
                + "e em breve teremos o sistema completo! 😊\n\n"
                + "Exemplo: 2x X-Burger, 1x Coca-Cola",
            cancellationToken: token
        );
    }
    else
    {
        await client.SendMessage(
            chatId: chatId,
            text: $"Recebi sua mensagem: \"{texto}\"\n\n"
                + "Use /start para ver os comandos disponíveis. 😉",
            cancellationToken: token
        );
    }
}

// === FUNÇÃO QUE TRATA ERROS ===
Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken token)
{
    Console.WriteLine($"❌ Erro: {exception.Message}");
    return Task.CompletedTask;
}