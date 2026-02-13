using AtendeBot.Bot.Models;

namespace AtendeBot.Bot.Services;

public class PedidoState
{
    public string Etapa { get; set; } = "escolher_item";
    public List<PedidoItemTemp> Itens { get; set; } = new();
    public string? TipoEntrega { get; set; }
    public string? Endereco { get; set; }
    public string? Observacao { get; set; } 

}

public class PedidoItemTemp
{
    public CardapioItem Item { get; set; } = null!;
    public int Quantidade { get; set; }
}