using AtendeBot.Bot.Data;
using AtendeBot.Bot.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace AtendeBot.Bot.Services;

// Classe que representa uma resposta com texto E botões
public class BotResponse
{
    public string Texto { get; set; } = "";
    public InlineKeyboardMarkup? Botoes { get; set; }
}

public class PedidoService
{
    private static readonly Dictionary<long, PedidoState> _estados = new();

    private readonly AppDbContext _db;

    public Pedido? UltimoPedidoFinalizado { get; private set; }

    public PedidoService(AppDbContext db)
    {
        _db = db;
    }

    public bool ClienteEstaPedindo(long chatId)
    {
        return _estados.ContainsKey(chatId);
    }

    // ==========================================
    // INICIAR PEDIDO
    // Cria estado novo e mostra cardápio com botões
    // ==========================================
    public BotResponse IniciarPedido(long chatId)
    {
        _estados[chatId] = new PedidoState();
        return MontarCardapioComBotoes();
    }

    // ==========================================
    // Monta o cardápio com um botão pra cada item
    // Cada botão tem um callback_data tipo "item_1", "item_2"...
    // Quando clicado, o Telegram manda esse dado pra gente
    // ==========================================
    private BotResponse MontarCardapioComBotoes()
    {
        var itens = _db.CardapioItens
            .Where(i => i.ComercioId == 1 && i.Disponivel)
            .ToList();

        var texto = "📝 *NOVO PEDIDO*\n\nEscolha um item:\n\n";
        for (int i = 0; i < itens.Count; i++)
        {
            texto += $"• {itens[i].Nome} - R$ {itens[i].Preco:F2}\n";
        }

        // Cria os botões - cada item vira um botão
        // InlineKeyboardButton.WithCallbackData("texto visível", "dado escondido")
        var botoes = new List<List<InlineKeyboardButton>>();

        for (int i = 0; i < itens.Count; i++)
        {
            // Cada botão fica numa linha separada
            // "item_1" é o que a gente recebe quando o cliente clica
            botoes.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    text: $"🍽️ {itens[i].Nome} - R$ {itens[i].Preco:F2}",
                    callbackData: $"item_{i + 1}"
                )
            });
        }

        // Botão de cancelar na última linha
        botoes.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("❌ Cancelar pedido", "cancelar")
        });

        return new BotResponse
        {
            Texto = texto,
            // InlineKeyboardMarkup transforma nossa lista de botões
            // no formato que o Telegram entende
            Botoes = new InlineKeyboardMarkup(botoes)
        };
    }

    // ==========================================
    // PROCESSAR CALLBACK
    // Essa função é chamada quando o cliente CLICA num botão
    // O "callbackData" é o dado escondido do botão (ex: "item_1")
    // ==========================================
    public BotResponse ProcessarCallback(long chatId, string callbackData)
    {
        if (!_estados.ContainsKey(chatId))
            return new BotResponse { Texto = "Você não tem um pedido em andamento. Use /pedir" };

        var state = _estados[chatId];

        // Se clicou em cancelar, cancela o pedido
        if (callbackData == "cancelar")
        {
            _estados.Remove(chatId);
            return new BotResponse { Texto = "❌ Pedido cancelado." };
        }

        switch (state.Etapa)
        {
            case "escolher_item":
                return ProcessarEscolhaItem(chatId, callbackData, state);

            case "mais_itens":
                return ProcessarMaisItens(chatId, callbackData, state);

            case "tipo_entrega":
                return ProcessarTipoEntrega(chatId, callbackData, state);

            case "observacao_pergunta":
                return ProcessarObservacaoPergunta(chatId, callbackData, state);

            default:
                return new BotResponse { Texto = "Erro no pedido. Use /pedir pra recomeçar." };
        }
    }

    // ==========================================
    // PROCESSAR MENSAGEM DE TEXTO
    // Ainda precisa disso pra etapas onde o cliente DIGITA
    // (quantidade, endereço, observação)
    // ==========================================
    public BotResponse ProcessarMensagem(long chatId, string texto)
    {
        if (!_estados.ContainsKey(chatId))
            return new BotResponse { Texto = "Você não tem um pedido em andamento. Use /pedir" };

        var state = _estados[chatId];

        if (texto == "0")
        {
            _estados.Remove(chatId);
            return new BotResponse { Texto = "❌ Pedido cancelado." };
        }

        switch (state.Etapa)
        {
            case "escolher_quantidade":
                return ProcessarQuantidade(chatId, texto, state);

            case "endereco":
                return ProcessarEndereco(chatId, texto, state);

            case "observacao_texto":
                return ProcessarObservacaoTexto(chatId, texto, state);

            default:
                return new BotResponse { Texto = "Use os botões para continuar! ☝️" };
        }
    }

    // ==========================================
    // ESCOLHER ITEM (via botão)
    // callbackData chega como "item_1", "item_2"...
    // A gente extrai o número e busca o item
    // ==========================================
    private BotResponse ProcessarEscolhaItem(long chatId, string callbackData, PedidoState state)
    {
        // "item_1".Replace("item_", "") = "1"
        var numeroParte = callbackData.Replace("item_", "");

        var itens = _db.CardapioItens
            .Where(i => i.ComercioId == 1 && i.Disponivel)
            .ToList();

        if (!int.TryParse(numeroParte, out int numero) || numero < 1 || numero > itens.Count)
        {
            return new BotResponse { Texto = "❌ Item inválido." };
        }

        var itemEscolhido = itens[numero - 1];
        state.Itens.Add(new PedidoItemTemp { Item = itemEscolhido, Quantidade = 0 });
        state.Etapa = "escolher_quantidade";

        // Quantidade o cliente ainda DIGITA (não tem botão)
        return new BotResponse
        {
            Texto = $"Você escolheu: *{itemEscolhido.Nome}*\n\nQuantas unidades? (digite o número)"
        };
    }

    // ==========================================
    // QUANTIDADE (via texto digitado)
    // Depois de digitar, mostra botões "Mais itens" ou "Finalizar"
    // ==========================================
    private BotResponse ProcessarQuantidade(long chatId, string texto, PedidoState state)
    {
        if (!int.TryParse(texto, out int qtd) || qtd < 1 || qtd > 50)
        {
            return new BotResponse { Texto = "❌ Quantidade inválida. Digite um número de 1 a 50:" };
        }

        var ultimoItem = state.Itens.Last();
        ultimoItem.Quantidade = qtd;

        state.Etapa = "mais_itens";

        // Agora mostra botões em vez de pedir pra digitar 1 ou 2
        var botoes = new List<List<InlineKeyboardButton>>
        {
            new() { InlineKeyboardButton.WithCallbackData("➕ Adicionar mais itens", "mais_sim") },
            new() { InlineKeyboardButton.WithCallbackData("✅ Finalizar pedido", "mais_nao") }
        };

        return new BotResponse
        {
            Texto = $"✅ {qtd}x {ultimoItem.Item.Nome} adicionado!\n\nDeseja mais alguma coisa?",
            Botoes = new InlineKeyboardMarkup(botoes)
        };
    }

    // ==========================================
    // MAIS ITENS (via botão)
    // "mais_sim" = mostra cardápio de novo (sem resetar!)
    // "mais_nao" = vai pra tipo de entrega
    // ==========================================
    private BotResponse ProcessarMaisItens(long chatId, string callbackData, PedidoState state)
    {
        if (callbackData == "mais_sim")
        {
            state.Etapa = "escolher_item";

            // Mostra cardápio SEM resetar (mesmo fix de ontem!)
            var itens = _db.CardapioItens
                .Where(i => i.ComercioId == 1 && i.Disponivel)
                .ToList();

            var texto = "Escolha mais um item:\n\n";
            for (int i = 0; i < itens.Count; i++)
            {
                texto += $"• {itens[i].Nome} - R$ {itens[i].Preco:F2}\n";
            }

            var botoes = new List<List<InlineKeyboardButton>>();
            for (int i = 0; i < itens.Count; i++)
            {
                botoes.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(
                        text: $"🍽️ {itens[i].Nome} - R$ {itens[i].Preco:F2}",
                        callbackData: $"item_{i + 1}"
                    )
                });
            }
            botoes.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("❌ Cancelar pedido", "cancelar")
            });

            return new BotResponse
            {
                Texto = texto,
                Botoes = new InlineKeyboardMarkup(botoes)
            };
        }
        else if (callbackData == "mais_nao")
        {
            state.Etapa = "tipo_entrega";

            var botoes = new List<List<InlineKeyboardButton>>
            {
                new() { InlineKeyboardButton.WithCallbackData("🛵 Entrega", "entrega_sim") },
                new() { InlineKeyboardButton.WithCallbackData("🏪 Retirada no local", "entrega_nao") }
            };

            return new BotResponse
            {
                Texto = "🚗 Tipo de entrega:",
                Botoes = new InlineKeyboardMarkup(botoes)
            };
        }

        return new BotResponse { Texto = "❌ Opção inválida." };
    }

    // ==========================================
    // TIPO ENTREGA (via botão)
    // "entrega_sim" = pede endereço (texto)
    // "entrega_nao" = vai pra observação
    // ==========================================
    private BotResponse ProcessarTipoEntrega(long chatId, string callbackData, PedidoState state)
    {
        if (callbackData == "entrega_sim")
        {
            state.TipoEntrega = "entrega";
            state.Etapa = "endereco";
            return new BotResponse { Texto = "📍 Qual o endereço de entrega? (digite)" };
        }
        else if (callbackData == "entrega_nao")
        {
            state.TipoEntrega = "retirada";
            state.Etapa = "observacao_pergunta";

            var botoes = new List<List<InlineKeyboardButton>>
            {
                new() { InlineKeyboardButton.WithCallbackData("✏️ Sim, quero adicionar", "obs_sim") },
                new() { InlineKeyboardButton.WithCallbackData("👍 Não, tá ótimo!", "obs_nao") }
            };

            return new BotResponse
            {
                Texto = "📝 Deseja adicionar alguma observação?\n_(Ex: tirar cebola, sem gelo, extra catupiry...)_",
                Botoes = new InlineKeyboardMarkup(botoes)
            };
        }

        return new BotResponse { Texto = "❌ Opção inválida." };
    }

    // ==========================================
    // ENDEREÇO (via texto digitado)
    // Depois vai pra observação com botões
    // ==========================================
    private BotResponse ProcessarEndereco(long chatId, string texto, PedidoState state)
    {
        state.Endereco = texto;
        state.Etapa = "observacao_pergunta";

        var botoes = new List<List<InlineKeyboardButton>>
        {
            new() { InlineKeyboardButton.WithCallbackData("✏️ Sim, quero adicionar", "obs_sim") },
            new() { InlineKeyboardButton.WithCallbackData("👍 Não, tá ótimo!", "obs_nao") }
        };

        return new BotResponse
        {
            Texto = "📝 Deseja adicionar alguma observação?\n_(Ex: tirar cebola, sem gelo, extra catupiry...)_",
            Botoes = new InlineKeyboardMarkup(botoes)
        };
    }

    // ==========================================
    // OBSERVAÇÃO PERGUNTA (via botão)
    // "obs_sim" = pede pra digitar
    // "obs_nao" = finaliza direto
    // ==========================================
    private BotResponse ProcessarObservacaoPergunta(long chatId, string callbackData, PedidoState state)
    {
        if (callbackData == "obs_sim")
        {
            state.Etapa = "observacao_texto";
            return new BotResponse { Texto = "✏️ Digite sua observação:" };
        }
        else if (callbackData == "obs_nao")
        {
            return FinalizarPedido(chatId, state);
        }

        return new BotResponse { Texto = "❌ Opção inválida." };
    }

    // ==========================================
    // OBSERVAÇÃO TEXTO (via texto digitado)
    // ==========================================
    private BotResponse ProcessarObservacaoTexto(long chatId, string texto, PedidoState state)
    {
        state.Observacao = texto;
        return FinalizarPedido(chatId, state);
    }

    // ==========================================
    // FINALIZAR PEDIDO
    // Salva no banco e monta o resumo
    // ==========================================
    private BotResponse FinalizarPedido(long chatId, PedidoState state)
    {
        decimal total = state.Itens.Sum(i => i.Item.Preco * i.Quantidade);

        var pedido = new Pedido
        {
            ComercioId = 1,
            ClienteTelegramId = chatId.ToString(),
            TipoEntrega = state.TipoEntrega,
            EnderecoEntrega = state.Endereco,
            Observacao = state.Observacao,
            Status = "novo",
            Total = total,
            CriadoEm = DateTime.Now
        };

        _db.Pedidos.Add(pedido);
        _db.SaveChanges();

        foreach (var item in state.Itens)
        {
            var pedidoItem = new PedidoItem
            {
                PedidoId = pedido.Id,
                CardapioItemId = item.Item.Id,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.Item.Preco
            };
            _db.PedidoItens.Add(pedidoItem);
        }
        _db.SaveChanges();

        UltimoPedidoFinalizado = pedido;

        _estados.Remove(chatId);

        var resumo = "✅ *PEDIDO CONFIRMADO!*\n\n";
        resumo += $"📋 Pedido #{pedido.Id}\n\n";
        foreach (var item in state.Itens)
        {
            resumo += $"  {item.Quantidade}x {item.Item.Nome} - R$ {(item.Item.Preco * item.Quantidade):F2}\n";
        }
        resumo += $"\n💰 *Total: R$ {total:F2}*\n";
        resumo += $"🚗 {(state.TipoEntrega == "entrega" ? $"Entrega em: {state.Endereco}" : "Retirada no local")}\n";

        if (!string.IsNullOrEmpty(state.Observacao))
        {
            resumo += $"📝 Obs: {state.Observacao}\n";
        }

        resumo += "\nObrigado pelo pedido! 😊";

        return new BotResponse { Texto = resumo };
    }
}