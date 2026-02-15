using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.EntityFrameworkCore;
using AtendeBot.Bot.Data;
using AtendeBot.Bot.Services;
using DotNetEnv;

// Carrega variáveis do .env
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

var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
    .Options;

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

// ==========================================
// HANDLER PRINCIPAL
// Agora trata MENSAGENS e CALLBACKS (cliques em botões)
// ==========================================
async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token)
{
  // ==========================================
  // CALLBACK = cliente CLICOU num botão
  // ==========================================
  if (update.CallbackQuery is { } callback)
  {
    var chatId = callback.Message!.Chat.Id;
    var callbackData = callback.Data!;
    var nomeCallback = callback.From.FirstName ?? "Cliente";

    Console.WriteLine($"🔘 [{chatId}] Clicou: {callbackData}");

    using var db = new AppDbContext(dbOptions);
    var pedidoService = new PedidoService(db);

    await client.AnswerCallbackQuery(callback.Id, cancellationToken: token);

    BotResponse response;

    // ==========================================
    // BOTÕES DO MENU PRINCIPAL
    // ==========================================
    if (callbackData == "menu_cardapio")
    {
      var itens = db.CardapioItens
          .Where(i => i.ComercioId == 1 && i.Disponivel)
          .ToList();

      if (itens.Count == 0)
      {
        response = new BotResponse { Texto = "😕 Nenhum item disponível no momento." };
      }
      else
      {
        var cardapioTexto = "🍽️ *CARDÁPIO*\n\n";
        for (int i = 0; i < itens.Count; i++)
        {
          var item = itens[i];
          cardapioTexto += $"{i + 1}️⃣ {item.Nome} - R$ {item.Preco:F2}\n";
          if (!string.IsNullOrEmpty(item.Descricao))
            cardapioTexto += $"   _{item.Descricao}_\n";
          cardapioTexto += "\n";
        }

        var botoesCardapio = new List<List<InlineKeyboardButton>>
            {
                new() { InlineKeyboardButton.WithCallbackData("🛒 Fazer Pedido", "menu_pedir") },
                new() { InlineKeyboardButton.WithCallbackData("🔙 Voltar ao menu", "menu_voltar") }
            };

        response = new BotResponse
        {
          Texto = cardapioTexto,
          Botoes = new InlineKeyboardMarkup(botoesCardapio)
        };
      }
    }
    else if (callbackData == "menu_pedir")
    {
      response = pedidoService.IniciarPedido(chatId);
    }
    else if (callbackData == "menu_status")
    {
      var pedidosCliente = db.Pedidos
          .Where(p => p.ClienteTelegramId == chatId.ToString())
          .OrderByDescending(p => p.CriadoEm)
          .Take(3)
          .ToList();

      if (pedidosCliente.Count == 0)
      {
        response = new BotResponse { Texto = "📭 Você ainda não fez nenhum pedido." };
      }
      else
      {
        var statusTexto = "📋 *SEUS ÚLTIMOS PEDIDOS:*\n\n";
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

          statusTexto += $"{statusEmoji} *Pedido #{p.Id}*\n";
          statusTexto += $"   Total: R$ {p.Total:F2}\n";
          statusTexto += $"   Status: {p.Status}\n";
          statusTexto += $"   Data: {p.CriadoEm:dd/MM/yyyy HH:mm}\n\n";
        }

        var botoesStatus = new List<List<InlineKeyboardButton>>
            {
                new() { InlineKeyboardButton.WithCallbackData("🔙 Voltar ao menu", "menu_voltar") }
            };

        response = new BotResponse
        {
          Texto = statusTexto,
          Botoes = new InlineKeyboardMarkup(botoesStatus)
        };
      }
    }
    else if (callbackData == "menu_voltar")
    {
      var botoesMenu = new List<List<InlineKeyboardButton>>
        {
            new() { InlineKeyboardButton.WithCallbackData("🍽️ Ver Cardápio", "menu_cardapio") },
            new() { InlineKeyboardButton.WithCallbackData("🛒 Fazer Pedido", "menu_pedir") },
            new() { InlineKeyboardButton.WithCallbackData("📋 Meus Pedidos", "menu_status") }
        };

      response = new BotResponse
      {
        Texto = $"O que deseja fazer? 😊",
        Botoes = new InlineKeyboardMarkup(botoesMenu)
      };
    }
    // ==========================================
    // BOTÕES DO PEDIDO (já existia)
    // ==========================================
    else
    {
      response = pedidoService.ProcessarCallback(chatId, callbackData);
    }

    await client.SendMessage(
        chatId: chatId,
        text: response.Texto,
        parseMode: ParseMode.Markdown,
        replyMarkup: response.Botoes,
        cancellationToken: token
    );

    await NotificarDonoSeNecessario(client, pedidoService, token);

    return;
  }

  // ==========================================
  // MENSAGEM = cliente DIGITOU texto
  // ==========================================
  if (update.Message is not { } message)
    return;
  if (message.Text is not { } texto)
    return;

  var msgChatId = message.Chat.Id;
  var nomeCliente = message.From?.FirstName ?? "Cliente";

  Console.WriteLine($"📩 [{msgChatId}] {nomeCliente}: {texto}");

  using var msgDb = new AppDbContext(dbOptions);
  var msgPedidoService = new PedidoService(msgDb);

  BotResponse resposta;

  // Se tá no meio de um pedido e não é comando
  if (msgPedidoService.ClienteEstaPedindo(msgChatId) && !texto.StartsWith("/"))
  {
    resposta = msgPedidoService.ProcessarMensagem(msgChatId, texto);

    await client.SendMessage(
        chatId: msgChatId,
        text: resposta.Texto,
        parseMode: ParseMode.Markdown,
        replyMarkup: resposta.Botoes,
        cancellationToken: token
    );

    // Notifica o dono se um pedido foi finalizado
    await NotificarDonoSeNecessario(client, msgPedidoService, token);

    return;
  }

  // ==========================================
  // COMANDOS
  // ==========================================
  switch (texto)
  {
    case "/start":
      var botoesStart = new List<List<InlineKeyboardButton>>
    {
        new() { InlineKeyboardButton.WithCallbackData("🍽️ Ver Cardápio", "menu_cardapio") },
        new() { InlineKeyboardButton.WithCallbackData("🛒 Fazer Pedido", "menu_pedir") },
        new() { InlineKeyboardButton.WithCallbackData("📋 Meus Pedidos", "menu_status") }
    };

      resposta = new BotResponse
      {
        Texto = $"Olá, {nomeCliente}! 👋\n\n"
              + "Bem-vindo ao *AtendeBot*! 🤖\n\n"
              + "O que deseja fazer?",
        Botoes = new InlineKeyboardMarkup(botoesStart)
      };
      break;

    case "/cardapio":
      var itens = msgDb.CardapioItens
          .Where(i => i.ComercioId == 1 && i.Disponivel)
          .ToList();

      if (itens.Count == 0)
      {
        resposta = new BotResponse { Texto = "😕 Nenhum item disponível no momento." };
        break;
      }

      var cardapioTexto = "🍽️ *CARDÁPIO*\n\n";
      for (int i = 0; i < itens.Count; i++)
      {
        var item = itens[i];
        cardapioTexto += $"{i + 1}️⃣ {item.Nome} - R$ {item.Preco:F2}\n";
        if (!string.IsNullOrEmpty(item.Descricao))
          cardapioTexto += $"   _{item.Descricao}_\n";
        cardapioTexto += "\n";
      }
      cardapioTexto += "Para pedir, envie /pedir";
      resposta = new BotResponse { Texto = cardapioTexto };
      break;

    case "/pedir":
      resposta = msgPedidoService.IniciarPedido(msgChatId);
      break;

    case "/status":
      var pedidosCliente = msgDb.Pedidos
          .Where(p => p.ClienteTelegramId == msgChatId.ToString())
          .OrderByDescending(p => p.CriadoEm)
          .Take(3)
          .ToList();

      if (pedidosCliente.Count == 0)
      {
        resposta = new BotResponse { Texto = "📭 Você ainda não fez nenhum pedido.\n\nUse /pedir pra começar!" };
        break;
      }

      var statusTexto = "📋 *SEUS ÚLTIMOS PEDIDOS:*\n\n";
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

        statusTexto += $"{statusEmoji} *Pedido #{p.Id}*\n";
        statusTexto += $"   Total: R$ {p.Total:F2}\n";
        statusTexto += $"   Status: {p.Status}\n";
        statusTexto += $"   Data: {p.CriadoEm:dd/MM/yyyy HH:mm}\n\n";
      }
      resposta = new BotResponse { Texto = statusTexto };
      break;

    case "/pedidos":
      if (msgChatId != ownerChatId)
      {
        resposta = new BotResponse { Texto = "❌ Esse comando é só para o dono do estabelecimento." };
        break;
      }

      var pedidosNovos = msgDb.Pedidos
          .Where(p => p.ComercioId == 1 && p.Status != "entregue" && p.Status != "cancelado")
          .OrderByDescending(p => p.CriadoEm)
          .Take(10)
          .ToList();

      if (pedidosNovos.Count == 0)
      {
        resposta = new BotResponse { Texto = "✅ Nenhum pedido pendente no momento!" };
        break;
      }

      var pedidosTexto = "📋 *PEDIDOS PENDENTES:*\n\n";
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

        pedidosTexto += $"{statusEmoji} *Pedido #{p.Id}*\n";
        pedidosTexto += $"   Total: R$ {p.Total:F2}\n";
        pedidosTexto += $"   Status: {p.Status}\n";
        pedidosTexto += $"   Entrega: {(p.TipoEntrega == "entrega" ? p.EnderecoEntrega : "Retirada")}\n";
        if (!string.IsNullOrEmpty(p.Observacao))
          pedidosTexto += $"   Obs: {p.Observacao}\n";
        pedidosTexto += $"   Data: {p.CriadoEm:dd/MM/yyyy HH:mm}\n\n";
      }

      pedidosTexto += "Para atualizar status, envie:\n";
      pedidosTexto += "`/atualizar 1 preparando`\n\n";
      pedidosTexto += "Status disponíveis:\n";
      pedidosTexto += "• preparando\n";
      pedidosTexto += "• saiu\\_entrega\n";
      pedidosTexto += "• pronto\\_retirada\n";
      pedidosTexto += "• entregue\n";
      pedidosTexto += "• cancelado";
      resposta = new BotResponse { Texto = pedidosTexto };
      break;

    case string s when s.StartsWith("/atualizar"):
      if (msgChatId != ownerChatId)
      {
        resposta = new BotResponse { Texto = "❌ Esse comando é só para o dono do estabelecimento." };
        break;
      }

      var partes = texto.Split(' ');
      if (partes.Length != 3)
      {
        resposta = new BotResponse { Texto = "❌ Formato: /atualizar [id] [status]\nExemplo: `/atualizar 1 preparando`" };
        break;
      }

      if (!int.TryParse(partes[1], out int pedidoId))
      {
        resposta = new BotResponse { Texto = "❌ ID do pedido inválido." };
        break;
      }

      var statusValidos = new[] { "preparando", "saiu_entrega", "pronto_retirada", "entregue", "cancelado" };
      var novoStatus = partes[2];

      if (!statusValidos.Contains(novoStatus))
      {
        resposta = new BotResponse { Texto = "❌ Status inválido. Use: preparando, saiu_entrega, pronto_retirada, entregue, cancelado" };
        break;
      }

      var pedidoAtualizar = msgDb.Pedidos.FirstOrDefault(p => p.Id == pedidoId);
      if (pedidoAtualizar == null)
      {
        resposta = new BotResponse { Texto = $"❌ Pedido #{pedidoId} não encontrado." };
        break;
      }

      pedidoAtualizar.Status = novoStatus;
      msgDb.SaveChanges();

      resposta = new BotResponse { Texto = $"✅ Pedido #{pedidoId} atualizado para *{novoStatus}*!" };

      // Notifica o cliente
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

    default:
      var botoesMenu = new List<List<InlineKeyboardButton>>
    {
        new() { InlineKeyboardButton.WithCallbackData("🍽️ Ver Cardápio", "menu_cardapio") },
        new() { InlineKeyboardButton.WithCallbackData("🛒 Fazer Pedido", "menu_pedir") },
        new() { InlineKeyboardButton.WithCallbackData("📋 Meus Pedidos", "menu_status") }
    };

      resposta = new BotResponse
      {
        Texto = $"Olá, {nomeCliente}! 👋\n\n"
              + "Como posso te ajudar?",
        Botoes = new InlineKeyboardMarkup(botoesMenu)
      };
      break;
  }

  await client.SendMessage(
      chatId: msgChatId,
      text: resposta.Texto,
      parseMode: ParseMode.Markdown,
      replyMarkup: resposta.Botoes,
      cancellationToken: token
  );
}

// ==========================================
// NOTIFICAR DONO
// Função separada pra não repetir código
// ==========================================
async Task NotificarDonoSeNecessario(ITelegramBotClient client, PedidoService pedidoService, CancellationToken token)
{
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

Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken token)
{
  Console.WriteLine($"❌ Erro: {exception.Message}");
  return Task.CompletedTask;
}