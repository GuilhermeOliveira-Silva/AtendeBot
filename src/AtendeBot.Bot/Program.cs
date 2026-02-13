using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.EntityFrameworkCore;
using AtendeBot.Bot.Data;
using AtendeBot.Bot.Services;
using DotNetEnv;

// Carrega variáveis do .env
DotNetEnv.Env.Load();

var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION");

if (string.IsNullOrEmpty(botToken))
{
  Console.WriteLine("❌ Token não encontrado! Crie um arquivo .env com TELEGRAM_BOT_TOKEN=seu_token");
  return;
}

if (string.IsNullOrEmpty(connectionString))
{
  Console.WriteLine("❌ Connection string não encontrada! Adicione DATABASE_CONNECTION no .env");
  return;
}

// Configura a conexão com o banco
var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
    .Options;

// Testa a conexão com o banco
try
{
  using var testDb = new AppDbContext(dbOptions);
  testDb.Database.CanConnect();
  Console.WriteLine("✅ Conectado ao banco de dados!");
}
catch (Exception ex)
{
  Console.WriteLine($"❌ Erro ao conectar no banco: {ex.Message}");
  return;
}

var botClient = new TelegramBotClient(botToken);

Console.WriteLine("🤖 AtendeBot está rodando! Pressione Ctrl+C para parar.");

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

var me = await botClient.GetMe();
Console.WriteLine($"✅ Bot conectado como: @{me.Username}");

Console.ReadLine();
cts.Cancel();

// === FUNÇÃO QUE RECEBE AS MENSAGENS ===
async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token)
{
  if (update.Message is not { } message)
    return;
  if (message.Text is not { } texto)
    return;

  var nomeCliente = message.From?.FirstName ?? "Cliente";
  var chatId = message.Chat.Id;

  Console.WriteLine($"📩 Mensagem de {nomeCliente}: {texto}");

  // Cria conexão com o banco pra cada mensagem
  using var db = new AppDbContext(dbOptions);
  var pedidoService = new PedidoService(db);

  string resposta;

  // Se o cliente tá no meio de um pedido, processa por lá
  if (pedidoService.ClienteEstaPedindo(chatId) && !texto.StartsWith("/"))
  {
    resposta = pedidoService.ProcessarMensagem(chatId, texto);
  }
  else
  {
    // Comandos normais
    switch (texto)
    {
      case "/start":
        resposta = $"Olá, {nomeCliente}! 👋\n\n"
            + "Bem-vindo ao AtendeBot! 🤖\n\n"
            + "Comandos disponíveis:\n"
            + "/cardapio - Ver nosso cardápio\n"
            + "/pedir - Fazer um pedido\n"
            + "/status - Ver status do seu pedido";
        break;

      case "/cardapio":
        var itens = db.CardapioItens
            .Where(i => i.ComercioId == 1 && i.Disponivel)
            .ToList();

        if (itens.Count == 0)
        {
          resposta = "😕 Nenhum item disponível no momento.";
          break;
        }

        resposta = "🍽️ *CARDÁPIO*\n\n";
        for (int i = 0; i < itens.Count; i++)
        {
          var item = itens[i];
          resposta += $"{i + 1}️⃣ {item.Nome} - R$ {item.Preco:F2}\n";
          if (!string.IsNullOrEmpty(item.Descricao))
            resposta += $"   _{item.Descricao}_\n";
          resposta += "\n";
        }
        resposta += "Para pedir, envie /pedir";
        break;

      case "/pedir":
        resposta = pedidoService.IniciarPedido(chatId);
        break;

      case "/status":
        resposta = "🔜 Em breve você poderá ver o status aqui!";
        break;

      default:
        resposta = $"Recebi sua mensagem: \"{texto}\"\n\n"
            + "Use /start para ver os comandos disponíveis. 😉";
        break;
    }
  }

  await client.SendMessage(
      chatId: chatId,
      text: resposta,
      parseMode: ParseMode.Markdown,
      cancellationToken: token
  );
}

Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken token)
{
  Console.WriteLine($"❌ Erro: {exception.Message}");
  return Task.CompletedTask;
}