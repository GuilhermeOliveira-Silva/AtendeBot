using AtendeBot.Bot.Data;
using AtendeBot.Bot.Models;

namespace AtendeBot.Bot.Services;

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

    public string IniciarPedido(long chatId)
    {
        _estados[chatId] = new PedidoState();

        var itens = _db.CardapioItens
            .Where(i => i.ComercioId == 1 && i.Disponivel)
            .ToList();

        var texto = "📝 *NOVO PEDIDO*\n\n";
        texto += "Escolha o item pelo número:\n\n";
        for (int i = 0; i < itens.Count; i++)
        {
            texto += $"{i + 1}. {itens[i].Nome} - R$ {itens[i].Preco:F2}\n";
        }
        texto += "\n0 - Cancelar pedido";

        return texto;
    }

    public string ProcessarMensagem(long chatId, string texto)
    {
        if (!_estados.ContainsKey(chatId))
            return "Você não tem um pedido em andamento. Use /pedir";

        var state = _estados[chatId];

        if (texto == "0")
        {
            _estados.Remove(chatId);
            return "❌ Pedido cancelado.";
        }

        switch (state.Etapa)
        {
            case "escolher_item":
                return ProcessarEscolhaItem(chatId, texto, state);

            case "escolher_quantidade":
                return ProcessarQuantidade(chatId, texto, state);

            case "mais_itens":
                return ProcessarMaisItens(chatId, texto, state);

            case "tipo_entrega":
                return ProcessarTipoEntrega(chatId, texto, state);

            case "endereco":
                return ProcessarEndereco(chatId, texto, state);

            case "observacao_pergunta":
                return ProcessarObservacaoPergunta(chatId, texto, state);

            case "observacao_texto":
                return ProcessarObservacaoTexto(chatId, texto, state);

            default:
                return "Erro no pedido. Use /pedir pra recomeçar.";
        }
    }

    private string ProcessarEscolhaItem(long chatId, string texto, PedidoState state)
    {
        var itens = _db.CardapioItens
            .Where(i => i.ComercioId == 1 && i.Disponivel)
            .ToList();

        if (!int.TryParse(texto, out int numero) || numero < 1 || numero > itens.Count)
        {
            return "❌ Número inválido. Escolha um item da lista:";
        }

        var itemEscolhido = itens[numero - 1];
        state.Itens.Add(new PedidoItemTemp { Item = itemEscolhido, Quantidade = 0 });
        state.Etapa = "escolher_quantidade";

        return $"Você escolheu: *{itemEscolhido.Nome}*\n\nQuantas unidades?";
    }

    private string ProcessarQuantidade(long chatId, string texto, PedidoState state)
    {
        if (!int.TryParse(texto, out int qtd) || qtd < 1 || qtd > 50)
        {
            return "❌ Quantidade inválida. Digite um número de 1 a 50:";
        }

        var ultimoItem = state.Itens.Last();
        ultimoItem.Quantidade = qtd;

        state.Etapa = "mais_itens";

        return $"✅ {qtd}x {ultimoItem.Item.Nome} adicionado!\n\n"
             + "Deseja mais alguma coisa?\n"
             + "1 - Sim, quero adicionar mais\n"
             + "2 - Não, finalizar pedido";
    }

    private string ProcessarMaisItens(long chatId, string texto, PedidoState state)
    {
        if (texto == "1")
        {
            state.Etapa = "escolher_item";

            var itens = _db.CardapioItens
                .Where(i => i.ComercioId == 1 && i.Disponivel)
                .ToList();

            var cardapio = "Escolha o item pelo número:\n\n";
            for (int i = 0; i < itens.Count; i++)
            {
                cardapio += $"{i + 1}. {itens[i].Nome} - R$ {itens[i].Preco:F2}\n";
            }
            cardapio += "\n0 - Cancelar pedido";

            return cardapio;
        }
        else if (texto == "2")
        {
            state.Etapa = "tipo_entrega";
            return "🚗 Tipo de entrega:\n\n"
                 + "1 - Entrega\n"
                 + "2 - Retirada no local";
        }

        return "❌ Opção inválida. Digite 1 ou 2:";
    }

    private string ProcessarTipoEntrega(long chatId, string texto, PedidoState state)
    {
        if (texto == "1")
        {
            state.TipoEntrega = "entrega";
            state.Etapa = "endereco";
            return "📍 Qual o endereço de entrega?";
        }
        else if (texto == "2")
        {
            state.TipoEntrega = "retirada";
            state.Etapa = "observacao_pergunta";
            return "📝 Deseja adicionar alguma observação ao pedido?\n"
                 + "_(Ex: tirar cebola, sem gelo, extra catupiry...)_\n\n"
                 + "1 - Sim\n"
                 + "2 - Não";
        }

        return "❌ Opção inválida. Digite 1 ou 2:";
    }

    private string ProcessarEndereco(long chatId, string texto, PedidoState state)
    {
        state.Endereco = texto;
        state.Etapa = "observacao_pergunta";

        return "📝 Deseja adicionar alguma observação ao pedido?\n"
             + "_(Ex: tirar cebola, sem gelo, extra catupiry...)_\n\n"
             + "1 - Sim\n"
             + "2 - Não";
    }

    private string ProcessarObservacaoPergunta(long chatId, string texto, PedidoState state)
    {
        if (texto == "1")
        {
            state.Etapa = "observacao_texto";
            return "✏️ Digite sua observação:";
        }
        else if (texto == "2")
        {
            return FinalizarPedido(chatId, state);
        }

        return "❌ Opção inválida. Digite 1 ou 2:";
    }

    private string ProcessarObservacaoTexto(long chatId, string texto, PedidoState state)
    {
        state.Observacao = texto;
        return FinalizarPedido(chatId, state);
    }

    private string FinalizarPedido(long chatId, PedidoState state)
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

    // Guarda o ID do pedido pra notificar o dono
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

    return resumo;
}
}