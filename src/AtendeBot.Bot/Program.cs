using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.EntityFrameworkCore;
using AtendeBot.Bot.Data;
using AtendeBot.Bot.Services;
using DotNetEnv;

// Carrega .env para segurança
DotNetEnv.Env.Load();

var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION");
var ownerChatId = long.Parse(Environment.GetEnvironmentVariable("OWNER_CHAT_ID") ?? "0");

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

// Faz o teste de conexão com o banco
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

  Console.WriteLine($"📩 [{chatId}] {nomeCliente}: {texto}");

  // Cria conexão com o banco pra cada mensagem
  using var db = new AppDbContext(dbOptions);
  var pedidoService = new PedidoService(db);

  string resposta;

  // Se o cliente tá no meio de um pedido, processa por lá
if (pedidoService.ClienteEstaPedindo(chatId) && !texto.StartsWith("/"))
{
    resposta = pedidoService.ProcessarMensagem(chatId, texto);

    // Se um pedido foi finalizado, notifica o dono
    if (pedidoService.UltimoPedidoFinalizado is { } pedidoNovo)
    {
        var notificacao = "🔔 *NOVO PEDIDO!*\n\n"
            + $"📋 Pedido #{pedidoNovo.Id}\n"
            + $"💰 Total: R$ {pedidoNovo.Total:F2}\n"
            + $"🚗 {(pedidoNovo.TipoEntrega == "entrega" ? $"Entrega: {pedidoNovo.EnderecoEntrega}" : "Retirada")}\n";

        if (!string.IsNullOrEmpty(pedidoNovo.Observacao))
            notificacao += $"📝 Obs: {pedidoNovo.Observacao}\n";

        notificacao += $"\nPra gerenciar, use /pedidos";

        await client.SendMessage(
            chatId: ownerChatId,
            text: notificacao,
            parseMode: ParseMode.Markdown,
            cancellationToken: token
        );
    }
}
  else
  {
    // Comandos Base do atendemento
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
        var pedidosCliente = db.Pedidos
            .Where(p => p.ClienteTelegramId == chatId.ToString())
            .OrderByDescending(p => p.CriadoEm)
            .Take(3)
            .ToList();

    

        if (pedidosCliente.Count == 0)
        {
          resposta = "📭 Você ainda não fez nenhum pedido.\n\nUse /pedir pra começar!";
          break;
        }

        resposta = "📋 *SEUS ÚLTIMOS PEDIDOS:*\n\n";

        // procura na lista o status do pedido 
        foreach (var p in pedidosCliente)
        {
          var statusEmoji = p.Status switch
          {
            "novo" => "🆕",
            "preparando" => "👨‍🍳",
            "saiu_entrega" => "🛵",
            "pronto_retirada" => "✅",
            "entregue" => "📦",
            "cancelado" => "❌",
            _ => "❓"
          };

          resposta += $"{statusEmoji} *Pedido #{p.Id}*\n";
          resposta += $"   Total: R$ {p.Total:F2}\n";
          resposta += $"   Status: {p.Status}\n";
          resposta += $"   Data: {p.CriadoEm:dd/MM/yyyy HH:mm}\n\n";
        }
        break;

      default:
        resposta = $"Recebi sua mensagem: \"{texto}\"\n\n"
            + "Use /start para ver os comandos disponíveis. 😉";
        break;

        // Verificar pedidos
      case "/pedidos":
    if (chatId != ownerChatId)
    {
        resposta = "❌ Esse comando é só para o dono do estabelecimento.";
        break;
    }

    var pedidosNovos = db.Pedidos
        .Where(p => p.ComercioId == 1 && p.Status != "entregue" && p.Status != "cancelado")
        .OrderByDescending(p => p.CriadoEm)
        .Take(10)
        .ToList();

    if (pedidosNovos.Count == 0)
    {
        resposta = "✅ Nenhum pedido pendente no momento!";
        break;
    }

    resposta = "📋 *PEDIDOS PENDENTES:*\n\n";
    foreach (var p in pedidosNovos)
    {
        var statusEmoji = p.Status switch
        {
            "novo" => "🆕",
            "preparando" => "👨‍🍳",
            "saiu_entrega" => "🛵",
            "pronto_retirada" => "✅",
            _ => "❓"
        };

        resposta += $"{statusEmoji} *Pedido #{p.Id}*\n";
        resposta += $"   Total: R$ {p.Total:F2}\n";
        resposta += $"   Status: {p.Status}\n";
        resposta += $"   Entrega: {(p.TipoEntrega == "entrega" ? p.EnderecoEntrega : "Retirada")}\n";
        if (!string.IsNullOrEmpty(p.Observacao))
            resposta += $"   Obs: {p.Observacao}\n";
        resposta += $"   Data: {p.CriadoEm:dd/MM/yyyy HH:mm}\n\n";
    }

    resposta += "Para atualizar status, envie:\n";
    resposta += "`/atualizar 1 preparando`\n\n";
    resposta += "Status disponíveis:\n";
    resposta += "• preparando\n";
    resposta += "• saiu\\_entrega\n";
    resposta += "• pronto\\_retirada\n";
    resposta += "• entregue\n";
    resposta += "• cancelado";
    break;

    // atualizar status do pedido

    case string s when s.StartsWith("/atualizar"):
    if (chatId != ownerChatId)
    {
        resposta = "❌ Esse comando é só para o dono do estabelecimento.";
        break;
    }

    var partes = texto.Split(' ');
    if (partes.Length != 3)
    {
        resposta = "❌ Formato: /atualizar [id] [status]\nExemplo: `/atualizar 1 preparando`";
        break;
    }

    if (!int.TryParse(partes[1], out int pedidoId))
    {
        resposta = "❌ ID do pedido inválido.";
        break;
    }

    var statusValidos = new[] { "preparando", "saiu_entrega", "pronto_retirada", "entregue", "cancelado" };
    var novoStatus = partes[2];

    if (!statusValidos.Contains(novoStatus))
    {
        resposta = "❌ Status inválido. Use: preparando, saiu_entrega, pronto_retirada, entregue, cancelado";
        break;
    }

    var pedidoAtualizar = db.Pedidos.FirstOrDefault(p => p.Id == pedidoId);
    if (pedidoAtualizar == null)
    {
        resposta = $"❌ Pedido #{pedidoId} não encontrado.";
        break;
    }

    pedidoAtualizar.Status = novoStatus;
    db.SaveChanges();

    resposta = $"✅ Pedido #{pedidoId} atualizado para *{novoStatus}*!";

    // Notifica o cliente sobre a mudança de status
    var clienteChatId = long.Parse(pedidoAtualizar.ClienteTelegramId);
    var statusMsg = novoStatus switch
    {
        "preparando" => "👨‍🍳 Seu pedido está sendo preparado!",
        "saiu_entrega" => "🛵 Seu pedido saiu para entrega!",
        "pronto_retirada" => "✅ Seu pedido está pronto para retirada!",
        "entregue" => "📦 Pedido entregue! Obrigado!",
        "cancelado" => "❌ Seu pedido foi cancelado.",
        _ => "Seu pedido foi atualizado."
    };

    await client.SendMessage(
        chatId: clienteChatId,
        text: $"{statusMsg}\n\n📋 Pedido #{pedidoId}",
        parseMode: ParseMode.Markdown,
        cancellationToken: token
    );
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